## Torus Games Assets Tool

Torus Games Assets Tool is a tool for extracting and analyzing assets from Torus Games games.
Still in active development. 

Currently supported games:
- Monster High New Ghoul in School (Windows, PS3)

### Features
- **File Viewer**: Inspect content of `.hnk` archives (Textures, Strings, Fonts, etc.).
- **Export/Import**: Unpack `.hnk` files to edit their contents and repack them for use in-game.
  - Supports resizing files.
  - Generates human-readable structure.
  - Handles Big Endian (PS3/Wii) and Little Endian (PC) automatically.

### Usage

#### GUI
Open the application and load a `.hnk` file.
- **Export**: Go to `Tools -> Export HNK...` to unpack the file.
- **Import**: Go to `Tools -> Import HNK...`, select a `manifest.yaml`, and save the new `.hnk` file.

#### CLI (Verification Tool)
You can also use the CLI for batch operations:

```bash
# Export
dotnet run --project TorusTool.Verification -- export <hnk_file> <output_dir>

# Import
dotnet run --project TorusTool.Verification -- import <manifest_file> <output_hnk>
```

### Installation Notes

**macOS**:
If you encounter a "Malware detected" error:
```bash
xattr -d com.apple.quarantine TorusTool
```
