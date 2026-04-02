using Microsoft.Extensions.Logging;

public class SamsungStockSource : WebSocketSourceBase
{
    public SamsungStockSource(SourceSettings settings, ILogger<SamsungStockSource> logger)
        : base(settings, logger) { }

    public override SourceType Source => SourceType.SamsungStock;

    public override string BuildSubscriptionMessage()
    {
        var args = Settings.Symbols
            .Select(s => $"\"publicTrade.{s}\"");

        var joined = string.Join(",", args);

        return $$"""{"op":"subscribe","args":[{{joined}}]}""";
    }

    protected override bool IsServiceMessage(string message)
    {
        return message.Contains("\"success\"") || message.Contains("\"pong\"");
    }
}