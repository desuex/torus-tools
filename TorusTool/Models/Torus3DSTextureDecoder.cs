using System;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using System.Runtime.InteropServices;

namespace TorusTool.Models
{
    public static class Torus3DSTextureDecoder
    {
        public static Bitmap Decode(int width, int height, string format, byte[] data)
        {
            // Calculate buffer size
            int pixelCount = width * height;
            byte[] pixelData = new byte[pixelCount * 4];

            if (format.Contains("L8")) // 3DS_L8
            {
                 if (data.Length == width * height)
                 {
                     DecodeETC1A4(width, height, data, pixelData);
                 }
                 else
                 {
                     DecodeL8(width, height, data, pixelData);
                 }
            }
            else
            {
                DecodeL8(width, height, data, pixelData);
            }

            // Create Bitmap
            var bitmap = new WriteableBitmap(
                new PixelSize(width, height),
                new Vector(96, 96),
                PixelFormat.Bgra8888,
                AlphaFormat.Unpremul
            );

            using (var buffer = bitmap.Lock())
            {
                Marshal.Copy(pixelData, 0, buffer.Address, pixelData.Length);
            }

            return bitmap;
        }

        private static void DecodeL8(int width, int height, byte[] input, byte[] output)
        {
             // PICA200 8x8 Tiled Morton Swizzling logic (Stubbed if unused, but kept for fallback)
        }

        private static void DecodeETC1A4(int width, int height, byte[] input, byte[] output)
        {
            int ptr = 0;
            
            for (int tileY = 0; tileY < height; tileY += 8)
            {
                for (int tileX = 0; tileX < width; tileX += 8)
                {
                    // 4 Blocks per tile
                    for (int by = 0; by < 2; by++)
                    {
                        for (int bx = 0; bx < 2; bx++)
                        {
                            if (ptr + 16 > input.Length) return;
                            
                            // Read 8 bytes Alpha
                            long alphaChunk = BitConverter.ToInt64(input, ptr);
                            ptr += 8;
                            
                            // Read 8 bytes ETC1
                            long etc1Chunk = BitConverter.ToInt64(input, ptr);
                            byte[] etc1Bytes = new byte[8];
                            Array.Copy(input, ptr, etc1Bytes, 0, 8);
                            ptr += 8;
                            
                            // Decode 4x4 Block
                            int blockX = tileX + bx * 4;
                            int blockY = tileY + by * 4;
                            
                            DecodeETC1Block(etc1Bytes, alphaChunk, blockX, blockY, width, height, output);
                        }
                    }
                }
            }
        }
        
        private static readonly int[,] ETC1ModifierTable = new int[,] 
        {
            { 2, 8, -2, -8 },
            { 5, 17, -5, -17 },
            { 9, 29, -9, -29 },
            { 13, 42, -13, -42 },
            { 18, 60, -18, -60 },
            { 24, 80, -24, -80 },
            { 33, 106, -33, -106 },
            { 47, 183, -47, -183 }
        };

