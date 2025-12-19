using System;

namespace TorusTool.Models;

public class HunkRecord
{
    public uint Size { get; set; }
    public HunkRecordType Type { get; set; }
    public byte[] RawData { get; set; } = Array.Empty<byte>();

    public string TypeDescription => Enum.IsDefined(typeof(HunkRecordType), Type) ? Type.ToString() : $"Unknown (0x{((int)Type):X})";
}
