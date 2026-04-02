using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;

public abstract class WebSocketSourceBase : IMarketDataSource
{
    protected SourceSettings Settings { get; }
    private readonly ILogger _logger;
    protected WebSocketSourceBase(SourceSettings settings, ILogger logger)
    {
        Settings = settings;
        _logger = logger;
    }

    public abstract SourceType Source { get; }
    public abstract string BuildSubscriptionMessage();
    
    protected virtual bool IsServiceMessage(string message) => false;

    public async IAsyncEnumerable<RawTick> StreamAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var channel = Channel.CreateBounded<RawTick>(
            new BoundedChannelOptions(1024)
            {
                SingleWriter = true,
                SingleReader = true,
                FullMode = BoundedChannelFullMode.Wait
            });

        var producerTask = Task.Run(() => ProduceAsync(channel.Writer, cancellationToken), cancellationToken);

        await foreach(var tick in channel.Reader.ReadAllAsync(cancellationToken))
        {
            yield return tick;
        }
    }

    private async Task ProduceAsync(ChannelWriter<RawTick> writer, CancellationToken cancellationToken)
    {
        var delay = Settings.ReconnectBaseDelayMs;

        try
        {
            while(!cancellationToken.IsCancellationRequested)
            {
                ClientWebSocket? ws = null;
                try
                {
                    ws = new ClientWebSocket();
                    await ws.ConnectAsync(new Uri(Settings.Url), cancellationToken);
                    _logger.LogInformation("[{Source}] Connected to {Url}", Source, Settings.Url);

                    delay = Settings.ReconnectBaseDelayMs;

                    var subMessage = BuildSubscriptionMessage();
                    var subBytes = Encoding.UTF8.GetBytes(subMessage);
                    await ws.SendAsync(subBytes, WebSocketMessageType.Text, true, cancellationToken);

                    await foreach (var message in ReadMessagesAsync(ws, cancellationToken))
                    {
                        if (IsServiceMessage(message))
                            continue;
                        
                        await writer.WriteAsync(new RawTick
                        {
                            Source = Source,
                            Payload = message,
                            ReceivedAt = DateTimeOffset.UtcNow
                        }, cancellationToken);
                    }
                    _logger.LogWarning("[{Source}] Server closed connection", Source);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogInformation("[{Source}] Connection error", Source);
                }
                finally
                {
                    ws?.Dispose();
                }

                _logger.LogInformation("[{Source}] Reconnecting in {Delay}ms...", Source, delay);
                await Task.Delay(delay, cancellationToken);
                delay = Math.Min(delay * 2, Settings.ReconnectMazDelayMs);
            }
        }
        finally
        {
            writer.Complete();
        }
    }

    private static async IAsyncEnumerable<string> ReadMessagesAsync(
        ClientWebSocket ws,
        [EnumeratorCancellation] CancellationToken cancellationToken
    )
    {
        var buffer = new byte[4096];

        var messageBuffer = new MemoryStream();

        while (ws.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
        {
            messageBuffer.SetLength(0);

            WebSocketReceiveResult result;
            do
            {
                result = await ws.ReceiveAsync(buffer, cancellationToken);

                if (result.MessageType == WebSocketMessageType.Close)
                    yield break;

                
                messageBuffer.Write(buffer, 0, result.Count);
            }
            while (!result.EndOfMessage);

            if(messageBuffer.Length > 0)
            {
                yield return Encoding.UTF8.GetString(
                    messageBuffer.GetBuffer(), 0, (int)messageBuffer.Length
                );
            }
        }
    }

}