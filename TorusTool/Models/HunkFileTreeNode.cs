using System.Collections.ObjectModel;

namespace TorusTool.Models;

public class HunkFileTreeNode
{
    public string Name { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public bool IsFolder { get; set; }
    public ObservableCollection<HunkFileTreeNode> Children { get; set; } = new();
    public ObservableCollection<HunkRecord> Records { get; set; } = new(); // Only for leaf nodes (Files)

    // For binding convenience
    public string Icon => IsFolder ? "📁" : "📄";
    
    // For Packfile support
    public PackfileEntry? PackEntry { get; set; }
}
