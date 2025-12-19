namespace TorusTool.ViewModels;

public class FontItemViewModel
{
    public int Index { get; set; }
    public ushort CharId { get; set; }
    public string CharDisplay { get; set; } = string.Empty;
    public string RowData { get; set; } = string.Empty;
    public string DecodedData { get; set; } = string.Empty;
    public string ExtraData { get; set; } = string.Empty;
    
    // Explicit Metrics
    public short X { get; set; }
    public short Y { get; set; }
    public short Width { get; set; }
    public short Height { get; set; } // Add Height
    public short Aux { get; set; }
    
    // Atlas Visualization
    public double VisualX => (X & 0xFFF); // Guessing packed format
    public double VisualY => Y;
    public double VisualWidth => Width > 0 ? Width : 5; // Min width for visibility
    public double VisualHeight => 30; // Placeholder height
    
    public bool IsVisibleInAtlas => Width > 0;
}
