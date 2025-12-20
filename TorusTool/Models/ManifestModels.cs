using System.Collections.Generic;
using YamlDotNet.Serialization;

namespace TorusTool.Models;

public class ManifestRoot
{
    [YamlMember(Alias = "globalHeader")]
    public string GlobalHeader { get; set; } = string.Empty;

    [YamlMember(Alias = "gameName")]
    public string GameName { get; set; } = string.Empty;

    [YamlMember(Alias = "platform")]
    public string Platform { get; set; } = string.Empty;

    [YamlMember(Alias = "isBigEndian")]
    public bool IsBigEndian { get; set; }

    [YamlMember(Alias = "rootNodes")]
    public List<ManifestNode> RootNodes { get; set; } = new();
}

public class ManifestNode
{
    [YamlMember(Alias = "name")]
    public string Name { get; set; } = string.Empty;

    [YamlMember(Alias = "isFolder")]
    public bool IsFolder { get; set; }

    [YamlMember(Alias = "children")]
    public List<ManifestNode> Children { get; set; } = new();

    [YamlMember(Alias = "records")]
    public List<ManifestRecord> Records { get; set; } = new();
}

public class ManifestRecord
{
    [YamlMember(Alias = "type")]
    public string Type { get; set; } = string.Empty;

    [YamlMember(Alias = "typeId")]
    public int TypeId { get; set; }

    [YamlMember(Alias = "originalSize")]
    public int OriginalSize { get; set; }

    [YamlMember(Alias = "dataFile")]
    public string DataFile { get; set; } = string.Empty;

    [YamlMember(Alias = "sortIndex")]
    public int SortIndex { get; set; }
}
