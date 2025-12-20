using System.Collections.Generic;

namespace TorusTool.Models;

public class PackfileEntry
{
    public uint NameCrc { get; set; }
    public uint Offset { get; set; }
    public uint Size { get; set; }
    public string SuggestedExtension { get; set; } = "dat";
    public string DisplayName => $"{NameCrc:X8}.{SuggestedExtension}";
}

public class Packfile
{
    public List<PackfileEntry> Entries { get; set; } = new();
}
