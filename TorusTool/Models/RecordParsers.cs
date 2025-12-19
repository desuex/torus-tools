using System;
using System.Text;
using System.IO;
using TorusTool.IO;

namespace TorusTool.Models;

public static class RecordParsers
{
    public struct FileHeaderData
    {
        public short X2;
        public short DataType;
        public short Parts;
        public short FolderLen;
        public short FilenameLen;
        public string Folder;
        public string Filename;
    }

    public static FileHeaderData? ParseFilenameHeader(HunkRecord record, bool isBigEndian = false)
    {
        if (record.Type != HunkRecordType.FilenameHeader) return null;
        if (record.RawData.Length < 10) return null; 

        using var reader = new TorusBinaryReader(record.RawData, isBigEndian);
        
        short v0 = reader.ReadInt16();
        short v1 = reader.ReadInt16();
        short v2 = reader.ReadInt16();
        short folderLen = reader.ReadInt16();
        short filenameLen = reader.ReadInt16();

        if (reader.Position + folderLen > record.RawData.Length) return null;
        string folder = reader.ReadStringFixed(folderLen);
        
        if (reader.Position + filenameLen > record.RawData.Length) return null;
        string filename = reader.ReadStringFixed(filenameLen);

        return new FileHeaderData
        {
            X2 = v0,
            DataType = v1,
            Parts = v2,
            FolderLen = folderLen,
            FilenameLen = filenameLen,
            Folder = folder,
            Filename = filename
        };
    }

    public static StringTableData? ParseStringTable(HunkRecord record, bool isBigEndian = false)
    {
        if (record.Type != HunkRecordType.TSEStringTableMain) return null;
        if (record.RawData.Length < 28) return null;
        
        using var reader = new TorusBinaryReader(record.RawData, isBigEndian);

        uint v0 = reader.ReadUInt32();
        uint v1 = reader.ReadUInt32();
        uint count = reader.ReadUInt32();
        uint offsetStart = reader.ReadUInt32();
        uint hashStart = reader.ReadUInt32();
        uint v5 = reader.ReadUInt32();
        uint v6 = reader.ReadUInt32();

        var table = new StringTableData
        {
            Size = (int)record.Size,
            Count = (int)count
        };

        if (count == 0) return table;
        
        // StoreOffsets
        var offsets = new uint[count];
        reader.Seek(offsetStart, SeekOrigin.Begin);
        for(int i=0; i<count; i++) offsets[i] = reader.ReadUInt32();
        
        // Store Hashes
        var hashes = new uint[count];
        reader.Seek(hashStart, SeekOrigin.Begin);
        for(int i=0; i<count; i++) hashes[i] = reader.ReadUInt32();
        
        // Read Strings
        for (int i = 0; i < count; i++)
        {
             long strOffset = offsets[i];
             if (strOffset >= record.RawData.Length) continue;
             
             reader.Seek(strOffset, SeekOrigin.Begin);
             
             // manual read until null
             var sb = new StringBuilder();
             while (reader.Position < reader.Length)
             {
                 byte b = reader.ReadByte();
                 if (b == 0) break;
                 sb.Append((char)b); 
             }
             
             table.Rows.Add(new StringTableRow 
             {
                 ID = i,
                 Offset = (int)strOffset,
                 Hash = (int)hashes[i],
                 Value = sb.ToString()
             });
        }

        return table;
    }

    public static HunkHeaderData? ParseHunkHeader(HunkRecord record, bool isBigEndian = false)
    {
        if (record.Type != HunkRecordType.Header) return null;
        if (record.RawData.Length < 592) return null;

        using var reader = new TorusBinaryReader(record.RawData, isBigEndian);
        var header = new HunkHeaderData();

        for (int i = 0; i < 8; i++)
        {
            header.Mysteries[i] = reader.ReadInt16();
        }

        // Start offset = 16. Reader is already there (8 * 2 = 16).
        
        for (int i = 0; i < 8; i++)
        {
            string name = reader.ReadStringFixed(64);
            int maxSize = reader.ReadInt32();
            int type = reader.ReadInt32();

            header.Rows.Add(new HunkHeaderRow
            {
                Name = name,
                MaxSize = maxSize,
                Type = type
            });
        }

        return header;
    }
    
