using System.Collections.Generic;

namespace TorusTool.Models;

public class PackfileEntry
{
    public uint NameCrc { get; set; } // Legacy field, might be unused now
    public uint Offset { get; set; }
    public uint Size { get; set; } // This is usually COMPRESSED size in the container
    public string SuggestedExtension { get; set; } = "dat";
    
    // New Fields for Monster Jam format
    public string FullPath { get; set; } = string.Empty;
    public bool IsCompressed { get; set; }
    public uint OriginalSize { get; set; } // Decompressed size
    public int PointerIndex { get; set; }

    public string DisplayName => !string.IsNullOrEmpty(FullPath) ? FullPath : $"{NameCrc:X8}.{SuggestedExtension}";
}

public class Packfile
{
    public List<PackfileEntry> Entries { get; set; } = new();
}
