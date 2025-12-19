using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace TorusTool.Models;

public class HunkFileTreeBuilder
{
    public static ObservableCollection<HunkFileTreeNode> BuildTree(IEnumerable<HunkRecord> records)
    {
        var rootNodes = new ObservableCollection<HunkFileTreeNode>();
        
        // Root folder wrapper or just flat list of folders?
        // Let's assume we want a hierarchy.
        
        HunkFileTreeNode? currentFileNode = null;
        
        // Determine if we need a "Root" node or multiple roots.
        // Hunk files seem to be flat lists of files which have "Folder" and "Filename" properties.
        
        // Strategy:
        // maintain a dictionary of paths to folder nodes.
        
        var folderDict = new Dictionary<string, HunkFileTreeNode>();

        // Helper to get or create folder path
        HunkFileTreeNode GetOrCreateFolder(string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath)) 
            {
                // This shouldn't really happen with the format, but maybe root?
                // Let's create a special root if needed, or return null to add to top level.
                // For now, let's treat empty folder as top level.
                 return null;
            }

            if (folderDict.TryGetValue(folderPath, out var node)) return node;

            // Split path? The example showed "TSETexture" as folder. 
            // If there are nested folders like "A/B", we need to handle that.
            // Assuming simple 1-level or full path strings for now.
            // Let's try to build hierarchical if slashes exist.
            
            var parts = folderPath.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
            HunkFileTreeNode? parent = null;
            string currentPath = "";
            
            foreach (var part in parts)
            {
                currentPath = string.IsNullOrEmpty(currentPath) ? part : currentPath + "/" + part;
                
                if (!folderDict.TryGetValue(currentPath, out var current))
                {
                    current = new HunkFileTreeNode 
                    { 
                        Name = part, 
                        FullPath = currentPath, 
                        IsFolder = true 
                    };
                    folderDict[currentPath] = current;
                    
                    if (parent == null)
                    {
                        rootNodes.Add(current);
                    }
                    else
                    {
                        parent.Children.Add(current);
                    }
                }
                parent = current;
            }
            return parent;
        }

        // Implicit "Uncategorized" file node for records before any header?
        // Or if the file doesn't start with a FilenameHeader (e.g. the Global header).
        // Let's create a "Header" file node for the initial chunks.
        
        var initialNode = new HunkFileTreeNode { Name = "Global Header", IsFolder = false };
        rootNodes.Add(initialNode);
        currentFileNode = initialNode;

        foreach (var record in records)
        {
            if (record.Type == HunkRecordType.FilenameHeader)
            {
                var parser = RecordParsers.ParseFilenameHeader(record);
                if (parser.HasValue)
                {
                    var folderNode = GetOrCreateFolder(parser.Value.Folder);
                    
                    var newFileNode = new HunkFileTreeNode 
                    { 
                        Name = parser.Value.Filename, 
                        IsFolder = false 
                    };
                    
                    if (folderNode != null)
                        folderNode.Children.Add(newFileNode);
                    else
                        rootNodes.Add(newFileNode);
                        
                    currentFileNode = newFileNode;
                    
                    // Add the header record itself to the new file
                    currentFileNode.Records.Add(record);
                    continue;
                }
            }
            
            // Add record to current file
            currentFileNode?.Records.Add(record);
        }

        return rootNodes;
    }
}
