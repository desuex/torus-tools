using System;
using System.Collections.Generic;
using System.IO;
using TorusTool.Models;

namespace TorusTool.IO;

public class PackfileWriter
{
    public static void Repack(string inputDirectory, string outputPackfile)
    {
        throw new NotImplementedException("Repacking is not yet supported for the Monster Jam packfile format due to its complexity (linked lists, pointer tables).");
    }
}

public static class PackfileWriterExtensions
{
     public static void UnpackAll(string packPath, string outputDir, Action<string, int, int>? progressCallback = null)
    {
        var pack = PackfileReader.Read(packPath);
        int total = pack.Entries.Count;
        int current = 0;

        Directory.CreateDirectory(outputDir);

        foreach (var entry in pack.Entries)
        {
            current++;
            progressCallback?.Invoke(entry.DisplayName, current, total);
            
            byte[] data = PackfileReader.ExtractFile(packPath, entry);
            
            // Generate valid output path
            string relativePath = entry.DisplayName;
            // entry.DisplayName is now FullPath (e.g. pc/globals/physics/file.ext)
            
            string outPath = Path.Combine(outputDir, relativePath);
            
            // Safe guard against traversal
            // Ensure outPath is within outputDir
            // (Assuming entry.FullPath does not contain ..)
            
            string? dir = Path.GetDirectoryName(outPath);
            if (dir != null) Directory.CreateDirectory(dir);
            
            File.WriteAllBytes(outPath, data);
        }
    }
}
