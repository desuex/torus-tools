using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace TorusTool.Models;

public class HunkExporter
{
    private int _globalSortIndex = 0;

    public void Export(string hnkPath, string outputDir, string gameName = "Unknown", string platform = "Unknown")
    {
        if (!File.Exists(hnkPath)) throw new FileNotFoundException("HNK file not found", hnkPath);

        // Compute SHA1
        string sha1Hash;
        using (var fs = new FileStream(hnkPath, FileMode.Open, FileAccess.Read))
        using (var sha1 = System.Security.Cryptography.SHA1.Create())
        {
            var hashBytes = sha1.ComputeHash(fs);
            sha1Hash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
        }

        // Ensure output directory exists
        Directory.CreateDirectory(outputDir);
        string dataDir = Path.Combine(outputDir, "data");
        Directory.CreateDirectory(dataDir);

        // Platform detection for metadata ONLY
        bool isPlatformBigEndian = hnkPath.Contains("PS3") || hnkPath.Contains("WII");

        // HNK Container is ALWAYS Little Endian (based on verification findings)
        // Only internal content (textures, tables) respects platform endianness.
        var parser = new HunkFileParser();
        var records = parser.Parse(hnkPath, false).ToList(); // Force LE
        var tree = HunkFileTreeBuilder.BuildTree(records);

        var manifest = new HunkManifest
        {
            IsBigEndian = false, // Container is LE.
            GameName = gameName,
            OriginalFileName = Path.GetFileName(hnkPath),
            Platform = platform,
            OriginalFileHash = sha1Hash,
            RootNodes = ConvertNodes(tree, dataDir, "")
        };

        var serializer = new SerializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        string yaml = serializer.Serialize(manifest);
        File.WriteAllText(Path.Combine(outputDir, "manifest.yaml"), yaml);
    }

    private List<HunkManifestNode> ConvertNodes(IEnumerable<HunkFileTreeNode> nodes, string rootDataDir, string currentRelativePath)
    {
        var list = new List<HunkManifestNode>();
        foreach (var node in nodes)
        {
            var manifestNode = new HunkManifestNode
            {
                Name = node.Name,
                IsFolder = node.IsFolder
            };

            // Build path for this node
            string safeName = string.Join("_", node.Name.Split(Path.GetInvalidFileNameChars()));
            string nextRelativePath = string.IsNullOrEmpty(currentRelativePath)
                ? safeName
                : Path.Combine(currentRelativePath, safeName);

            if (node.IsFolder)
            {
                // Recursive call for children
                manifestNode.Children = ConvertNodes(node.Children, rootDataDir, nextRelativePath);
            }
            else
            {
                // It's a file, make a directory for it to hold its chunks
                string fileDir = Path.Combine(rootDataDir, nextRelativePath);

                // If the path components don't exist, create them
                Directory.CreateDirectory(fileDir);

                for (int i = 0; i < node.Records.Count; i++)
                {
                    var record = node.Records[i];

                    // Naming strategy: {RecordType}_{Index}_{GUID}.bin
                    string filename = $"{record.Type}_{i}_{Guid.NewGuid().ToString("N").Substring(0, 8)}.bin";
                    string fullPath = Path.Combine(fileDir, filename);

                    File.WriteAllBytes(fullPath, record.RawData);

                    string relativeDataPath = Path.Combine("data", nextRelativePath, filename);

                    manifestNode.Records.Add(new HunkManifestRecord
                    {
                        Type = record.Type.ToString(),
                        TypeId = (uint)record.Type,
                        OriginalSize = record.Size,
                        DataFile = relativeDataPath,
                        SortIndex = _globalSortIndex++
                    });
                }
            }
            list.Add(manifestNode);
        }
        return list;
    }
}
