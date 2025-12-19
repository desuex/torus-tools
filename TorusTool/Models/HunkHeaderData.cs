using System;
using System.Collections.Generic;

namespace TorusTool.Models;

public class HunkHeaderRow
{
    public string Name { get; set; } = string.Empty;
    public int MaxSize { get; set; }
    public int Type { get; set; }
    
    public string TypeDescription 
    {
        get 
        {
            if (Enum.IsDefined(typeof(HunkSectionType), Type))
                return ((HunkSectionType)Type).ToString();
            return Type.ToString();
        }
    }
}

public class HunkHeaderData
{
    public short[] Mysteries { get; set; } = new short[8];
    public List<HunkHeaderRow> Rows { get; set; } = new();
}
