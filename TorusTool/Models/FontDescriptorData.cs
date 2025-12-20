using System;
using System.Collections.Generic;

namespace TorusTool.Models;

public class FontDescriptorData
{
    public TSEPlatformHeader PlatformHeader { get; set; }
    public TSEFileHeaderLE FileHeader { get; set; }

    public List<TSEPreGlyphEntry> PreGlyphs { get; set; } = new();
    public List<TSEGlyphEntry> Glyphs { get; set; } = new();
    public List<TSECodepoint> Codepoints { get; set; } = new();
}

public struct TSEPlatformHeader
{
    public ushort VersionOrFlags { get; set; }
    public ushort LineHeight { get; set; }
    public short BBoxMinX { get; set; }
    public ushort Ascender { get; set; }
    public short BBoxMinY { get; set; }
    public ushort GlyphCount { get; set; }
    public ushort GlyphDataCount { get; set; }
    public ushort Reserved { get; set; }
}

public struct TSEFileHeaderLE
{
    public uint TableCount { get; set; }
    public uint PreGlyphOffset { get; set; }
    public uint GlyphTableOffset { get; set; }
    public uint Reserved0 { get; set; }
    public uint UnicodeOffset { get; set; }
}

public struct TSEPreGlyphEntry
{
    public ushort Field0 { get; set; }
    public ushort Field1 { get; set; }
    public short Field2 { get; set; }
    public short Field3 { get; set; }

    public string HexDisplay => $"{Field0:X4}-{Field1:X4}-{Field2:X4}-{Field3:X4}";
}

public struct TSEGlyphEntry
{
    public ushort GlyphIndex { get; set; } // to which glyph this element belongs
    public ushort ElementId { get; set; } // unique ID / primitive index
    public short Param1 { get; set; } // offsets/coordinates
    public short Param2 { get; set; } // offsets/flags

    public string Display => $"G:{GlyphIndex} E:{ElementId} P1:{Param1} P2:{Param2}";
}

public struct TSECodepoint
{
    public uint Code { get; set; }

    public string CharDisplay => $"{(char)Code} (0x{Code:X})";
}
