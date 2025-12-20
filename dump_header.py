import sys

def dump_file(path, outfile):
    outfile.write(f"--- {path} ---\n")
    try:
        with open(path, 'rb') as f:
            data = f.read(128)
            for i in range(0, len(data), 16):
                chunk = data[i:i+16]
                hex_str = ' '.join(f"{b:02X}" for b in chunk)
                ascii_str = ''.join(chr(b) if 32 <= b <= 126 else '.' for b in chunk)
                outfile.write(f"{i:04X}: {hex_str:<48} | {ascii_str}\n")
    except Exception as e:
        outfile.write(f"Error: {e}\n")

if __name__ == "__main__":
    with open(r"d:\TorusGames\TorusTool\header_dump.txt", "w") as f:
        dump_file(r"d:\TorusGames\Games\3DS\EXT\RomFS\packfile.dat", f)
        dump_file(r"d:\TorusGames\Games\3DS\RomFS\hunkfiles\languageselection.hnk", f)
