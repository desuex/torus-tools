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
                // Is this actually L8? If user said it failed, maybe we repurpose this ID 
                // effectively, but let's keep L8 method for legacy and add ETC1.
                // But the caller passes "3DS_L8" because we hardcoded it in RecordParsers.
                // We should assume "3DS_L8" might actually be "3DS_ETC1A4" if size matches?
                // Or I'll update RecordParsers to say "3DS_ETC1A4" later.
                // For now, let's force ETC1A4 logic if length matches dimensions * 1
                
                 if (data.Length == width * height)
                 {
                     // Could be L8 or ETC1A4 (both 1bpp avg)
                     // Given L8 looked like noise, let's try ETC1A4.
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
             // ... (Keep existing L8 logic or Empty) ...
        }

        private static void DecodeETC1A4(int width, int height, byte[] input, byte[] output)
        {
            // PICA200 ETC1A4 Layout: 8x8 Tiles.
            // Each 8x8 Tile contains four 4x4 compressed blocks.
            // Tile Layout (2x2 blocks):
            // Block 0: (0,0) - (3,3)
            // Block 1: (4,0) - (7,3)
            // Block 2: (0,4) - (3,7)
            // Block 3: (4,4) - (7,7)
            
            // Per 4x4 Block:
            // 8 bytes Alpha (64 bits) -> 4 bits per pixel? 
            //   Usually reversed? Or LSB?
            //   Reference: Alpha is separate. PICA might store it as 8 bytes A4.
            // 8 bytes ETC1 Color
            
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
            // PICA200 ETC1 / Standard ETC1 (Big Endian Stream)
            // Bytes 0-3: High 32 bits (Pixel Indices)
            // Bytes 4-7: Low 32 bits (Data / Colors / Flags)
            
            // Construct Big Endian words manually to match Spec bit positions
            uint high = (uint)((block[0] << 24) | (block[1] << 16) | (block[2] << 8) | block[3]);
            uint low  = (uint)((block[4] << 24) | (block[5] << 16) | (block[6] << 8) | block[7]);
            
            // 'low' contains Data/Colors (Bits 31..0 of 64-bit word)
            // 'high' contains Indices (Bits 63..32 of 64-bit word)

            // Parse Data (low)
            // Bit 0 (LSB of low) is technically Bit 0 of 64-bit word? 
            // Yes, if we treat 0-7 as MSB..LSB sequence. 
            // So block[7] contains Bits 7..0. block[7] bit 0 is Bit 0.
            // Our 'low' construction puts block[7] at LSB. So 'low' bits align with Spec.
            
            bool diffBit = (low & 2) != 0; // Bit 1
            bool flipBit = (low & 1) != 0; // Bit 0
            
            int r1, g1, b1, r2, g2, b2;
            int table1, table2;
            
            if (diffBit)
            {
                // Differential Mode
                // base1: 5 bits (Bits 27..31 -> Top 5 bits of Low)
                // R1: low >> 27 ?
                // Let's check mask. 
                // R1 is Bits 63..59 of LOW part? No, Spec says 63..32 is Index.
                // Low is 31..0.
                // Base color 1 R is Bits 27..31 (5 bits).
                
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
            
            // Decode Pixels
            // Indices in 'high'.
            // "MSB of pixel index is... LSB of pixel index is..."
            // Let's use the standard "Access 2 bits at position i" logic.
            // Index bits are spread across MS 16 bits and LS 16 bits of the 32-bit 'high' word?
            // Usually: 
            // LSBs for 16 pixels are in bits 15..0
            // MSBs for 16 pixels are in bits 31..16
            
            for (int y = 0; y < 4; y++)
            {
                for (int x = 0; x < 4; x++)
                {
                    int px = baseX + x;
                    int py = baseY + y;
                    if (px >= width || py >= height) continue;

                    // Init Subblock
                    int subBlock = 0; // 0 or 1
                    if (flipBit)
                    {
                        // 2x4 blocks (Top, Bottom)
                        subBlock = (y < 2) ? 0 : 1;
                    }
                    else
                    {
                        // 4x2 blocks (Left, Right)
                        subBlock = (x < 2) ? 0 : 1;
                    }
                    
                    int baseR = (subBlock == 0) ? r1 : r2;
                    int baseG = (subBlock == 0) ? g1 : g2;
                    int baseB = (subBlock == 0) ? b1 : b2;
                    int tableIdx = (subBlock == 0) ? table1 : table2;
                    
                    // Pixel Index extraction
                    // Standard ETC1 (OpenGL):
                    // Pixels are Row-Major: 0=(0,0), 1=(1,0), 2=(2,0), 3=(3,0), 4=(0,1)...
                    // Indices are packed MSB first.
                    // i.e. Bit 15 of 'indices' word is for pixel 0. Bit 0 is for pixel 15.
                    
                    int pixelIndex = y * 4 + x; 
                    int bitOffset = 15 - pixelIndex;
                    
                    // High word contains 32 bits: [Table2(16)] [Table1(16)]?
                    // No, 'high' variable contains the 32 bits of Indices.
                    // The spec says:
                    // bytes 0..1 = LSBs of indices
                    // bytes 2..3 = MSBs of indices 
                    // (Using Byte offsets 0..7 of file).
                    
                    // My previous 'high' construction:
                    // block[0]<<24 | block[1]<<16 ...
                    // This puts block[0] at bits 31..24.
                    
                    // If file bytes 0..3 are Indices (as assumed in previous step):
                    // block[0..1] = IndexMSB? or IndexLSB?
                    // block[2..3] = IndexLSB? or IndexMSB?
                    
                    // Let's look at the mask logic.
                    // LSB vector = bytes 0,1 ?
                    // MSB vector = bytes 2,3 ?
                    
                    // Let's assume 'high' is constructed as Big Endian ulong.
                    // But maybe we should split it.
                    // uint val = high;
                    // vector1 = val >> 16; (Upper 16) -> block[0], block[1]
                    // vector2 = val & 0xFFFF; (Lower 16) -> block[2], block[3]
                    
                    // Usually:
                    // Index LSBs = 0..15 bits
                    // Index MSBs = 16..31 bits
                    
                    // Let's try:
                    // bit0 comes from one half, bit1 from the other.
                    // Which one?
                    // If stripes are vertical, maybe x/y mapping is still wrong.
                    
                    // Let's try standard Row Major + MSB extraction on the current 'high' word.
                    // But assume 'high' has LSB vector in lower 16, MSB vector in upper 16?
                    // Previous: bit0 from lower 16, bit1 from upper 16.
                    
                    int bit0 = (int)((high >> bitOffset) & 1);        // From block[2..3] (if valid)
                    int bit1 = (int)((high >> (bitOffset + 16)) & 1); // From block[0..1]
                    
                    // Wait, if I used MSB First (15-i), and `high` is 32 bits...
                    // Lower 16 bits (0-15) correspond to block[2..3].
                    // Upper 16 bits (16-31) correspond to block[0..1].
                    
                    // If standard:
                    // LSB vector = bytes 0,1??
                    // Let's assume block[0..1] is LSB Indicies, block[2..3] is MSB Indices.
                    // Then `bit0` should come from upper 16 (if BE load) or lower 16?
                    // My `high` load: block[0] is most significant BITS.
                    // So block[0..1] is Upper 16.
                    
                    // If block[0..1] stores LSBs, then Bit0 comes from Upper 16.
                    // If block[2..3] stores MSBs, then Bit1 comes from Lower 16.
                    
                    // Let's try:
                    // bit0 = (high >> (bitOffset + 16)) & 1;
                    // bit1 = (high >> bitOffset) & 1;
                    
                    int val = (bit1 << 1) | bit0;
                    
                    int modifier = ETC1ModifierTable[tableIdx, val];
                    
                    int r = Math.Max(0, Math.Min(255, baseR + modifier));
                    int g = Math.Max(0, Math.Min(255, baseG + modifier));
                    int b = Math.Max(0, Math.Min(255, baseB + modifier));
                    
                    // Alpha (Keep existing logic)
                    // PICA Alpha is often tiled too? Or just row major?
                    // Previous success suggests existing alpha logic was okay-ish.
                    // But let's verify if alpha needs transposing too. 
                    // Previous: `int pixelIdx = y * 4 + x;` (Row Major)
                    // If ETC1 colors use Column Major (x*4+y), maybe alpha does too?
                    // Let's stick to Row Major for Alpha for now since shape was "correct".
                    
                    // Wait, if colors are Column Major, Alpha should probably match?
                    // Let's try Column Major for Alpha too? 
                    // `int alphaIdx = x * 4 + y;`
                    // Just to be safe. "Shape correct" might be lenient on transposition if shape is simple.
                    // Let's keep Alpha Row Major (y*4+x) for now to minimize regression risk on shape.
                    
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
