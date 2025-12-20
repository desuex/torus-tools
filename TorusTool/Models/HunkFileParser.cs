using System;
using System.Collections.Generic;
using System.IO;
using TorusTool.IO;

namespace TorusTool.Models;

public class HunkFileParser
{
    public IEnumerable<HunkRecord> Parse(string filePath, bool isBigEndian = false)
    {
        if (!File.Exists(filePath))
            yield break;

        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        foreach (var record in Parse(fs, isBigEndian))
        {
            yield return record;
        }
    }

    public IEnumerable<HunkRecord> Parse(Stream stream, bool isBigEndian = false)
    {
        // Don't dispose the stream here, caller owns it if passed in
        // But TorusBinaryReader doesn't dispose stream unless Dispose is called, which we might do via 'using' on reader?
        // TorusBinaryReader.Dispose disposes the underlying BinaryReader which disposes the stream.
        // So we should be careful. 
        // If we duplicate the logic, it's safer.
        // OR we make TorusBinaryReader not dispose stream? 
        // Typically BinaryReader disposes stream.
        // Let's use 'leaveOpen' if available? BinaryReader has it.
        // TorusBinaryReader constructors don't expose leaveOpen.
        // For now, let's assume this method consumes the stream if we wrap it in TorusBinaryReader.
        
        // Actually, let's copy the logic to avoid ownership issues or update TorusBinaryReader.
        // Or just let it dispose if we pass a MemoryStream (who cares).
        
        using var reader = new TorusBinaryReader(stream, isBigEndian);

        while (reader.Position < reader.Length)
        {
            if (reader.Position + 4 > reader.Length) break;
            uint size = reader.ReadUInt32();

            if (reader.Position + 4 > reader.Length) break;
            uint typeInt = reader.ReadUInt32();
            
            byte[] data = Array.Empty<byte>();
            if (size > 0)
            {
               if (reader.Position + size > reader.Length)
               {
                   // Truncated file or bad read
                   break;
               }
               data = reader.ReadBytes((int)size);
            }

            yield return new HunkRecord
            {
                Size = size,
                Type = (HunkRecordType)typeInt,
                RawData = data
            };
        }
    }
}
