using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TorusTool.IO;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace TorusTool.Models;

public class HunkImporter
{
    public void Import(string manifestPath, string outputHnkPath)
    {
        if (!File.Exists(manifestPath)) throw new FileNotFoundException("Manifest not found", manifestPath);

        string rootDir = Path.GetDirectoryName(manifestPath) ?? string.Empty;
        string yaml = File.ReadAllText(manifestPath);

        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        var manifest = deserializer.Deserialize<HunkManifest>(yaml);

        // Collect all records
        var allRecords = new List<HunkManifestRecord>();
        CollectRecords(manifest.RootNodes, allRecords);

        // Sort by index to preserve original order
        var sortedRecords = allRecords.OrderBy(x => x.SortIndex).ToList();

        using var fs = new FileStream(outputHnkPath, FileMode.Create, FileAccess.Write);
        using var writer = new TorusBinaryWriter(fs, manifest.IsBigEndian);

        foreach (var record in sortedRecords)
        {
            string binPath = Path.Combine(rootDir, record.DataFile);
            if (!File.Exists(binPath))
            {
                Console.WriteLine($"Warning: Data file not found: {binPath}. Skipping record.");
                continue;
            }

            byte[] data = File.ReadAllBytes(binPath);
            uint size = (uint)data.Length; // Recalculate size

            writer.Write(size);
            writer.Write(record.TypeId);
            if (size > 0)
            {
                writer.Write(data);
            }
        }
    }

    private void CollectRecords(List<HunkManifestNode> nodes, List<HunkManifestRecord> accumulator)
    {
        foreach (var node in nodes)
        {
            if (node.IsFolder)
            {
                CollectRecords(node.Children, accumulator);
            }
            else
            {
                accumulator.AddRange(node.Records);
            }
        }
    }
}
