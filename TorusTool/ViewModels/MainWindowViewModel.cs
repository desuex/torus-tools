using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TorusTool.Models;
using Avalonia.Platform.Storage;
using Avalonia.Media.Imaging;
using System.Runtime.InteropServices;
using Pfim;

using TorusTool.ViewModels;
using TorusTool.Services;

namespace TorusTool.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly HunkFileParser _parser = new();
    private readonly SettingsService _settingsService = new();

    [ObservableProperty]
    private string _greeting = "Welcome to TorusTool!";

    [ObservableProperty]
    private string _currentFile = string.Empty;

    public ObservableCollection<HunkFileTreeNode> RootNodes { get; } = new();

    public ObservableCollection<string> RecentFiles { get; } = new();

    [RelayCommand]
    private void LoadRecentFile(string path)
    {
        if (System.IO.File.Exists(path))
        {
            OpenFileFromPath(path);
        }
        else
        {
            // Remove?
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HexViewText))]
    [NotifyPropertyChangedFor(nameof(HasStringTable))]
    [NotifyPropertyChangedFor(nameof(IsStringTableVisible))]
    [NotifyPropertyChangedFor(nameof(HasHunkHeader))]
    [NotifyPropertyChangedFor(nameof(IsHunkHeaderVisible))]
    [NotifyPropertyChangedFor(nameof(HasFontDescriptor))]
    [NotifyPropertyChangedFor(nameof(IsFontDescriptorVisible))]
    [NotifyPropertyChangedFor(nameof(HasRenderSprite))]
    [NotifyPropertyChangedFor(nameof(IsRenderSpriteVisible))]
    [NotifyPropertyChangedFor(nameof(HasDataTable))]
    [NotifyPropertyChangedFor(nameof(IsDataTableVisible))]
    private HunkFileTreeNode? _selectedNode;

    public string HexViewText => GenerateHexView(SelectedNode);

    // String Table Logic
    public ObservableCollection<StringTableRow> CurrentStringTable { get; } = new();

    // Font Descriptor Logic
    public ObservableCollection<FontDescriptorData> CurrentFontDescriptor { get; } = new();
    // Actually, FontDescriptorData is a single object with lists. 
    // We should probably expose the lists or a composite view.
    // Let's expose a collection of "FontItems" for the DataGrid.

    public ObservableCollection<FontItemViewModel> CurrentFontItems { get; } = new();

    // Hunk Header Logic
    public ObservableCollection<HunkHeaderRow> CurrentHunkHeader { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRenderSpriteVisible))]
    private ObservableCollection<RenderSpriteItem> _currentRenderSpriteItems = new();

    public ObservableCollection<DataTableItem> CurrentDataTableItems { get; } = new();

    [ObservableProperty]
    private RenderSpriteItem? _selectedRenderSpriteItem;


    [ObservableProperty]
    private bool _showFontOverlay = true;

    private readonly GameConfigService _configService = new();

    public ObservableCollection<GameConfig> AvailableGames { get; } = new();

    [ObservableProperty]
    private GameConfig _selectedGame;

    partial void OnSelectedGameChanged(GameConfig value)
    {
        IsBigEndian = value.IsBigEndian;
        if (_settingsService != null) // Might be null during init if constructor order is weird (but fields init first)
        {
            _settingsService.LastSelectedGameId = value.Id;
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TextureInfo))]
    private bool _isBigEndian = false;

    partial void OnIsBigEndianChanged(bool value)
    {
        // Re-run the node change logic to refresh
        OnSelectedNodeChanged(SelectedNode);
    }

    public MainWindowViewModel()
    {
        // Load settings first
        var recent = _settingsService.RunTimeRecentFiles;
        foreach (var r in recent) RecentFiles.Add(r);

        foreach (var game in _configService.AvailableGames)
        {
            AvailableGames.Add(game);
        }

        // Restore Selection
        var lastId = _settingsService.LastSelectedGameId;
        var found = AvailableGames.FirstOrDefault(g => g.Id == lastId);
        _selectedGame = found ?? _configService.CurrentGame;

        IsBigEndian = _selectedGame.IsBigEndian;
    }

    // Texture Logic
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsTextureVisible))]
    private Avalonia.Media.Imaging.Bitmap? _currentTextureImage;

    [ObservableProperty]
    private string _textureInfo = string.Empty;

    // public bool HasTexture => CurrentTextureImage != null; // Removed
    // public bool IsTextureVisible => HasTexture && !ShowHexOnly; // Removed/Redefined below

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsStringTableVisible))]
    [NotifyPropertyChangedFor(nameof(IsHunkHeaderVisible))]
    [NotifyPropertyChangedFor(nameof(IsFontDescriptorVisible))]
    [NotifyPropertyChangedFor(nameof(IsRenderSpriteVisible))]
    [NotifyPropertyChangedFor(nameof(IsDataTableVisible))]
    [NotifyPropertyChangedFor(nameof(IsTextureVisible))]
    private bool _showHexOnly = false;

    // ... Checkers ...
    private bool CheckForTexture(HunkFileTreeNode? node) => node != null && node.Records.Any(r => r.Type == HunkRecordType.TSETextureHeader || r.Type == HunkRecordType.TSETextureData);

    public bool HasTexture => CheckForTexture(SelectedNode);
    // IsTextureVisible should rely on HasTexture if we want the GRID to be visible. 
    // But the image inside depends on CurrentTextureImage.
    // My previous logic: IsTextureVisible => HasTexture && !ShowHexOnly;
    // Where HasTexture => CurrentTextureImage != null;
    // Wait, earlier I defined HasTexture => CurrentTextureImage != null.
    // But if Decode fails, CurrentTextureImage is null, so HasTexture is false, so IsTextureVisible is false.
    // Then the user sees NOTHING (or falls back to Hex View if !IsStringTableVisible etc).
    // I should separate "HasTextureRecord" from "HasTextureImage".

    public bool IsTextureVisible => (CheckForTexture(SelectedNode)) && !ShowHexOnly;

    public bool HasStringTable => CheckForStringTable(SelectedNode);
    public bool IsStringTableVisible => HasStringTable && !ShowHexOnly;

    public bool HasHunkHeader => CheckForHunkHeader(SelectedNode);
    public bool IsHunkHeaderVisible => HasHunkHeader && !ShowHexOnly;

    public bool HasFontDescriptor => CheckForFontDescriptor(SelectedNode);
    public bool IsFontDescriptorVisible => HasFontDescriptor && !ShowHexOnly;

    public bool HasRenderSprite => CheckForRenderSprite(SelectedNode);
    public bool IsRenderSpriteVisible => HasRenderSprite && !ShowHexOnly;

    public bool HasDataTable => CheckForDataTable(SelectedNode);
    public bool IsDataTableVisible => HasDataTable && !ShowHexOnly;

    private bool CheckForStringTable(HunkFileTreeNode? node) => node?.Records.Any(r => r.Type == HunkRecordType.TSEStringTableMain) ?? false;
    private bool CheckForHunkHeader(HunkFileTreeNode? node) => node?.Records.Any(r => r.Type == HunkRecordType.Header) ?? false;
    private bool CheckForFontDescriptor(HunkFileTreeNode? node) => node?.Records.Any(r => r.Type == HunkRecordType.TSEFontDescriptorData) ?? false;
    private bool CheckForRenderSprite(HunkFileTreeNode? node) => node?.Records.Any(r => r.Type == HunkRecordType.TSERenderSprite) ?? false;
    private bool CheckForDataTable(HunkFileTreeNode? node) => node?.Records.Any(r => r.Type == HunkRecordType.TSEDataTableData1 || r.Type == HunkRecordType.TSEDataTableData2) ?? false;

    private HunkRecord? FindRecordByType(ObservableCollection<HunkFileTreeNode> nodes, HunkRecordType type)
    {
        foreach (var node in nodes)
        {
            var rec = node.Records.FirstOrDefault(r => r.Type == type);
            if (rec != null) return rec;

            var childRec = FindRecordByType(node.Children, type);
            if (childRec != null) return childRec;
        }
        return null;
    }

    partial void OnSelectedNodeChanged(HunkFileTreeNode? value)
    {
        CurrentStringTable.Clear();
        CurrentHunkHeader.Clear();
        CurrentFontItems.Clear();
        CurrentRenderSpriteItems.Clear();
        CurrentDataTableItems.Clear();
        CurrentTextureImage = null;
        if (value == null) return;

        // Check for DataTable
        var dtRecord = value.Records.FirstOrDefault(r => r.Type == HunkRecordType.TSEDataTableData1 || r.Type == HunkRecordType.TSEDataTableData2);
        if (dtRecord != null)
        {
            var dt = RecordParsers.ParseDataTable(dtRecord, IsBigEndian);
            if (dt != null)
            {
                if (dt.StringValues.Any())
                {
                    for (int i = 0; i < dt.StringValues.Count; i++)
                    {
                        var s = dt.StringValues[i];
                        // Re-encode to show what the raw data likely looked like (UTF-16 LE + Null)
                        var bytes = System.Text.Encoding.Unicode.GetBytes(s);
                        // Add null terminator (2 bytes)
                        var fullBytes = new byte[bytes.Length + 2];
                        Array.Copy(bytes, fullBytes, bytes.Length);

                        var hex = BitConverter.ToString(fullBytes).Replace("-", " ");
                        CurrentDataTableItems.Add(new DataTableItem { Index = i, StringValue = s, HexValue = hex });
                    }
                }
                else if (dt.ElementRawData.Any())
                {
                    // Binary Table with split elements
                    for (int i = 0; i < dt.ElementRawData.Count; i++)
                    {
                        var raw = dt.ElementRawData[i];
                        var hex = BitConverter.ToString(raw).Replace("-", " ");
                        CurrentDataTableItems.Add(new DataTableItem { Index = i, HexValue = hex, StringValue = "(Binary)" });
                    }
                }
                else if (dt.Body.Length > 0 && dt.Count > 0)
                {
                    // Fallback if splitting didn't happen (non-even stride?)
                    // Just show one big item? Or "Count" items of unknown size?
                    // If we are here, ElementRawData is empty.
                    CurrentDataTableItems.Add(new DataTableItem { Index = 0, HexValue = "Raw Body (Not split)", StringValue = $"Count={dt.Count}, Len={dt.Body.Length}" });
                }
            }
        }

        // Check for StringTable
        var strTableRecord = value.Records.FirstOrDefault(r => r.Type == HunkRecordType.TSEStringTableMain);
        if (strTableRecord != null)
        {
            var table = RecordParsers.ParseStringTable(strTableRecord, IsBigEndian);
            if (table != null)
            {
                foreach (var row in table.Rows)
                {
                    CurrentStringTable.Add(row);
                }
            }
        }

        // Check for HunkHeader
        var headerRecord = value.Records.FirstOrDefault(r => r.Type == HunkRecordType.Header);
        if (headerRecord != null)
        {
            var header = RecordParsers.ParseHunkHeader(headerRecord, false); // Header is metadata, always LE
            if (header != null)
            {
                foreach (var row in header.Rows)
                {
                    CurrentHunkHeader.Add(row);
                }
            }
        }

        // Scope fd for later use
        FontDescriptorData? fd = null;

        // Check for FontDescriptor
        var fontRecord = value.Records.FirstOrDefault(r => r.Type == HunkRecordType.TSEFontDescriptorData);

        // Fallback: Search globally if not in current node
        if (fontRecord == null)
        {
            fontRecord = FindRecordByType(RootNodes, HunkRecordType.TSEFontDescriptorData);
        }

        if (fontRecord != null)
        {
            fd = RecordParsers.ParseFontDescriptor(fontRecord, IsBigEndian);
            if (fd != null)
            {
                // Combine Row and Tuple if counts match
                // If not, just show what we can?
                // Cnt1 applies to both.

                for (int i = 0; i < fd.Header.Cnt1; i++)
                {
                    var item = new FontItemViewModel
                    {
                        Index = i
                    };

                    if (i < fd.Tuples.Count)
                    {
                        item.CharId = fd.Tuples[i].CharId;
                        item.CharDisplay = fd.Tuples[i].CharDisplay;
                    }

                    if (i < fd.Rows.Count)
                    {
                        var row = fd.Rows[i];
                        item.RowData = row.HexDisplay;

                        // Custom Parsing based on visualization
                        // Value2: X (likely in 4-pixel blocks)
                        // Value1: Y (likely bitpacked or just Y)
                        // Width: valid width
                        // Aux: ???

                        // 2-Row Atlas Hypothesis
                        // Row 0: Value1 ends in E0? -> Y=0
                        // Row 1: Value1 ends in E1? -> Y=128

                        // New Logic based on Win/PS3 Comparison:
                        // PS3 Data (after swap): E0-00-01-1B...
                        // Win Data:              00-E0-1B-01...

                        // Actually, let's look at the "FirstValid" from User Report:
                        // Win: 00-E0-1B-01...
                        // PS3: E0-00-01-1B... -> This looks like scrambled words?
                        // Or maybe My previous parse was reading bytes in file order.
                        // Now "reader.ReadUInt16()" will Swap if BigEndian.

                        // If file has 00 E0 ...
                        // LE Read: 0xE000
                        // BE Read: 0x00E0

                        // Let's assume the Parser does its job.
                        // We just map fields:
                        // X = Value2 * 4?
                        // Y = derived from Value1?

                        // Let's try to match the "Win Data" from the request which seemed correct-ish.
                        // Win Hex: 00-E0-1B-01... (Bytes 0,1, 2,3)
                        // Value1 = 0xE000 (LE)
                        // Value2 = 0x011B (LE)
                        // Width = ...

                        // Wait, "FirstValid" X=108. 108/4 = 27 = 0x1B.
                        // So Byte 2 (0x1B) is X. 
                        // In Win (LE implied?), Bytes are 00 E0 1B 01.
                        // 0,1 -> Value1. 2,3 -> Value2.
                        // Value1 = E000. Value2 = 011B.
                        // So X comes from lower byte of Value2? (0x1B).

                        // PS3 Hex: E0 00 01 1B
                        // BE Read:
                        // Value1 = E000.
                        // Value2 = 011B.
                        // The values MATCH if parsed correctly!

                        // So we can use the same logic for both.

                        // X = (Value2 & 0xFF) * 4;
                        // Y = ((Value1 >> 8) & 1) * 128; // 0xE0 -> 0 (Even), 0xE1 -> 1 (Odd) ??
                        // Actually 0xE0 >> 8 is 0x00 ?? No, 0xE0 is byte 1.
                        // Value1 = 0xE000.
                        // High byte is 0xE0.
                        // (0xE0 & 1) == 0. Y=0. Correct.

                        item.X = (short)((row.Value2 & 0xFF) * 4);
                        // item.Y = (short)(((row.Value1 >> 8) & 1) * 128); // Based on 0xE0/0xE1

                        // Let's rely on the user's "Found D-Pad" example
                        // Win: 09-E1-22-01...
                        // Value1 = E109. High=E1. (Odd -> Y=128?).
                        // Value2 = 0122. Low=22. 0x22 = 34. 34*4 = 136.
                        // User says X_Calc=136. CORRECT.

                        item.Y = (short)(((row.Value1 >> 8) & 1) * 128);

                        // Width is direct
                        item.Width = row.Width;
                        item.Aux = row.Aux;


                        // Heuristic for Height: 
                        // Q1=96 (matches L-Stick height 96).
                        // Q3=28 (too small).

                        short globalHeight = (short)fd.Header.Q1;
                        if (globalHeight <= 0) globalHeight = 32;

                        item.Height = globalHeight;

                        // item.Aux = globalHeight; // Removed hack

                        item.DecodedData = $"{item.X}, {item.Y}, {item.Width}, {item.Aux} (Mixed)";

                        // Populate ExtraData if valid index
                        int extraIdx = item.Aux; // Aux is used as index? Or explicit field?

                        if (extraIdx >= 0 && extraIdx < fd.Extras.Count)
                        {
                            var eData = fd.Extras[extraIdx].Data;
                            short ReadExtraShort(int offset)
                            {
                                if (IsBigEndian)
                                    return (short)((eData[offset] << 8) | eData[offset + 1]);
                                else
                                    return BitConverter.ToInt16(eData, offset);
                            }
                            item.ExtraData = $"{ReadExtraShort(0)}, {ReadExtraShort(2)}, {ReadExtraShort(4)}, {ReadExtraShort(6)}";
                        }
                    }

                    CurrentFontItems.Add(item);
                }
            }
        }

        // Check for RenderSprite
        var spriteRecord = value.Records.FirstOrDefault(r => r.Type == HunkRecordType.TSERenderSprite);
        if (spriteRecord != null)
        {
            var rs = RecordParsers.ParseRenderSprite(spriteRecord, IsBigEndian);
            if (rs != null)
            {
                foreach (var item in rs.Items)
                {
                    CurrentRenderSpriteItems.Add(item);
                }
            }
        }
        // Check for Texture
        CurrentTextureImage = null;
        TextureInfo = "";

        var texHeaderRecord = value.Records.FirstOrDefault(r => r.Type == HunkRecordType.TSETextureHeader);
        var texDataRecord = value.Records.FirstOrDefault(r => r.Type == HunkRecordType.TSETextureData || r.Type == HunkRecordType.TSETextureData2 || r.Type == HunkRecordType.TSETextureDataWii || r.Type == HunkRecordType.TSETextureDataPS3);

        if (texHeaderRecord != null && texDataRecord != null)
        {
            var header = RecordParsers.ParseTextureHeader(texHeaderRecord, IsBigEndian);
            if (header != null)
            {
                TextureInfo = $"{header.Width}x{header.Height} ({header.Format})";

                try
                {
                    CurrentTextureImage = DecodeTexture(header, texDataRecord.RawData);
                }
                catch (Exception ex)
                {
                    TextureInfo += $" | Decode Error: {ex.Message}";
                }

                // Debug Font Items
                if (CurrentFontItems.Count > 0)
                {
                    var f = CurrentFontItems[0];
                    var nonEmpty = CurrentFontItems.Where(x => x.Width > 0).Take(5).ToList();

                    TextureInfo += $" | Glyphs: {CurrentFontItems.Count}";

                    if (fd != null)
                    {
                        TextureInfo += $" | Hdr: V={fd.Header.Vertical}, Z={fd.Header.Z}, Q1={fd.Header.Q1}, Q3={fd.Header.Q3}";
                    }

                    var dpadCandidates = CurrentFontItems.Where(x => Math.Abs(x.Width) >= 98 && Math.Abs(x.Width) <= 108).ToList();
                    var lstickCandidates = CurrentFontItems.Where(x => Math.Abs(x.Width) >= 80 && Math.Abs(x.Width) <= 90).ToList();

                    if (dpadCandidates.Any())
                    {
                        var c = dpadCandidates.First();
                        TextureInfo += $" | Found D-Pad? (Idx={c.Index}): ID={c.CharDisplay}, W={c.Width}, Hex={c.RowData}";
                    }

                    if (lstickCandidates.Any())
                    {
                        var c = lstickCandidates.First();
                        TextureInfo += $" | Found L-Stick? (Idx={c.Index}): ID={c.CharDisplay}, W={c.Width}, Hex={c.RowData}";
                    }

                    var zeroByte2 = CurrentFontItems.FirstOrDefault(item =>
                    {
                        return item.X == 0;
                    });

                    if (zeroByte2 != null)
                    {
                        TextureInfo += $" | RAW ZERO: ID={zeroByte2.CharDisplay}, W={zeroByte2.Width}, Hex={zeroByte2.RowData}";
                    }
                    else
                    {
                        TextureInfo += " | No X=0 found.";
                    }

                    var idC5 = CurrentFontItems.FirstOrDefault(x => x.CharId == 0xC5);
                    if (idC5 != null)
                    {
                        TextureInfo += $" | TARGET C5: ID={idC5.CharDisplay}, W={idC5.Width}, Hex={idC5.RowData} | X_Calc={idC5.X}";
                    }

                    var idCC = CurrentFontItems.FirstOrDefault(x => x.CharId == 0xCC);
                    if (idCC != null)
                    {
                        TextureInfo += $" | TARGET CC: ID={idCC.CharDisplay}, W={idCC.Width}, Hex={idCC.RowData} | X_Calc={idCC.X}";
                    }

                    if (nonEmpty.Any())
                    {
                        var first = nonEmpty[0];
                        TextureInfo += $" | FirstValid: X={first.X}, Y={first.Y}, W={first.Width}";
                        TextureInfo += $" | Hex: {string.Join(" | ", nonEmpty.Select(x => x.RowData))}";
                    }
                    else
                    {
                        TextureInfo += " | No valid glyphs found.";
                    }
                }
                else
                {
                    TextureInfo += " | No Glyphs";
                }
            }
        }
        else
        {
            // Debugging info
            if (texHeaderRecord != null) TextureInfo = "Found Header, missing Data.";
            else if (texDataRecord != null) TextureInfo = "Found Data, missing Header.";
            else TextureInfo = "No Texture Records.";

            // Inspect all types
            var types = string.Join(", ", value.Records.Select(r => r.Type.ToString()));
            System.Diagnostics.Debug.WriteLine($"Selected Node Records: {types}");
        }
    }

    private Avalonia.Media.Imaging.Bitmap? DecodeTexture(TextureHeader header, byte[] data)
    {
        // Add DDS Header
        var ddsHeader = CreateDDSHeader(header.Width, header.Height, header.Format);
        // If Big Endian (PS3), we initially thought we needed to swap DXT data.
        // However, it turns out the DXT Payload is Little Endian (Standard) even on PS3.
        // Only the Header (Metadata) is Big Endian.
        // So we just pass the data through.
        byte[] finalData = data;

        var ddsData = new byte[ddsHeader.Length + finalData.Length];
        Array.Copy(ddsHeader, 0, ddsData, 0, ddsHeader.Length);
        Array.Copy(finalData, 0, ddsData, ddsHeader.Length, finalData.Length);

        using var stream = new System.IO.MemoryStream(ddsData);
        using var image = Pfim.Pfimage.FromStream(stream);

        // Determine PixelFormat
        Avalonia.Platform.PixelFormat pixelFormat = Avalonia.Platform.PixelFormat.Bgra8888;
        if (image.Format == Pfim.ImageFormat.Rgba32) pixelFormat = Avalonia.Platform.PixelFormat.Rgba8888;

        // Handle Rgb24 manually if needed, but DXT usually decodes to 32-bit.

        // ... existing code ...
        var bitmap = new Avalonia.Media.Imaging.WriteableBitmap(
            new Avalonia.PixelSize(image.Width, image.Height),
            new Avalonia.Vector(96, 96), // DPI
            pixelFormat,
            Avalonia.Platform.AlphaFormat.Unpremul
        );

        using (var buffer = bitmap.Lock())
        {
            // Safety check for data length
            int bufferSize = buffer.RowBytes * image.Height;
            int copyLength = Math.Min(image.DataLen, bufferSize);

            // Log if mismatch
            if (image.DataLen != bufferSize)
            {
                System.Diagnostics.Debug.WriteLine($"[Warning] Texture Size Mismatch! Pfim: {image.DataLen}, Bitmap: {bufferSize} (RowBytes={buffer.RowBytes}, H={image.Height})");
            }

            Marshal.Copy(image.Data, 0, buffer.Address, copyLength);
        }

        return bitmap;
    }

    private byte[] CreateDDSHeader(int width, int height, string format)
    {
        var header = new byte[128];
        // Magic
        header[0] = (byte)'D'; header[1] = (byte)'D'; header[2] = (byte)'S'; header[3] = (byte)' ';

        // Size
        BitConverter.GetBytes(124).CopyTo(header, 4);

        // Flags
        uint flags = 0x1 | 0x2 | 0x4 | 0x1000;
        if (format == "DXT1" || format == "DXT5") flags |= 0x80000;
        // Linear size logic omitted safely for Avalonia? Avalonia might need it.
        // Python script calculates linear size.
        // pitch_or_linear_size
        uint pitchOrLinear = 0;
        if (format == "DXT1" || format == "DXT5")
        {
            int blockSize = (format == "DXT1") ? 8 : 16;
            int numBlocksWide = Math.Max(1, (width + 3) / 4);
            int numBlocksHigh = Math.Max(1, (height + 3) / 4);
            pitchOrLinear = (uint)(numBlocksWide * numBlocksHigh * blockSize);
        }
        else
        {
            flags |= 0x8; // DDSD_PITCH
            pitchOrLinear = (uint)(width * 4);
        }
        BitConverter.GetBytes(flags).CopyTo(header, 8);

        BitConverter.GetBytes(height).CopyTo(header, 12);
        BitConverter.GetBytes(width).CopyTo(header, 16);
        BitConverter.GetBytes(pitchOrLinear).CopyTo(header, 20);

        // MipMap count (0 for 2D simple)
        BitConverter.GetBytes(0).CopyTo(header, 28);

        // PixelFormat
        BitConverter.GetBytes(32).CopyTo(header, 76); // Size

        if (format == "DXT1" || format == "DXT5")
        {
            BitConverter.GetBytes(0x4).CopyTo(header, 80); // DDPF_FOURCC

            // FourCC
            header[84] = (byte)format[0];
            header[85] = (byte)format[1];
            header[86] = (byte)format[2];
            header[87] = (byte)format[3];
        }
        else if (format == "R8G8B8A8")
        {
            BitConverter.GetBytes(0x41).CopyTo(header, 80); // RGB | ALPHAPIXELS
            BitConverter.GetBytes(32).CopyTo(header, 88); // RGBBitCount
            BitConverter.GetBytes(0x00FF0000).CopyTo(header, 92); // R
            BitConverter.GetBytes(0x0000FF00).CopyTo(header, 96); // G
            BitConverter.GetBytes(0x000000FF).CopyTo(header, 100); // B
            BitConverter.GetBytes(0xFF000000).CopyTo(header, 104); // A
        }

        // Caps
        uint caps = 0x1000;
        BitConverter.GetBytes(caps).CopyTo(header, 108);

        return header;
    }



    [RelayCommand]
    private void ExitApp()
    {
        Environment.Exit(0);
    }

    [RelayCommand]
    public async Task OpenFile(IStorageProvider storageProvider)
    {
        var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open .hnk File",
            AllowMultiple = false,
            FileTypeFilter = new[] { new FilePickerFileType("Hunk Files") { Patterns = new[] { "*.hnk" } } }
        });

        if (files.Count >= 1)
        {
            var file = files[0];
            var path = file.Path.LocalPath;
            OpenFileFromPath(path);
        }
    }

    private void OpenFileFromPath(string path)
    {
        if (!System.IO.File.Exists(path)) return;

        CurrentFile = path;
        RootNodes.Clear();
        SelectedNode = null;

        // Update Settings
        _settingsService.AddRecentFile(path);

        // Refresh UI List
        RecentFiles.Clear();
        foreach (var r in _settingsService.RunTimeRecentFiles) RecentFiles.Add(r);

        try
        {
            var records = _parser.Parse(path, false); // Container is always LE
            var tree = HunkFileTreeBuilder.BuildTree(records);
            foreach (var node in tree)
            {
                RootNodes.Add(node);
            }
        }
        catch (Exception ex)
        {
            // Error handling (omitted for brevity)
            System.Diagnostics.Debug.WriteLine(ex);
        }
    }

    private string GenerateHexView(HunkFileTreeNode? node)
    {
        if (node == null || node.IsFolder) return "Select a file to view data.";

        var sb = new System.Text.StringBuilder();
        // int globalOffset = 0; // Unused
        int maxBytes = 1000;
        int bytesWritten = 0;

        foreach (var record in node.Records)
        {
            if (bytesWritten >= maxBytes) break;

            sb.AppendLine($"--- Record: {record.TypeDescription} (Size: {record.Size}) ---");

            var data = record.RawData;
            // Cap data if needed, but the user asked for 1000 first bytes of "Right part", 
            // interpreted as the FILE representation or the RECORD representation?
            // "Right part should be a representation of the data (let's write 1000 first bytes..."
            // I'll show the data of the accumulated records for this file node.

            if (data.Length == 0) continue;

            for (int i = 0; i < data.Length; i += 16)
            {
                if (bytesWritten >= maxBytes) break;

                // Offset
                sb.Append($"{i:X8}  ");

                // Hex
                for (int j = 0; j < 16; j++)
                {
                    if (i + j < data.Length)
                        sb.Append($"{data[i + j]:X2} ");
                    else
                        sb.Append("   ");
                }

                sb.Append(" ");

                // ASCII
                for (int j = 0; j < 16; j++)
                {
                    if (i + j < data.Length)
                    {
                        char c = (char)data[i + j];
                        sb.Append(char.IsControl(c) ? '.' : c);
                        bytesWritten++; // Not strictly bytes written to output, but processed
                    }
                }
                sb.AppendLine();
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }
}

public class DataTableItem
{
    public int Index { get; set; }
    public string StringValue { get; set; } = string.Empty;
    public string HexValue { get; set; } = string.Empty;
}