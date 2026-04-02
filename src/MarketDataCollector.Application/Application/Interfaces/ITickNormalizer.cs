public interface ITickNormalizer
{
    SourceType Source { get; }
    NormalizedTick? TryNormalize(RawTick raw);
}