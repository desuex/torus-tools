using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TorusTool.Models;
using Avalonia.Platform.Storage;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using System.Collections.Generic;

namespace TorusTool.ViewModels;

public partial class FontToolViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _manifestPath = string.Empty;

    [ObservableProperty]
    private string _statusMessage = "Ready. Please load a manifest.yaml file.";

    [ObservableProperty]
    private ObservableCollection<FontSummaryViewModel> _foundFonts = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DetailViewModel))]
    private FontSummaryViewModel? _selectedFont;

    public FontDescriptorData? DetailViewModel => SelectedFont?.DescriptorData;

    [RelayCommand]
    public async Task LoadManifest(IStorageProvider storageProvider)
    {
        var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select manifest.yaml",
            AllowMultiple = false,
            FileTypeFilter = new[] { new FilePickerFileType("YAML Manifest") { Patterns = new[] { "manifest.yaml", "*.yaml" } } }
        });

        if (files.Count == 0) return;

        var path = files[0].Path.LocalPath;
        ManifestPath = path;

        await ScanManifest(path);
    }

    private async Task ScanManifest(string path)
    {
        StatusMessage = "Scanning...";
        FoundFonts.Clear();

        await Task.Run(async () =>
        {
            try
            {
                var deserializer = new DeserializerBuilder()
                    .WithNamingConvention(CamelCaseNamingConvention.Instance)
                    .IgnoreUnmatchedProperties()
                    .Build();

                var yaml = await File.ReadAllTextAsync(path);
                var manifest = deserializer.Deserialize<ManifestRoot>(yaml);

                var rootDir = Path.GetDirectoryName(path);

                // Find TSEFontDescriptor node
                var fontNode = FindNode(manifest.RootNodes, "TSEFontDescriptor");
                if (fontNode != null)
                {
                    foreach (var fontInfo in fontNode.Children)
                    {
                        var summary = new FontSummaryViewModel { Name = fontInfo.Name };

                        // Find FontDescriptor Record
                        var fdRecord = fontInfo.Records.FirstOrDefault(r => r.Type == "TSEFontDescriptorData" || r.TypeId == 0x43087 || r.TypeId == 0x42087);
                        if (fdRecord != null)
                        {
                            var dataFilePath = fdRecord.DataFile.Replace('\\', Path.DirectorySeparatorChar);
                            var fullPath = Path.Combine(rootDir!, dataFilePath);
                            if (File.Exists(fullPath))
                            {
                                summary.DescriptorPath = fullPath;

                                // Parse it
                                var raw = await File.ReadAllBytesAsync(fullPath);
                                var junkRecord = new HunkRecord { RawData = raw, Type = (HunkRecordType)fdRecord.TypeId, Size = (uint)raw.Length };
                                var fd = RecordParsers.ParseFontDescriptor(junkRecord, manifest.IsBigEndian);
                                summary.DescriptorData = fd;
                                if (fd != null)
                                {
                                    summary.GlyphCount = fd.PlatformHeader.GlyphCount;
                                    summary.LineHeight = fd.PlatformHeader.LineHeight;

                                    int idx = 0;
                                    foreach (var g in fd.Glyphs)
                                    {
                                        summary.Glyphs.Add(new FontItemViewModel
                                        {
                                            Index = idx++,
                                            CharId = g.GlyphIndex,
                                            ExtraData = $"El:{g.ElementId} P1:{g.Param1} P2:{g.Param2}"
                                        });
                                    }
                                }
                            }
                        }

                        // Correlate with Texture?
                        var textureRoot = FindNode(manifest.RootNodes, "TSETexture");
                        if (textureRoot != null)
                        {
                            var match = textureRoot.Children.FirstOrDefault(c =>
                                c.Name.Equals(fontInfo.Name, StringComparison.OrdinalIgnoreCase) ||
                                c.Name.Replace("0", "").Equals(fontInfo.Name.Replace("0", ""), StringComparison.OrdinalIgnoreCase) ||
                                 c.Name.Replace("_", "").Equals(fontInfo.Name.Replace("_", ""), StringComparison.OrdinalIgnoreCase)
                            );

                            if (match != null)
                            {
                                summary.TextureName = match.Name;
                            }
                        }

                        // Correlate with RenderSprite?
                        var spriteRoot = FindNode(manifest.RootNodes, "RenderSprite");
                        if (spriteRoot != null)
                        {
                            var match = spriteRoot.Children.FirstOrDefault(c =>
                                c.Name.Replace("0", "").Equals(fontInfo.Name.Replace("0", ""), StringComparison.OrdinalIgnoreCase)
                            );

                            if (match != null)
                            {
                                summary.RenderSpriteName = match.Name;

                                // Parse RenderSpriteData
                                var rsRecord = match.Records.FirstOrDefault(r => r.Type == "RenderSpriteData" || r.TypeId == 0x41007 || r.TypeId == 266247);
                                if (rsRecord != null)
                                {
                                    var rsPath = Path.Combine(rootDir!, rsRecord.DataFile.Replace('\\', Path.DirectorySeparatorChar));
                                    if (File.Exists(rsPath))
                                    {
                                        var raw = await File.ReadAllBytesAsync(rsPath);
                                        var junk = new HunkRecord { RawData = raw, Type = HunkRecordType.TSERenderSprite, Size = (uint)raw.Length };
                                        summary.RenderSpriteData = RecordParsers.ParseRenderSprite(junk, manifest.IsBigEndian);
                                    }
                                }
                            }
                        }

                        // Add to UI (on UI thread)
                        Avalonia.Threading.Dispatcher.UIThread.Post(() => FoundFonts.Add(summary));
                    }
                }

                Avalonia.Threading.Dispatcher.UIThread.Post(() => StatusMessage = $"Found {FoundFonts.Count} fonts.");
            }
            catch (Exception ex)
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() => StatusMessage = $"Error: {ex.Message}");
            }
        });
    }

    private ManifestNode? FindNode(List<ManifestNode> nodes, string name)
    {
        foreach (var node in nodes)
        {
            if (node.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) return node;
        }
        return null;
    }
}

public class FontSummaryViewModel
{
    public string Name { get; set; } = string.Empty;
    public string DescriptorPath { get; set; } = string.Empty;
    public FontDescriptorData? DescriptorData { get; set; }

    public int GlyphCount { get; set; }
    public int LineHeight { get; set; }

    public ObservableCollection<FontItemViewModel> Glyphs { get; set; } = new();

    public string TextureName { get; set; } = "Not Found";
    public string RenderSpriteName { get; set; } = "Not Found";

    public RenderSpriteData? RenderSpriteData { get; set; }

    public string DisplayInfo => $"{Name} (Glyphs: {GlyphCount})";
}
