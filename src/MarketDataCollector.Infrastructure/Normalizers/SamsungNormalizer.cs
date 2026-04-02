using System.Text.Json;

public class SamsungNormalizer : ITickNormalizer
{
    public SourceType Source => SourceType.SamsungStock;

    public NormalizedTick? TryNormalize(RawTick raw)
    {
        try
        {
            using var doc = JsonDocument.Parse(raw.Payload);
            var root = doc.RootElement;

            if(!root.TryGetProperty("data", out var dataArray))
                return null;

            if(dataArray.GetArrayLength() == 0)
                return null;

            var trade = dataArray[0];

            var symbol = trade.GetProperty("s").GetString()!;
            var price = decimal.Parse(trade.GetProperty("p").GetString()!);
            var volume = decimal.Parse(trade.GetProperty("v").GetString()!);
            var tradeTimeMs = trade.GetProperty("T").GetInt64();

            return new NormalizedTick
            {
                Source = SourceType.SamsungStock,
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