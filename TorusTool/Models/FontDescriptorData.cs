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
    public ushort FlagsOrVersion { get; set; }
    public ushort EmOrLineHeight { get; set; }
    public short GlobalMinX { get; set; }
    public ushort SomethingSize { get; set; }
    public short GlobalMinY { get; set; }
    public ushort GlyphCount { get; set; }
    public ushort GlyphDataCount { get; set; }
    public ushort Unk7 { get; set; }
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
    public ushort V0 { get; set; }
    public ushort V1 { get; set; }
    public ushort V2 { get; set; }
    public ushort V3 { get; set; }

    public string HexDisplay => $"{V0:X4}-{V1:X4}-{V2:X4}-{V3:X4}";
}

public struct TSEGlyphEntry
{
    public short A { get; set; }
    public short B { get; set; }
    public short C { get; set; }
    public short D { get; set; }

    public string ShortsDisplay => $"{A}, {B}, {C}, {D}";
}

public struct TSECodepoint
{
    public uint Code { get; set; }

    public string CharDisplay => $"{(char)Code} (0x{Code:X})";
}
