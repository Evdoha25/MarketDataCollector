public class NormalizedTick
{
    public required SourceType Source { get; set; }
    public required string Symbol { get; set; }
    public required decimal Price { get; set; }
    public required decimal Volume { get; set; }
    public DateTimeOffset ExchangeTimestamp { get; set; }
    public required DateTimeOffset ReceivedAt { get; set; }

    public string DeduplicationKey => $"{Source}:{Symbol}:{ExchangeTimestamp.ToUnixTimeMilliseconds()}:{Price}";
}
