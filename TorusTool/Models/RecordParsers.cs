using System;
using System.Text;
using System.IO;
using TorusTool.IO;
using System.Collections.Generic;
using System.Buffers.Binary;
using System.Linq;

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

        // FilenameHeader is usually Little Endian even on Big Endian consoles (Wii/Xbox/PS3?)
        // The container (chunk sizes) might follow platform rules (or not?), but the internal data for this specific record seems LE.
        // We'll try reading with the passed endianness first? No, let's try to detect or force LE.
        // Observed: 28 00 00 00 (40) -> LE.
        // If we read as BE: 00 00 00 28 -> Huge.
        // Let's force LE for this specific record type's parsing logic.
        using var reader = new TorusBinaryReader(record.RawData, false); // Force Little Endian

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

        // These fields appear to be Little Endian on both PC and PS3 (Mixed Endian Header)
        // So we bypass the Endian-aware reader for them.
        uint offsetStart = BitConverter.ToUInt32(reader.ReadBytes(4));
        uint hashStart = BitConverter.ToUInt32(reader.ReadBytes(4));

        uint v5 = reader.ReadUInt32(); // Unknown endianness, usually 0
        uint v6 = reader.ReadUInt32(); // Unknown

        var table = new StringTableData
        {
            Size = (int)record.Size,
            Count = (int)count
        };

        if (count == 0) return table;

        // StoreOffsets
        var offsets = new uint[count];
        reader.Seek(offsetStart, SeekOrigin.Begin);
        for (int i = 0; i < count; i++) offsets[i] = reader.ReadUInt32();

        // Store Hashes
        var hashes = new uint[count];
        reader.Seek(hashStart, SeekOrigin.Begin);
        for (int i = 0; i < count; i++) hashes[i] = reader.ReadUInt32();

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

        // 1. Platform Header (Follows file endianness)
        fd.PlatformHeader = new TSEPlatformHeader
        {
            FlagsOrVersion = reader.ReadUInt16(),
            EmOrLineHeight = reader.ReadUInt16(),
            GlobalMinX = reader.ReadInt16(),
            SomethingSize = reader.ReadUInt16(),
            GlobalMinY = reader.ReadInt16(),
            GlyphCount = reader.ReadUInt16(),
            GlyphDataCount = reader.ReadUInt16(),
            Unk7 = reader.ReadUInt16()
        };

        // 2. Main LE Header (Always Little Endian)
        // We need a reader that forces Little Endian.
        // If the original reader is BE, we need a new one or manual toggle.
        // TorusBinaryReader doesn't seem to support switching on the fly easily, 
        // so we'll instantiate a new one with isBigEndian=false.
        // We must preserve position. Current pos is 16.

        using var leReader = new TorusBinaryReader(record.RawData, false); // Force LE
        leReader.Seek(16, SeekOrigin.Begin);

        fd.FileHeader = new TSEFileHeaderLE
        {
            TableCount = leReader.ReadUInt32(),
            PreGlyphOffset = leReader.ReadUInt32(),
            GlyphTableOffset = leReader.ReadUInt32(),
            Reserved0 = leReader.ReadUInt32(),
            UnicodeOffset = leReader.ReadUInt32()
        };

        // 3. Tables

        // table 1: PreGlyphs
        if (fd.FileHeader.PreGlyphOffset > 0 && fd.FileHeader.PreGlyphOffset < record.RawData.Length)
        {
            leReader.Seek(fd.FileHeader.PreGlyphOffset, SeekOrigin.Begin);
            for (int i = 0; i < fd.PlatformHeader.GlyphCount; i++)
            {
                fd.PreGlyphs.Add(new TSEPreGlyphEntry
                {
                    V0 = leReader.ReadUInt16(),
                    V1 = leReader.ReadUInt16(),
                    V2 = leReader.ReadUInt16(),
                    V3 = leReader.ReadUInt16()
                });
            }
        }

        // table 2: Glyphs
        if (fd.FileHeader.GlyphTableOffset > 0 && fd.FileHeader.GlyphTableOffset < record.RawData.Length)
        {
            leReader.Seek(fd.FileHeader.GlyphTableOffset, SeekOrigin.Begin);
            for (int i = 0; i < fd.PlatformHeader.GlyphDataCount; i++)
            {
                fd.Glyphs.Add(new TSEGlyphEntry
                {
                    A = leReader.ReadInt16(),
                    B = leReader.ReadInt16(),
                    C = leReader.ReadInt16(),
                    D = leReader.ReadInt16()
                });
            }
        }

        // table 3: Codepoints
        if (fd.FileHeader.UnicodeOffset > 0 && fd.FileHeader.UnicodeOffset < record.RawData.Length)
        {
            leReader.Seek(fd.FileHeader.UnicodeOffset, SeekOrigin.Begin);
            for (int i = 0; i < fd.PlatformHeader.GlyphCount; i++)
            {
                fd.Codepoints.Add(new TSECodepoint
                {
                    Code = leReader.ReadUInt32()
                });
            }
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
            int nextOffset = (i + 1 < rs.Count) ? rs.Offsets[i + 1] : (record.RawData.Length);

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
            catch { }

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
        else if (b0 == 0x9F && b1 == 0xBC) tex.Format = "DXT5"; // PS3 DXT5 variant/endian-swap
        else if (b0 == 0x6F && b1 == 0x74) tex.Format = "R8G8B8A8";
        else if (b0 == 0x21 && b1 == 0x71) tex.Format = "3DS_L8"; // PICA200 L8 Swizzled
        else tex.Format = "DXT1";

        return tex;
    }

    public class DataTableData
    {
        public int Count;
        public int Unknown1;
        public int DataSize;
        public int TypeCode;
        public byte[] Body = Array.Empty<byte>();
        public List<string> StringValues = new();
        public List<byte[]> ElementRawData = new();
    }

    public static DataTableData? ParseDataTable(HunkRecord record, bool isBigEndian = false)
    {
        // Support both Data1 and Data2 as potential table containers
        if (record.Type != HunkRecordType.TSEDataTableData1 && record.Type != HunkRecordType.TSEDataTableData2) return null;
        if (record.RawData.Length < 28) return null;

        var dt = new DataTableData();
        dt.TypeCode = (int)record.Type;

        using var stream = new MemoryStream(record.RawData);
        using var reader = new TorusBinaryReader(stream, isBigEndian);

        // Header parsing strategies
        int headerSize = 28; // Default for Data1

        if (record.Type == HunkRecordType.TSEDataTableData2)
        {
            // Data2 seems to have variable header size or Offset to Data at 0x08
            // [Unk1 4b] [Count 4b] [DataOffset 4b] ...
            stream.Position = 0;
            dt.Unknown1 = reader.ReadInt32();
            dt.Count = reader.ReadInt32(); // Can be mismatch/negative?

            int dataOffset = reader.ReadInt32();

            // Heuristic for validity of DataOffset
            if (dataOffset > 0 && dataOffset < record.RawData.Length)
            {
                headerSize = dataOffset;
            }

            // Log weird counts
            // e.g. FFFF0008. If we treat as short?
            // Actually, for display purposes, we just trust the Int32 for now,
            // or handle the negative case in the splitter.
        }
        else // HunkRecordType.TSEDataTableData1
        {
            // Data1 Standard Header (28 bytes)
            dt.Unknown1 = reader.ReadInt32();
            dt.Count = reader.ReadInt32();
            int unk2 = reader.ReadInt32();
            dt.DataSize = reader.ReadInt32();
            int unk3 = reader.ReadInt32();
            int unk4 = reader.ReadInt32();
            // dt.TypeCode is already set from record.Type
            reader.ReadInt32(); // Consume the TypeCode from stream
        }

        // Read Body
        // The reader's position is now at the end of the header for Data1,
        // or after the initial 12 bytes for Data2.
        // We need to read the body starting from 'headerSize'.
        // The current reader position might not be at 'headerSize' for Data2 if dataOffset was used.

        reader.Seek(headerSize, SeekOrigin.Begin);

        int bodyLen = record.RawData.Length - headerSize;
        if (bodyLen > 0)
        {
            dt.Body = reader.ReadBytes(bodyLen);

            // Structured Parsing: Read 'Count' strings sequentially
            // We'll try to parse the body as a sequence of null-terminated strings.
            // If we fail (garbage characters), we assume it's a binary table.

            try
            {
                using var bodyReader = new TorusBinaryReader(dt.Body, isBigEndian);
                var tempList = new List<string>();
                bool isStringTable = true;

                for (int i = 0; i < dt.Count; i++)
                {
                    // Peek or read? We need to read until null.
                    // Max string length safety?
                    var startPos = bodyReader.Position;
                    if (startPos >= dt.Body.Length)
                    {
                        // Unexpected EOF before Count reached
                        isStringTable = false;
                        break;
                    }

                    // Read chars until null
                    var sb = new StringBuilder();
                    bool nullFound = false;
                    while (bodyReader.Position < bodyReader.Length - 1)
                    {
                        ushort val = bodyReader.ReadUInt16(); // Strings are UTF-16 LE
                        // Endianness is handled by TorusBinaryReader internally now.
                        // if (isBigEndian) val = BinaryPrimitives.ReverseEndianness(val); 

                        if (val == 0)
                        {
                            nullFound = true;
                            break;
                        }

                        char c = (char)val;
                        // Basic validation: Is it a valid text char?
                        if (char.IsControl(c) && c != '\t' && c != '\r' && c != '\n')
                        {
                            // Allow strict subset? 
                            // Binary data often has low control chars (0x01, 0x02).
                            // Real strings usually don't.
                            // But let's be lenient for now, or strict if we want to distinguish binary.
                            // GhoulHairstyles has 01 00 ... so that's char(1). IsControl(1) is true.
                            isStringTable = false;
                            break;
                        }
                        sb.Append(c);
                    }

                    if (!isStringTable) break;

                    if (!nullFound)
                    {
                        // End of stream without null
                        isStringTable = false;
                        break;
                    }

                    tempList.Add(sb.ToString());
                }

                // Validation: Did we consume enough of the body?
                // Binary tables might accidentally look like empty strings (00 00).
                // If we found 'Count' strings but only used 6 bytes out of 66, it's not a string table.
                long consumed = bodyReader.Position;
                if (consumed < dt.Body.Length * 0.8) // Threshold: 80%? 90%?
                {
                    // Exception: Start of file padding? Unlikely for DataTable.
                    // Let's require at least 90% or strictly "near end".
                    // Actually, if we stopped early, it means the rest of the bytes are unused?
                    // In Hunk files, everything is usually packed.
                    isStringTable = false;
                }

                if (isStringTable && tempList.Count == dt.Count)
                {
                    dt.StringValues = tempList;
                }
                else
                {
                    // Fallback: Binary Table?
                    // Try to split by count
                    // Handle Data2 quirks: Negative count means ??
                    // If Count is huge/negative, we can't split.

                    int validCount = dt.Count;
                    if (validCount < 0) validCount = 0; // Or treat as ushort?

                    if (validCount > 0 && dt.Body.Length > 0 && (dt.Body.Length % validCount == 0))
                    {
                        int stride = dt.Body.Length / validCount;
                        dt.ElementRawData = new List<byte[]>();
                        for (int i = 0; i < validCount; i++)
                        {
                            var chunk = new byte[stride];
                            Array.Copy(dt.Body, i * stride, chunk, 0, stride);
                            dt.ElementRawData.Add(chunk);
                        }
                    }
                    else if (dt.Body.Length > 0)
                    {
                        // Can't split evenly. Just add whole body as one element?
                        // Or if we suspected Data2 has 8-byte entries?
                        // Heuristic for Data2: 8-byte blocks?
                        if (dt.TypeCode == (int)HunkRecordType.TSEDataTableData2 && dt.Body.Length % 8 == 0)
                        {
                            // Force 8-byte split logic if structure looks like map entries
                            int stride = 8;
                            int count = dt.Body.Length / stride;
                            dt.ElementRawData = new List<byte[]>();
                            for (int i = 0; i < count; i++)
                            {
                                var chunk = new byte[stride];
                                Array.Copy(dt.Body, i * stride, chunk, 0, stride);
                                dt.ElementRawData.Add(chunk);
                            }
                        }
                    }
                }
            }
            catch { }
        }

        return dt;
    }
}
