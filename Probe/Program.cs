using System;
using System.IO;

class Program
{
    static void Main(string[] args)
    {
        string path = @"d:\TorusGames\Games\3DS\MHNGIS\HUNKFILES\878B912B.zdat";
        using var fs = File.OpenRead(path);
        using var reader = new BinaryReader(fs);

        Console.WriteLine($"File Length: {fs.Length}");

        while (fs.Position < fs.Length)
        {
            long startPos = fs.Position;
            if (fs.Position + 8 > fs.Length) break;
            uint size = reader.ReadUInt32();
            uint type = reader.ReadUInt32();

            Console.WriteLine($"@[{startPos:X}] Type: {type:X} Size: {size} (0x{size:X})");

            if (size == 0)
            {
                // Can't advance
                Console.WriteLine("Size 0 - Abort");
                break;
            }
            
            // Advance
            if (fs.Position + size <= fs.Length)
            {
                 fs.Seek(size, SeekOrigin.Current);
            }
            else
            {
                 Console.WriteLine("Size exceeds file!");
                 break;
            }
        }
    }
}
