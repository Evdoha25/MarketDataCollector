public class SourceSettings
{
    public required string Url { get; set; }

    public required string[] Symbols { get; set; }
    public int ReconnectBaseDelayMs { get; set; } = 3000;
    public int ReconnectMazDelayMs { get; set; } = 60000;
}