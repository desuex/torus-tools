## Torus Games Assets Tool

Torus Games Assets Tool is a tool for extracting and analyzing assets from Torus Games games.
Still in active development. 

Currently supported games:
- Monster High New Ghoul in School (Windows, PS3, Wii, Xbox 360, *3DS*)

Also tested on (with varying success):
- Barbie Puppy Rescue (Windows)
- Barbie Dreamhouse (Windows)
- Falling Skies: The Game (Windows)


### Features
- **File Viewer**: Inspect content of `.hnk` archives (Textures, Strings, Fonts, etc.).
- **Font Tool**: Specialized scanner and viewer for Game Fonts (`.tsefontdescriptor`).
  - ~~Visualize Glyph Atlases with character overlays.~~
  - View detailed Font Metrics and Glyph data.
  - Automatically correlates Font Descriptors with Textures and RenderSprite data via `manifest.yaml`.
- **3DS Tool**: can unpack/repack `packfile.dat` files, used in some 3DS games by Torus.
- **Advanced Inspectors**:
  - **RenderSpriteData**: Detailed parsing of sprite animation blocks.
    - Supports accurate UVs, Speed/Thickness, Ring Types, and Size.
    - ~~Full~~ support for **Wii (Big Endian)** and PC/Xbox/PS3 formats.
  - **String Tables**: View ~~and export~~ localized strings.
- **Export/Import**: Unpack `.hnk` files to edit their contents and repack them for use in-game.
  - Supports resizing files (maybe? probably not).
  - Generates human-readable structure (but only if you are a robot).
  - Handles Big Endian (PS3/Wii/Xbox 360) and Little Endian (PC) automatically (most of the time).
  - Uses manifest.yaml for preserving file structure.

### Usage

#### GUI
Open the application and load a `.hnk` file.
- **Export**: Go to `Tools -> Export HNK...` to unpack the file.
- **Import**: Go to `Tools -> Import HNK...`, select a `manifest.yaml`, and save the new `.hnk` file.

#### CLI (Verification Tool)
You can also use the CLI for batch operations:
*Note: this tool is not finished and is mostly used for testing.*

```bash
# Export
dotnet run --project TorusTool.Verification -- export <hnk_file> <output_dir>

# Import
dotnet run --project TorusTool.Verification -- import <manifest_file> <output_hnk>
```

### Installation Notes

**macOS**:
If you encounter a "Malware detected" error, you can try to circumvent it by running:
```bash
xattr -d com.apple.quarantine TorusTool
```

*Hint: just install dotnet 9.0 and run:*
```bash
dotnet run --project TorusTool/TorusTool.csproj
```

## TODO
- Add export/import for fonts (rebuiling atlases). This is required for localization on non-latin scripts.
- Add export/import for string tables.
- Add audio export/import (this is probably the easiest part since the format is already well understood).
- Add model viewer.
- Add script reader.
- Add map viewer.

## License
MIT/WTFPL
I don't really care what you're going to do with this tool, but please do something nice.


## What if I need a specific feature for my project?

You can always open an issue or submit a pull request.

## What if the goal of this project?

I want to make it possible to translate games from Torus Games to other languages.


## Do you use AI tools?

Do better without it.