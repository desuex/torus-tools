using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;

namespace TorusTool.IO;

public class TorusBinaryReader : IDisposable
{
    private readonly BinaryReader _reader;
    private readonly bool _isBigEndian;

    public TorusBinaryReader(Stream input, bool isBigEndian)
    {
        _reader = new BinaryReader(input);
        _isBigEndian = isBigEndian;
    }

    public TorusBinaryReader(byte[] data, bool isBigEndian)
    {
        _reader = new BinaryReader(new MemoryStream(data));
        _isBigEndian = isBigEndian;
    }

    public long Position
    {
        get => _reader.BaseStream.Position;
        set => _reader.BaseStream.Position = value;
    }

    public long Length => _reader.BaseStream.Length;

    public byte ReadByte() => _reader.ReadByte();
    public byte[] ReadBytes(int count) => _reader.ReadBytes(count);

    public sbyte ReadSByte() => _reader.ReadSByte();

    // Endian-aware reads
    public short ReadInt16()
    {
        var val = _reader.ReadInt16();
        return _isBigEndian ? BinaryPrimitives.ReverseEndianness(val) : val;
    }

    public ushort ReadUInt16()
    {
        var val = _reader.ReadUInt16();
        return _isBigEndian ? BinaryPrimitives.ReverseEndianness(val) : val;
    }

    public int ReadInt32()
    {
        var val = _reader.ReadInt32();
        return _isBigEndian ? BinaryPrimitives.ReverseEndianness(val) : val;
    }

    public uint ReadUInt32()
    {
        var val = _reader.ReadUInt32();
        return _isBigEndian ? BinaryPrimitives.ReverseEndianness(val) : val;
    }

    public long ReadInt64()
    {
        var val = _reader.ReadInt64();
        return _isBigEndian ? BinaryPrimitives.ReverseEndianness(val) : val;
    }

    public ulong ReadUInt64()
    {
        var val = _reader.ReadUInt64();
        return _isBigEndian ? BinaryPrimitives.ReverseEndianness(val) : val;
    }

    public float ReadSingle()
    {
        var val = _reader.ReadSingle();
        if (_isBigEndian)
        {
            // Reverse bytes for float
            var bytes = BitConverter.GetBytes(val);
            Array.Reverse(bytes);
            return BitConverter.ToSingle(bytes, 0);
        }
        return val;
    }

    public string ReadStringFixed(int length)
    {
        var bytes = _reader.ReadBytes(length);
        // Trim nulls
        return Encoding.UTF8.GetString(bytes).TrimEnd('\0');
    }

    public void Seek(long offset, SeekOrigin origin)
    {
        _reader.BaseStream.Seek(offset, origin);
    }

    public void Align(int alignment)
    {
        long pos = Position;
        if (pos % alignment != 0)
        {
            long newPos = (pos + alignment - 1) / alignment * alignment;
            Seek(newPos, SeekOrigin.Begin);
        }
    }

    public void Dispose()
    {
        _reader.Dispose();
    }
}
