using System.Text.Json;

public class AppleNormalizer : ITickNormalizer
{
    public SourceType Source => SourceType.AppleStock;

    public NormalizedTick? TryNormalize(RawTick raw)
    {
        try
        {
            using var doc = JsonDocument.Parse(raw.Payload);
            var root = doc.RootElement;

            if(!root.TryGetProperty("e", out var eventProp))
                return null;

            if (eventProp.GetString() != "trade")
                return null;

            var symbol = root.GetProperty("s").GetString()!;
            var price = decimal.Parse(root.GetProperty("p").GetString()!);
            var volume = decimal.Parse(root.GetProperty("q").GetString()!);
            var tradeTimeMs = root.GetProperty("T").GetInt64();

            return new NormalizedTick
            {
                Source = SourceType.AppleStock,
                Symbol = symbol,
                Price = price,
                Volume = volume,
                ExchangeTimestamp = DateTimeOffset.FromUnixTimeMilliseconds(tradeTimeMs),
                ReceivedAt = raw.ReceivedAt
            };
        }
        catch
        {
            return null;
        }
    }
}