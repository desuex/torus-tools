using System;
using System.Collections.Generic;

namespace TorusTool.Models;

public class FontDescriptorData
{
    public FontDescriptorHeader Header { get; set; } = new();
    public List<GlyphMetrics> Rows { get; set; } = new();
    public List<FontExtra> Extras { get; set; } = new();
    public List<FontTuple> Tuples { get; set; } = new();
}

public class FontDescriptorHeader
{
    public short Z { get; set; }
    public ushort Q1 { get; set; }
    public short Horizontal { get; set; } // Horizontal spacing/shift?
    public ushort Q3 { get; set; }
    public short Vertical { get; set; } // Vertical shift

    public ushort Cnt1 { get; set; }
    public ushort Cnt2 { get; set; }
    public ushort Z1 { get; set; }

    public byte[] Signature { get; set; } = new byte[8];

    public ushort Offset1 { get; set; }
    public ushort Z2 { get; set; }
    public ushort Z3 { get; set; }
    public ushort Z4 { get; set; }
    public ushort Offset2 { get; set; }
    public ushort Z5 { get; set; }
}

public class GlyphMetrics
{
    // Replaced raw bytes with explicit shorts for Endian support
    public ushort Value1 { get; set; }
    public ushort Value2 { get; set; }
    public short Width { get; set; }
    public short Aux { get; set; }

    public string HexDisplay => $"{Value1:X4}-{Value2:X4}-{Width:X4}-{Aux:X4}";
    public string ShortsDisplay => $"{Value1}, {Value2}, {Width}, {Aux}";
}

public class FontExtra
{
    public byte[] Data { get; set; } = new byte[8];
    public string HexDisplay => BitConverter.ToString(Data);

    public short S1 => BitConverter.ToInt16(Data, 0);
    public short S2 => BitConverter.ToInt16(Data, 2);
    public short S3 => BitConverter.ToInt16(Data, 4);
    public short S4 => BitConverter.ToInt16(Data, 6);

    public string ShortsDisplay => $"{S1}, {S2}, {S3}, {S4}";
}

public class FontTuple
{
    public ushort CharId { get; set; }
    public ushort Zero { get; set; }

    public string CharDisplay => $"{(char)CharId} (0x{CharId:X})";
}
