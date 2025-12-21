using System;
using System.IO;
using TorusTool.Models;
using Xunit;

namespace TorusTool.Tests
{
    public class RecordParsersTests
    {
        [Fact]
        public void ParseRenderSprite_WiiFormat_ReadsCorrectly()
        {
            // Scenario: Wii format
            // Header: Big Endian
            // Pointers: Little Endian
            // Block Data: Big Endian (Platform)
            
            // 1. Create Mock Data
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);

            // --- Header (BE) ---
            // Count = 1
            writer.Write(System.Buffers.Binary.BinaryPrimitives.ReverseEndianness(1)); 
            // DataOffset = 0x10 (LE field in struct, so we write 0x10 directly as bytes)
            writer.Write((byte)0x10); writer.Write((byte)0x00); writer.Write((byte)0x00); writer.Write((byte)0x00);
            // Format = 1 (BE)
            writer.Write(System.Buffers.Binary.BinaryPrimitives.ReverseEndianness((ushort)1)); 
            // Zero = 0
            writer.Write((ushort)0);
            // DataSize = 64 (0x40) (LE)
            writer.Write((byte)0x40); writer.Write((byte)0x00); writer.Write((byte)0x00); writer.Write((byte)0x00);

            // --- Pointer Table (LE) ---
            // Offset to first sprite = 0x14 (Header 0x10 + 4 bytes pointer = 0x14? No, absolute offset)
            // Header is 0x10 bytes. Pointer table is 4 bytes (1 entry). Total 0x14. 
            // So data starts at 0x14.
            writer.Write((int)0x14); 

            // --- Sprite Block (BE) ---
            // Just test a few fields to verify BE reading
            // SpeedOrThickness (Word 3/Index 6 in raw float array view) = 1.5f
            // 1.5f = 0x3FC00000. BE = 3F C0 00 00
            
            // Fill words 0-2 (uint16s)
            for(int i=0; i<6; i++) writer.Write(System.Buffers.Binary.BinaryPrimitives.ReverseEndianness((ushort)0));

            // Word 3: SpeedOrThickness (float)
            byte[] floatBytes = BitConverter.GetBytes(1.5f);
            Array.Reverse(floatBytes); // Make BE
            writer.Write(floatBytes); 

            // Fill rest with 0
            for(int i=0; i<60; i++) writer.Write((byte)0);

            // 2. Prepare Record
            var data = ms.ToArray();
            var record = new HunkRecord { RawData = data, Type = HunkRecordType.RenderSpriteData };

            // 3. Parse (Simulate Wii = Big Endian)
            var result = RecordParsers.ParseRenderSprite(record, isBigEndian: true);

            // 4. Assert
            Assert.NotNull(result);
            Assert.Single(result.Sprites);
            Assert.Equal(1.5f, result.Sprites[0].Block.SpeedOrThickness);
        }
    }
}
