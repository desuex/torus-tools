using System;

namespace TorusTool.Models;

public static class WiiTextureDecoder
{
    public static byte[] Detile(byte[] input, int width, int height, string format)
    {
        // Only handling DXT1 (CMPR) for now
        if (format != "DXT1") return input;

        // Wii CMPR (DXT1) is stored in 8x8 tiles.
        // Each tile contains four 4x4 DXT blocks.
        // Order: TL, TR, BL, BR.
        // Input Size checks
        int bpp = 8; // DXT1 is 8 bytes per 4x4 block
                     // 8x8 tile = 4 blocks = 32 bytes

        int tilesX = (width + 7) / 8;
        int tilesY = (height + 7) / 8;

        // Linear DXT buffer size
        // Standard DXT is sequence of 4x4 blocks in raster order.
        int blocksW = (width + 3) / 4;
        int blocksH = (height + 3) / 4;

        byte[] output = new byte[input.Length];

        // Iterate through the Tiled Input
        int srcOffset = 0;

        for (int ty = 0; ty < tilesY; ty++)
        {
            for (int tx = 0; tx < tilesX; tx++)
            {
                // Each 8x8 tile has 4 blocks (if fully within bounds)
                // BLK 0: (x*2, y*2)
                // BLK 1: (x*2+1, y*2)
                // BLK 2: (x*2, y*2+1)
                // BLK 3: (x*2+1, y*2+1)

                // Block coordinate bases
                int bx = tx * 2;
                int by = ty * 2;

                // Read 4 blocks
                for (int i = 0; i < 4; i++)
                {
                    int currBx = bx + (i % 2);
                    int currBy = by + (i / 2);

                    if (srcOffset + 8 > input.Length) break;

                    // Copy specific block if it's within the image bounds
                    // Even if out of bounds, the file usually pads it.
                    // But for the OUTPUT, we just need to place it correctly.

                    if (currBx < blocksW && currBy < blocksH)
                    {
                        // Calculate Dest Offset for this block
                        // Linear DXT: Block index = by * blocksW + bx
                        int destBlockIdx = currBy * blocksW + currBx;
                        int destOffset = destBlockIdx * 8;

                        if (destOffset + 8 <= output.Length)
                        {
                            // Copy 8 bytes
                            Array.Copy(input, srcOffset, output, destOffset, 8);

                            // While copying, do we need endian swap?
                            // Wii is Big Endian. DXT usually Little Endian.
                            // CMPR blocks might need 16-bit or 32-bit swap.
                            // Let's perform a 16-bit swap on the copied bytes just in case,
                            // OR we can leave it to the calling code.
                            // Since strict revert fixed "garbage" to "too wide", maybe swap WAS needed but detiling was the main issue.
                            // Let's try JUST detiling first.

                            // Experiment: Swap bytes in the block for Wii?
                            // Usually: yes.
                            SwapBlock(output, destOffset);
                        }
                    }

                    srcOffset += 8;
                }
            }
        }

        return output;
    }

    private static void SwapBlock(byte[] data, int offset)
    {
        // 16-bit swap (0x1234 -> 0x3412)
        // DXT1 block: 8 bytes.
        // Color0 (2), Color1 (2), Indices (4)

        // Swap first 2 bytes (Color0)
        byte temp = data[offset];
        data[offset] = data[offset + 1];
        data[offset + 1] = temp;

        // Swap next 2 bytes (Color1)
        temp = data[offset + 2];
        data[offset + 2] = data[offset + 3];
        data[offset + 3] = temp;

        // Indices: 32-bit encoded... but treated as bytes?
        // In CMPR, these are big-endian bytes or just byte-swapped?
        // Usually simply swapping every 2 bytes works for the whole block on GC/Wii.

        temp = data[offset + 4];
        data[offset + 4] = data[offset + 5];
        data[offset + 5] = temp;

        temp = data[offset + 6];
        data[offset + 6] = data[offset + 7];
        data[offset + 7] = temp;
    }
}
