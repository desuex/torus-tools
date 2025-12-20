using System;
using System.IO;
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
    [ObservableProperty]
    private FontDescriptorData? _selectedFontDescriptor;

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

        // Packfile Lazy Load
        if (value.PackEntry != null && value.Records.Count == 0)
        {
           try
           {
               var data = TorusTool.IO.PackfileReader.ExtractFile(CurrentFile, value.PackEntry);
               
               // Try to parse as Hunk
               // Only if extension is hnk? Or always try?
               // If extension is 'dat' or 'zdat' it might not be a hunk file (e.g. raw texture or unknown).
               // But 'languageselection.hnk' is definitely a hunk file.
               
               bool tryParse = value.PackEntry.SuggestedExtension.Contains("hnk");
               
               if (tryParse)
               {
                   using var ms = new System.IO.MemoryStream(data);
                   // Hunk files inside PCK might be BE or LE.
                   // For 3DS, we assume content is also LE?
                   // The main tool config 'IsBigEndian' should dictate how we interpret the CONTENTS (records).
                   // But the container format (Hunkfile headers) itself?
                   // Usually follows the same endianness.
                   // Let's assume 'IsBigEndian' setting is correct for the content.
                   var records = _parser.Parse(ms, IsBigEndian).ToList();
                   
                   if (records.Any())
                   {
                       foreach (var r in records) value.Records.Add(r);
                   }
                   else
                   {
                       // Failed to parse or empty, treat as raw
                       value.Records.Add(new HunkRecord { Type = HunkRecordType.Empty, RawData = data, Size = (uint)data.Length });
                   }
               }
               else
               {
                    // Treat as raw file
                    value.Records.Add(new HunkRecord { Type = HunkRecordType.Empty, RawData = data, Size = (uint)data.Length });
               }
           }
           catch (Exception ex)
           {
                System.Diagnostics.Debug.WriteLine($"Error extracting pack entry: {ex}");
           }
        }

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
            SelectedFontDescriptor = fd; // Assign property

            if (fd != null)
            {
                // Combine Row and Tuple if counts match
                // If not, just show what we can?
                // Cnt1 applies to both.

                for (int i = 0; i < fd.PlatformHeader.GlyphCount; i++)
                {
                    var item = new FontItemViewModel
                    {
                        Index = i
                    };

                    if (i < fd.Codepoints.Count)
                    {
                        item.CharId = (ushort)fd.Codepoints[i].Code;
                        item.CharDisplay = fd.Codepoints[i].CharDisplay;
                    }

                    if (i < fd.PreGlyphs.Count)
                    {
                        var row = fd.PreGlyphs[i];
                        item.RowData = row.HexDisplay;

                        // Custom Parsing based on visualization
                        // V1: X (likely in 4-pixel blocks)
                        // V0: Y (likely bitpacked or just Y)
                        // V2: Width (valid width)
                        // V3: Aux

                        // Mapping based on previous heuristics:
                        // Value2 (V1) -> X
                        // Value1 (V0) -> Y

                        item.X = (short)((row.V1 & 0xFF) * 4);

                        // item.Y logic:
                        item.Y = (short)(((row.V0 >> 8) & 1) * 128);

                        // Width is direct (cast V2 to short)
                        item.Width = (short)row.V2;
                        item.Aux = (short)row.V3;


                        // Heuristic for Height: 
                        // Q1=96 (matches L-Stick height 96).

                        short globalHeight = (short)fd.PlatformHeader.EmOrLineHeight;
                        if (globalHeight <= 0) globalHeight = 32;

                        item.Height = globalHeight;

                        item.DecodedData = $"{item.X}, {item.Y}, {item.Width}, {item.Aux} (Mixed)";

                        // Populate ExtraData if valid index
                        int extraIdx = item.Aux; // Aux is used as index? Or explicit field?

                        if (extraIdx >= 0 && extraIdx < fd.Glyphs.Count)
                        {
                            var g = fd.Glyphs[extraIdx];
                            item.ExtraData = $"{g.A}, {g.B}, {g.C}, {g.D}";
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
                        TextureInfo += $" | Hdr: V={fd.PlatformHeader.GlobalMinY}, Z={fd.PlatformHeader.FlagsOrVersion}, Q1={fd.PlatformHeader.EmOrLineHeight}, Q3={fd.PlatformHeader.SomethingSize}";
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
        if (header.Format.StartsWith("3DS_"))
        {
             return Torus3DSTextureDecoder.Decode(header.Width, header.Height, header.Format, data);
        }

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
    public async Task ExportHnk(IStorageProvider storageProvider)
    {
        if (string.IsNullOrEmpty(CurrentFile))
        {
            // TODO: Alert user
            return;
        }

        var folders = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select Output Folder for Export",
            AllowMultiple = false
        });

        if (folders.Count >= 1)
        {
            var outputDir = folders[0].Path.LocalPath;
            // Use subfolder? "Exported_Filename"?

            var name = System.IO.Path.GetFileNameWithoutExtension(CurrentFile);
            var finalDir = System.IO.Path.Combine(outputDir, $"{name}_Export");

            try
            {
                var exporter = new HunkExporter();
                // Pass metadata from selection
                string gName = SelectedGame?.Name ?? "Unknown";
                string plat = "Unknown";
                if (SelectedGame?.Name != null)
                {
                    if (SelectedGame.Name.Contains("PS3")) plat = "PS3";
                    else if (SelectedGame.Name.Contains("Wii")) plat = "Wii";
                    else if (SelectedGame.Name.Contains("PC")) plat = "PC";
                }

                await Task.Run(() => exporter.Export(CurrentFile, finalDir, gName, plat));

                // Show success?
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Export Error: {ex}");
            }
        }
    }

    [RelayCommand]
    public async Task ImportHnk(IStorageProvider storageProvider)
    {
        // 1. Pick Manifest
        var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select manifest.yaml",
            AllowMultiple = false,
            FileTypeFilter = new[] { new FilePickerFileType("YAML Manifest") { Patterns = new[] { "manifest.yaml", "*.yaml" } } }
        });

        if (files.Count == 0) return;
        var manifestPath = files[0].Path.LocalPath;

        // 2. Pick Output HNK
        var saveFile = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save New HNK File",
            DefaultExtension = "hnk",
            FileTypeChoices = new[] { new FilePickerFileType("Hunk File") { Patterns = new[] { "*.hnk" } } }
        });

        if (saveFile != null)
        {
            var outputPath = saveFile.Path.LocalPath;
            try
            {
                var importer = new HunkImporter();
                await Task.Run(() => importer.Import(manifestPath, outputPath));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Import Error: {ex}");
            }
        }
    }

    [RelayCommand]
    public async Task OpenFile(IStorageProvider storageProvider)
    {
        var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open .hnk File",
            AllowMultiple = false,
            FileTypeFilter = new[] { new FilePickerFileType("Supported Files") { Patterns = new[] { "*.hnk", "*.dat" } } }
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
            // Detect Packfile
            bool isPackfile = false;
            // Check magic
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var buf = new byte[4];
                if (fs.Read(buf, 0, 4) == 4)
                {
                    if (buf[0] == 'P' && buf[1] == 'A' && buf[2] == 'K' && buf[3] == 0)
                    {
                        isPackfile = true;
                    }
                }
            }

            if (isPackfile)
            {
                var pack = TorusTool.IO.PackfileReader.Read(path);
                foreach (var entry in pack.Entries)
                {
                    var node = new HunkFileTreeNode
                    {
                        Name = entry.DisplayName,
                        FullPath = $"{path}/{entry.DisplayName}",
                        IsFolder = false,
                        PackEntry = entry
                    };
                    // Determine if it's a "known" type based on extension?
                    RootNodes.Add(node);
                }
            }
            else
            {
                var records = _parser.Parse(path, false); // Container is always LE
                var tree = HunkFileTreeBuilder.BuildTree(records);
                foreach (var node in tree)
                {
                    RootNodes.Add(node);
                }
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
    [RelayCommand]
    public void OpenTools3DS(Avalonia.Controls.Window owner)
    {
        var window = new TorusTool.Views.Tools3DSWindow();
        window.ShowDialog(owner);
    }
}

public class DataTableItem
{
    public int Index { get; set; }
    public string StringValue { get; set; } = string.Empty;
    public string HexValue { get; set; } = string.Empty;
}