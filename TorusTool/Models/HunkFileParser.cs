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
        using var reader = new TorusBinaryReader(fs, isBigEndian);

        while (reader.Position < reader.Length)
        {
            // The python code reads:
            // int1 = fp.read(4) (Size)
            // int2 = fp.read(4) (Type)
            // data = fp.read(size)

            if (reader.Position + 4 > reader.Length) break;
            uint size = reader.ReadUInt32();

            if (reader.Position + 4 > reader.Length) break;
            uint typeInt = reader.ReadUInt32();
            
            // Validate size to avoid huge allocations on bad reads? Python didn't seem to care but for safety:
            // IF size is massive relative to file remainder, it might be corrupt, but we'll trust the format for now.
            // Note: size can be 0.
            
            byte[] data = Array.Empty<byte>();
            if (size > 0)
            {
               if (reader.Position + size > reader.Length)
               {
                   // Truncated file or bad read
                   break;
               }
               // ReadBytes is simple read.
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
