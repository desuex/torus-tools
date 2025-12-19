using System;

namespace TorusTool.Models
{
    public class TextureHeader
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public string Format { get; set; } = "DXT1";
        
        // Include raw data if needed?
        // Let's just keep parsed info.
    }
}
