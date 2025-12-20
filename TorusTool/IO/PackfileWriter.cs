using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using TorusTool.Models;

namespace TorusTool.IO;

public class PackfileWriter
{
    private const uint MAGIC_PAK = 0x004B4150; // "PAK\0"

    public static void Repack(string inputDirectory, string outputPackfile)
    {
        var files = Directory.GetFiles(inputDirectory);
        var entries = new List<PackfileEntry>();
        var blobStream = new MemoryStream();

        // 1. Process files
        foreach (var file in files)
        {
            var fileName = Path.GetFileName(file);
            // Parse filename to get CRC or original name.
            // Format: Index_CRC.ext or just CRC.ext?
            // "00000000_A1B2C3D4.hnk"
            
            uint crc = 0;
            var match = Regex.Match(fileName, @"^(\d+_)?([0-9a-fA-F]{8})");
            if (match.Success)
            {
                // Group 2 is the CRC
                string crcStr = match.Groups[2].Value;
                crc = Convert.ToUInt32(crcStr, 16);
            }
            else
            {
                // Fallback: Compute CRC32 of filename?
                // Or just error?
                // The BMS script implies files are identified by CRC.
                // If the user adds a NEW file, we must compute a CRC.
                // Assuming standard CRC32 for now as fallback.
                crc = ForceCrc32(Path.GetFileNameWithoutExtension(fileName));
            }

            var entry = new PackfileEntry
            {
                NameCrc = crc
            };
            
            // Read and Compress
            byte[] rawData = File.ReadAllBytes(file);
            byte[] processedData = CompressZLS(rawData);

            entry.Size = (uint)processedData.Length;
            entry.Offset = 0; // Set later
            
            // Write to blob
            // Align? BMS script didn't check alignment.
            long currentPos = blobStream.Position;
            entry.Offset = (uint)currentPos; // Relative to the blob start?
            // Wait, Offset in PAK is absolute from file start.
            // We need to calculate header size first.
            
            blobStream.Write(processedData, 0, processedData.Length);
            
            entries.Add(entry);
        }

        // 2. Calculate Offsets
        // Header: Magic (4) + Count (4) = 8 bytes
        // Entry: CRC (4) + Offset (4) + Size (4) = 12 bytes
        uint headerSize = 8 + (uint)(entries.Count * 12);
        
        // Adjust offsets
        foreach (var e in entries)
        {
            e.Offset += headerSize;
        }
        
        // 3. Write File
        using var fs = new FileStream(outputPackfile, FileMode.Create, FileAccess.Write);
        using var writer = new BinaryWriter(fs); // LE by default
        
        writer.Write(MAGIC_PAK);
        writer.Write((uint)entries.Count);
        
        foreach (var e in entries)
        {
            writer.Write(e.NameCrc);
            writer.Write(e.Offset);
            writer.Write(e.Size);
        }
        
        blobStream.Position = 0;
        blobStream.CopyTo(fs);
    }
    
    private static byte[] CompressZLS(byte[] data)
    {
        // !ZLS (4) + UncompressedSize (4) + ZLibData
        using var ms = new MemoryStream();
        // Write header
        ms.Write(System.Text.Encoding.ASCII.GetBytes("!ZLS"));
        ms.Write(BitConverter.GetBytes(data.Length)); // LE size
        
        // Compress
        using (var zlib = new ZLibStream(ms, CompressionLevel.Optimal, leaveOpen: true))
        {
            zlib.Write(data, 0, data.Length);
        }
        
        return ms.ToArray();
    }
    
    private static uint ForceCrc32(string str)
    {
        // Simple Crc32 implementation references System.IO.Hashing in newer .NET or check if available
        // Since we are .NET 9, we can use System.IO.Hashing.Crc32?
        // Check project file. It has Avalonia etc.
        // If not available, implement simple lookup table.
        // But the user might be adding files with arbitrary names.
        
        // Actually, let's assume simple implementation for now.
        return Crc32Computer.Compute(System.Text.Encoding.ASCII.GetBytes(str));
    }
}

public static class Crc32Computer
{
    private static readonly uint[] Table;
    
    static Crc32Computer()
    {
        uint poly = 0xedb88320;
        Table = new uint[256];
        for (uint i = 0; i < 256; i++)
        {
            uint crc = i;
            for (int j = 8; j > 0; j--)
            {
               if ((crc & 1) == 1)
                  crc = (crc >> 1) ^ poly;
               else
                  crc >>= 1;
            }
            Table[i] = crc;
        }
    }
    
    public static uint Compute(byte[] bytes)
    {
        uint crc = 0xffffffff;
        foreach (byte b in bytes)
        {
            byte tableIndex = (byte)(((crc) & 0xff) ^ b);
            crc = Table[tableIndex] ^ (crc >> 8);
        }
        return ~crc;
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
            string filename = entry.DisplayName; 
            
            string outPath = Path.Combine(outputDir, filename);
            File.WriteAllBytes(outPath, data);
        }
    }
}
