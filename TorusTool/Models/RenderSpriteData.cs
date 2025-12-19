using Avalonia; 
using System;
using System.Collections.Generic;

namespace TorusTool.Models
{
    public class RenderSpriteData
    {
        public int Count { get; set; }
        public int Unknown1 { get; set; }
        public int Unknown2 { get; set; }
        public int DataSize { get; set; }
        
        public List<int> Offsets { get; set; } = new();
        public List<RenderSpriteItem> Items { get; set; } = new();
    }
    
    public class RenderSpriteItem
    {
        public int Index { get; set; }
        public int Offset { get; set; }
        public byte[] Data { get; set; } = Array.Empty<byte>(); 
        
        public List<Point> Points { get; set; } = new();

        // Helpers for display
        public string HexDisplay => BitConverter.ToString(Data);
        
        // Try decoding as floats (common for sprites)
        public string FloatsDisplay
        {
            get
            {
                if (Data.Length % 4 != 0) return "";
                var floats = new List<float>();
                for(int i=0; i<Data.Length; i+=4)
                {
                    if (i+4 <= Data.Length)
                        floats.Add(BitConverter.ToSingle(Data, i));
                }
                return string.Join(", ", floats);
            }
        }

        // Try decoding as Ints
        public string IntsDisplay
        {
            get
            {
                if (Data.Length % 4 != 0) return "";
                var ints = new List<int>();
                for(int i=0; i<Data.Length; i+=4)
                {
                     if (i+4 <= Data.Length)
                        ints.Add(BitConverter.ToInt32(Data, i));
                }
                return string.Join(", ", ints);
            }
        }
    }
}
