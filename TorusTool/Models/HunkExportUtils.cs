using System.Collections.Generic;
using YamlDotNet.Serialization;

namespace TorusTool.Models;

public class HunkManifest
{
    public string GlobalHeader { get; set; } = "SimpleHunkManifest v1";
    public string GameName { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    public string OriginalFileHash { get; set; } = string.Empty; // SHA1
    public bool IsBigEndian { get; set; }
    public List<HunkManifestNode> RootNodes { get; set; } = new();
}

public class HunkManifestNode
{
    public string Name { get; set; } = string.Empty;
    public bool IsFolder { get; set; }

    // Only populated if IsFolder is true
    public List<HunkManifestNode> Children { get; set; } = new();

    // Only populated if IsFolder is false (it's a file)
    public List<HunkManifestRecord> Records { get; set; } = new();
}

public class HunkManifestRecord
{
    public string Type { get; set; } = string.Empty;
    public uint TypeId { get; set; }
    public uint OriginalSize { get; set; }

    // Path to the .bin file containing the raw data for this record, relative to the manifest
    public string DataFile { get; set; } = string.Empty;

    // Global index to preserve order of records (flattened) regardless of tree structure
    public int SortIndex { get; set; }
}
