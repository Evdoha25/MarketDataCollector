using Microsoft.Extensions.Logging;

public class AppleStockSource : WebSocketSourceBase
{
    public AppleStockSource(SourceSettings settings, ILogger<AppleStockSource> logger)
        : base(settings, logger) { }

    public override SourceType Source => SourceType.AppleStock;

    public override string BuildSubscriptionMessage()
    {
        var streams = Settings.Symbols
            .Select(s => $"\"{s.ToLower()}@trade\"");

        var joined = string.Join(",", streams);

        return $$"""{"method":"SUBSCRIBE","params":[{{joined}}], "id":1}""";
    }

    protected override bool IsServiceMessage(string message)
    {
        return message.Contains("\"result\"");
    }
}