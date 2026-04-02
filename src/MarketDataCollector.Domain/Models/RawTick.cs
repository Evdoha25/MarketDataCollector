public class RawTick
{
    public required SourceType Source { get; set; }
    public required string Payload { get; set; }
    public required DateTimeOffset ReceivedAt { get; set; }
}