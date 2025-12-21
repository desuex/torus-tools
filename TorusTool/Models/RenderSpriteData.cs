using System;
using System.Collections.Generic;

namespace TorusTool.Models;

public class RenderSpriteData
{
    // Header (BE)
    public uint Count { get; set; }
    public byte[] DataOffsetLE { get; set; } = new byte[4];
    public ushort Format { get; set; }
    public ushort Zero { get; set; }
    public byte[] DataSizeLE { get; set; } = new byte[4];

    public List<RenderSpriteEntry> Sprites { get; set; } = new();
}

public class RenderSpriteEntry
{
    public int Index { get; set; }
    public RenderSpriteBlock Block { get; set; } = new();
    public List<Avalonia.Point> Points { get; set; } = new();
}

public class RenderSpriteBlock
{
    // word 0..2 (6 x uint16)
    public ushort PackedInfo1 { get; set; }
    public ushort PackedInfo2 { get; set; }
    public ushort Flags1 { get; set; }
    public ushort Flags2 { get; set; }
    public ushort TypeCode { get; set; }
    public ushort Reserved1 { get; set; }

    // word 3..9 (7 x float)
    public float SpeedOrThickness { get; set; } // f0
    public float UvLeft { get; set; }           // f1
    public float StepValue { get; set; }        // f2
    public float UvTop { get; set; }            // f3
    public float UvRight { get; set; }          // f4
    public float Width { get; set; }            // f5
    public float Height { get; set; }           // f6

    // word 10 (2 x uint16)
    public ushort ExtraFlags { get; set; } // extra0
    public ushort RingType { get; set; }   // extra1

    // word 11 (float)
    public float IndexOrAngle { get; set; } // f8

    // word 12..14 (6 x uint16)
    public ushort LookupIndex { get; set; } // u6
    public ushort Reserved2 { get; set; }   // u7
    public ushort Reserved3 { get; set; }   // u8
    public ushort Reserved4 { get; set; }   // u9
    public ushort Reserved5 { get; set; }   // uA
    public ushort Reserved6 { get; set; }   // uB

    // word 15 (float)
    public float FutureFloat { get; set; }  // fTail
}
