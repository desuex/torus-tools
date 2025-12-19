using System.Collections.Generic;

namespace TorusTool.Models;

public class StringTableRow
{
    public int ID { get; set; }
    public int Offset { get; set; }
    public int Hash { get; set; }   // Kept as int for now, maybe uint? Python used 'I' (unsigned int) in unpack.
    public string Value { get; set; } = string.Empty;
}

public class StringTableData
{
    public int Size { get; set; }
    public int Count { get; set; }
    public List<StringTableRow> Rows { get; set; } = new();
}
