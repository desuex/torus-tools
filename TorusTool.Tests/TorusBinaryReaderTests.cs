using System.IO;
using TorusTool.Models;
using TorusTool.IO;
using Xunit;

namespace TorusTool.Tests
{
    public class TorusBinaryReaderTests
    {
        [Fact]
        public void ReadInt32_LittleEndian_ReadsCorrectly()
        {
            // Arrange
            byte[] data = { 0x01, 0x02, 0x03, 0x04 }; // 0x04030201 in LE
            using var ms = new MemoryStream(data);
            using var reader = new TorusBinaryReader(ms, isBigEndian: false);

            // Act
            int result = reader.ReadInt32();

            // Assert
            Assert.Equal(0x04030201, result);
        }

        [Fact]
        public void ReadInt32_BigEndian_ReadsCorrectly()
        {
            // Arrange
            byte[] data = { 0x01, 0x02, 0x03, 0x04 }; // 0x01020304 in BE
            using var ms = new MemoryStream(data);
            using var reader = new TorusBinaryReader(ms, isBigEndian: true);

            // Act
            int result = reader.ReadInt32();

            // Assert
            Assert.Equal(0x01020304, result);
        }

        [Fact]
        public void ReadSingle_BigEndian_ReadsCorrectly()
        {
            // Arrange
            // 1.0f in IEEE 754 is 0x3F800000
            // BE: 3F 80 00 00
            byte[] data = { 0x3F, 0x80, 0x00, 0x00 };
            using var ms = new MemoryStream(data);
            using var reader = new TorusBinaryReader(ms, isBigEndian: true);

            // Act
            float result = reader.ReadSingle();

            // Assert
            Assert.Equal(1.0f, result);
        }

        [Fact]
        public void ReadSingle_LittleEndian_ReadsCorrectly()
        {
            // Arrange
            // 1.0f
            // LE: 00 00 80 3F
            byte[] data = { 0x00, 0x00, 0x80, 0x3F };
            using var ms = new MemoryStream(data);
            using var reader = new TorusBinaryReader(ms, isBigEndian: false);

            // Act
            float result = reader.ReadSingle();

            // Assert
            Assert.Equal(1.0f, result);
        }

        [Fact]
        public void Alignment_WorksCorrectly()
        {
            // Arrange
            byte[] data = new byte[16];
            using var ms = new MemoryStream(data);
            using var reader = new TorusBinaryReader(ms, isBigEndian: false);

            // Act
            reader.ReadByte(); // Pos 1
            reader.Align(4);   // Should move to 4

            // Assert
            Assert.Equal(4, reader.Position);
        }
    }
}