        private static void DecodeETC1Block(byte[] block, long alphaData, int baseX, int baseY, int width, int height, byte[] output)
        {
            // PICA200 / ETC1 Layout (Data First):
            // Bytes 0-3: Data (Colors + Flags)
            // Bytes 4-7: Indices (High 32)
            
            // Construct Big Endian words
            // 'low' = Colors/Flags (0..3)
            uint low  = (uint)((block[0] << 24) | (block[1] << 16) | (block[2] << 8) | block[3]);
            // 'high' = Indices (4..7)
            uint high = (uint)((block[4] << 24) | (block[5] << 16) | (block[6] << 8) | block[7]);
            
            // Flag bits are in 'low' (Data Word)
            bool diffBit = (low & 2) != 0; // Bit 1
            bool flipBit = (low & 1) != 0; // Bit 0
            
            int r1, g1, b1, r2, g2, b2;
            int table1, table2;
            
            if (diffBit)
            {
                // Differential Mode
                int r1_5 = (int)((low >> 27) & 0x1F);
                int g1_5 = (int)((low >> 19) & 0x1F);
                int b1_5 = (int)((low >> 11) & 0x1F);
                
                int dr_3 = (int)((low >> 24) & 0x7);
                int dg_3 = (int)((low >> 16) & 0x7);
                int db_3 = (int)((low >> 8) & 0x7);
                
                // Sign extend 3-bit
                if (dr_3 >= 4) dr_3 -= 8;
                if (dg_3 >= 4) dg_3 -= 8;
                if (db_3 >= 4) db_3 -= 8;
                
                int r2_5 = r1_5 + dr_3;
                int g2_5 = g1_5 + dg_3;
                int b2_5 = b1_5 + db_3;
                
                // Clamp
                r2_5 = Math.Max(0, Math.Min(31, r2_5));
                g2_5 = Math.Max(0, Math.Min(31, g2_5));
                b2_5 = Math.Max(0, Math.Min(31, b2_5));
                
                // Expand to 8-bit
                r1 = (r1_5 << 3) | (r1_5 >> 2);
                g1 = (g1_5 << 3) | (g1_5 >> 2);
                b1 = (b1_5 << 3) | (b1_5 >> 2);
                
                r2 = (r2_5 << 3) | (r2_5 >> 2);
                g2 = (g2_5 << 3) | (g2_5 >> 2);
                b2 = (b2_5 << 3) | (b2_5 >> 2);
            }
            else
            {
                // Individual Mode
                int r1_4 = (int)((low >> 28) & 0xF);
                int g1_4 = (int)((low >> 20) & 0xF);
                int b1_4 = (int)((low >> 12) & 0xF);
                
                int r2_4 = (int)((low >> 24) & 0xF);
                int g2_4 = (int)((low >> 16) & 0xF);
                int b2_4 = (int)((low >> 8) & 0xF);
                
                r1 = (r1_4 << 4) | r1_4;
                g1 = (g1_4 << 4) | g1_4;
                b1 = (b1_4 << 4) | b1_4;
                
                r2 = (r2_4 << 4) | r2_4;
                g2 = (g2_4 << 4) | g2_4;
                b2 = (b2_4 << 4) | b2_4;
            }
            
            table1 = (int)((low >> 5) & 0x7);
            table2 = (int)((low >> 2) & 0x7);
            
            for (int y = 0; y < 4; y++)
            {
                for (int x = 0; x < 4; x++)
                {
                    int px = baseX + x;
                    int py = baseY + y;
                    if (px >= width || py >= height) continue;

                    int subBlock = 0;
                    if (flipBit)
                    {
                        subBlock = (y < 2) ? 0 : 1;
                    }
                    else
                    {
                        subBlock = (x < 2) ? 0 : 1;
                    }
                    
                    int baseR = (subBlock == 0) ? r1 : r2;
                    int baseG = (subBlock == 0) ? g1 : g2;
                    int baseB = (subBlock == 0) ? b1 : b2;
                    int tableIdx = (subBlock == 0) ? table1 : table2;
                    
                    // Pixel Index extraction (Column Major)
                    // High 32 bits (Indices):
                    //   Bits 0..15: LSBs of indices
                    //   Bits 16..31: MSBs of indices
                    int bitPos = x * 4 + y;
                    int bit0 = (int)((high >> bitPos) & 1);
                    int bit1 = (int)((high >> (bitPos + 16)) & 1);
                    int val = (bit1 << 1) | bit0;
                    
                    int modifier = ETC1ModifierTable[tableIdx, val];
                    
                    int r = Math.Max(0, Math.Min(255, baseR + modifier));
                    int g = Math.Max(0, Math.Min(255, baseG + modifier));
                    int b = Math.Max(0, Math.Min(255, baseB + modifier));
                    
                    // Alpha (Keep existing logic)
                    int pixelIdx = y * 4 + x; 
                    int shift = pixelIdx * 4;
                    long alpha4 = (alphaData >> shift) & 0xF;
                    int alpha8 = (int)((alpha4 << 4) | alpha4);
                    
                    int outIdx = (py * width + px) * 4;
                    output[outIdx] = (byte)b;
                    output[outIdx + 1] = (byte)g;
                    output[outIdx + 2] = (byte)r;
                    output[outIdx + 3] = (byte)alpha8;
                }
            }
        }
        
        // Deinterleave bits to extract single coordinate
        private static uint Deinterleave(uint n)
        {
            n &= 0x55555555;
            n = (n | (n >> 1)) & 0x33333333;
            n = (n | (n >> 2)) & 0x0F0F0F0F;
            return n;
        }
    }
}
