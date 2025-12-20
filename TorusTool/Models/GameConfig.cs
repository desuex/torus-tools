namespace TorusTool.Models;

public enum PlatformType
{
    PC,
    PS3,
    Wii360,
    Xbox,
    Wii,
    Nintendo3DS
}

public class GameConfig
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public PlatformType Platform { get; set; }
    public bool IsBigEndian { get; set; }
    public string DefaultPath { get; set; } = string.Empty;
}
