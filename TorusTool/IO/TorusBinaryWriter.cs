using System;
using System.IO;
using System.Text;

namespace TorusTool.IO;

public class TorusBinaryWriter : BinaryWriter
{
    private readonly bool _userDataBigEndian;

    public TorusBinaryWriter(Stream output, bool isBigEndian = false) : base(output)
    {
        _userDataBigEndian = isBigEndian;
    }

    public TorusBinaryWriter(Stream output, Encoding encoding, bool isBigEndian = false) : base(output, encoding)
    {
        _userDataBigEndian = isBigEndian;
    }

    public TorusBinaryWriter(Stream output, Encoding encoding, bool leaveOpen, bool isBigEndian = false) : base(output, encoding, leaveOpen)
    {
        _userDataBigEndian = isBigEndian;
    }

    public override void Write(short value)
    {
        if (_userDataBigEndian)
        {
            var data = BitConverter.GetBytes(value);
            Array.Reverse(data);
            base.Write(data);
        }
        else
        {
            base.Write(value);
        }
    }

    public override void Write(ushort value)
    {
        if (_userDataBigEndian)
        {
            var data = BitConverter.GetBytes(value);
            Array.Reverse(data);
            base.Write(data);
        }
        else
        {
            base.Write(value);
        }
    }

    public override void Write(int value)
    {
        if (_userDataBigEndian)
        {
            var data = BitConverter.GetBytes(value);
            Array.Reverse(data);
            base.Write(data);
        }
        else
        {
            base.Write(value);
        }
    }

    public override void Write(uint value)
    {
        if (_userDataBigEndian)
        {
            var data = BitConverter.GetBytes(value);
            Array.Reverse(data);
            base.Write(data);
        }
        else
        {
            base.Write(value);
        }
    }

    public override void Write(long value)
    {
        if (_userDataBigEndian)
        {
            var data = BitConverter.GetBytes(value);
            Array.Reverse(data);
            base.Write(data);
        }
        else
        {
            base.Write(value);
        }
    }

    public override void Write(ulong value)
    {
        if (_userDataBigEndian)
        {
            var data = BitConverter.GetBytes(value);
            Array.Reverse(data);
            base.Write(data);
        }
        else
        {
            base.Write(value);
        }
    }

    public override void Write(float value)
    {
        if (_userDataBigEndian)
        {
            var data = BitConverter.GetBytes(value);
            Array.Reverse(data);
            base.Write(data);
        }
        else
        {
            base.Write(value);
        }
    }

    public override void Write(double value)
    {
        if (_userDataBigEndian)
        {
            var data = BitConverter.GetBytes(value);
            Array.Reverse(data);
            base.Write(data);
        }
        else
        {
            base.Write(value);
        }
    }
}
