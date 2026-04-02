using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.UseWebSockets();

var clientCount = 0;

app.Map("/ws", async context =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = 400;
        await context.Response.WriteAsync("Expected WebSocket");
        return;
    }

    var ws = await context.WebSockets.AcceptWebSocketAsync();
    var id = Interlocked.Increment(ref clientCount);
    Console.WriteLine($"[Client {id}] Connected");

    try
    {
        var sub = await ReadMessageAsync(ws);
        Console.WriteLine($"[Client {id}] Sub: {sub}");

        var format = sub.Contains("SUBSCRIBE") ? "Apple" : "Samsung";
        Console.WriteLine($"[Client {id}] Format: {format}");

        await SendTicksAsync(ws, format);
    }
    catch (WebSocketException)
    {
        
    }

    Console.WriteLine($"[Client {id}] Disconnected");
});

app.MapGet("/", () => "Mock WS Server running. Connect to ws");

Console.WriteLine("Mock server: ws://localhost:5099/ws");
app.Run("http://localhost:5099");

async Task<string> ReadMessageAsync(WebSocket ws)
{
    var buffer = new byte[4096];
    var result = await ws.ReceiveAsync(buffer, CancellationToken.None);
    return Encoding.UTF8.GetString(buffer, 0, result.Count);
}

async Task SendTicksAsync(WebSocket ws, string format)
{
    var rng = new Random();
    var symbols = new[] {"BTCUSDT", "ETHUSDT"};
    var basePrices = new Dictionary<string,decimal>
    {
        ["BTCUSDT"] = 43000m,
        ["ETHUSDT"] = 2200m
    };

    while (ws.State == WebSocketState.Open)
    {
        foreach (var symbol in symbols)
        {
            var price = basePrices[symbol]
                * (1m + (decimal)(rng.NextDouble() * 0.01 - 0.005));
            price = Math.Round(price, 2);

            var volume = Math.Round((decimal)(rng.NextDouble() * 2 + 0.01), 4);
            var ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            var json = format == "apple"
            ? AppleTick(symbol, price, volume, ts)
            : SamsungTick(symbol, price, volume, ts);

            var bytes = Encoding.UTF8.GetBytes(json);
            await ws.SendAsync(
                bytes,
                WebSocketMessageType.Text,
                endOfMessage: true,
                CancellationToken.None
            );

            await Task.Delay(rng.Next(20, 50));
        }
    }

    string AppleTick(string symbol, decimal price, decimal volume, long ts)
    {
        return JsonSerializer.Serialize(new
        {
            e = "trade",
            E = ts,
            s = symbol,
            t = Random.Shared.Next(100000, 999999),
            p = price.ToString(),
            q = volume.ToString(),
            T = ts
        });
    }

    string SamsungTick(string symbol, decimal price, decimal volume, long ts)
    {
        return JsonSerializer.Serialize(new
        {
            topic = $"publicTrade.{symbol}",
            data = new []
            {
                new
                {
                    s = symbol,
                    p = price.ToString(),
                    v = volume.ToString(),
                    T = ts
                }
            }
        });
    }
}