    public static FontDescriptorData? ParseFontDescriptor(HunkRecord record, bool isBigEndian = false)
    {
        if (record.Type != HunkRecordType.TSEFontDescriptorData) return null;
        if (record.RawData.Length < 36) return null;
        
        using var reader = new TorusBinaryReader(record.RawData, isBigEndian);
        var fd = new FontDescriptorData();
        
        fd.Header.Z = reader.ReadInt16();
        fd.Header.Q1 = reader.ReadUInt16();
        fd.Header.Horizontal = reader.ReadInt16();
        fd.Header.Q3 = reader.ReadUInt16();
        fd.Header.Vertical = reader.ReadInt16();
        
        fd.Header.Cnt1 = reader.ReadUInt16();
        fd.Header.Cnt2 = reader.ReadUInt16();
        fd.Header.Z1 = reader.ReadUInt16();
        
        fd.Header.Signature = reader.ReadBytes(8);
        
        fd.Header.Offset1 = reader.ReadUInt16();
        fd.Header.Z2 = reader.ReadUInt16();
        fd.Header.Z3 = reader.ReadUInt16();
        fd.Header.Z4 = reader.ReadUInt16();
        fd.Header.Offset2 = reader.ReadUInt16();
        fd.Header.Z5 = reader.ReadUInt16();
        
        int dataStartOffset = fd.Header.Signature[4]; 
        if (dataStartOffset < 36) dataStartOffset = 36;
        
        reader.Seek(dataStartOffset, SeekOrigin.Begin);

        // Read Rows (cnt1)
        for (int i = 0; i < fd.Header.Cnt1; i++)
        {
            var row = new GlyphMetrics();
            row.Data = reader.ReadBytes(8); 
            fd.Rows.Add(row);
        }
        
        // Read Extras (cnt2)
        for (int i = 0; i < fd.Header.Cnt2; i++)
        {
             var extra = new FontExtra();
             extra.Data = reader.ReadBytes(8);
             fd.Extras.Add(extra);
        }

        // Read Tuples (cnt1) - These are UShorts, so Endianness matters!
        for (int i = 0; i < fd.Header.Cnt1; i++)
        {
             var tuple = new FontTuple();
             tuple.CharId = reader.ReadUInt16();
             tuple.Zero = reader.ReadUInt16();
             fd.Tuples.Add(tuple);
        }

        return fd;
    }

    public static RenderSpriteData? ParseRenderSprite(HunkRecord record, bool isBigEndian = false)
    {
        if (record.Type != HunkRecordType.TSERenderSprite) return null;
        if (record.RawData.Length < 16) return null;
        
        using var reader = new TorusBinaryReader(record.RawData, isBigEndian);
        
        var rs = new RenderSpriteData();
        rs.Count = reader.ReadInt32();
        rs.Unknown1 = reader.ReadInt32();
        rs.Unknown2 = reader.ReadInt32();
        rs.DataSize = reader.ReadInt32();
        
        for (int i = 0; i < rs.Count; i++) rs.Offsets.Add(reader.ReadInt32());
        
        // Read Items
        for (int i = 0; i < rs.Count; i++)
        {
            int itemOffset = rs.Offsets[i];
            int nextOffset = (i + 1 < rs.Count) ? rs.Offsets[i+1] : (record.RawData.Length);
            
            if (itemOffset < 0 || itemOffset >= record.RawData.Length) continue;
            
            int len = nextOffset - itemOffset;
            if (len <= 0) len = 64; 
            if (itemOffset + len > record.RawData.Length) len = record.RawData.Length - itemOffset;
            
            var item = new RenderSpriteItem { Index = i, Offset = itemOffset };
            item.Data = new byte[len];
            
            // Read raw bytes for the item data
            reader.Seek(itemOffset, SeekOrigin.Begin);
            item.Data = reader.ReadBytes(len); 
            
            // Attempt to parse points from the item buffer
            // We need a temporary reader for this buffer to handle endianness of floats
            using var itemReader = new TorusBinaryReader(item.Data, isBigEndian);
            
            try 
            {
                // Assuming format: Float X, Float Y...
                while (itemReader.Position + 8 <= itemReader.Length)
                {
                    float x = itemReader.ReadSingle();
                    float y = itemReader.ReadSingle();
                    
                    if (x > -10000 && x < 10000 && y > -10000 && y < 10000) 
                    {
                         item.Points.Add(new Avalonia.Point(x, y));
                    }
                }
            }
            catch {}

            rs.Items.Add(item);
        }
        
        return rs;
    }

    public static TextureHeader? ParseTextureHeader(HunkRecord record, bool isBigEndian = false)
    {
        if (record.Type != HunkRecordType.TSETextureHeader) return null;
        if (record.RawData.Length < 0x10) return null;
        
        using var reader = new TorusBinaryReader(record.RawData, isBigEndian);
        
        // Format detection usually relies on raw bytes at 0x0?
        reader.Seek(0x0C, SeekOrigin.Begin);
        ushort w = reader.ReadUInt16();
        ushort h = reader.ReadUInt16();
        
        var tex = new TextureHeader { Width = w, Height = h };
        
        // Marker check at 0
        reader.Seek(0, SeekOrigin.Begin);
        byte b0 = reader.ReadByte();
        byte b1 = reader.ReadByte();
        
        if (b0 == 0xF9 && b1 == 0x3D) tex.Format = "DXT1";
        else if (b0 == 0xD3 && b1 == 0x3A) tex.Format = "DXT5";
        else if (b0 == 0x6F && b1 == 0x74) tex.Format = "R8G8B8A8";
        else tex.Format = "DXT1";
        
        return tex;
    }
}
