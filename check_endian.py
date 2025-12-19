
import sys

def search_hex(filepath, hex_str):
    search_bytes = bytes.fromhex(hex_str)
    with open(filepath, 'rb') as f:
        data = f.read() # Might be dangerous if file is huge, but let's try.
        # If file is huge, we should chunk.
        
        offset = data.find(search_bytes)
        if offset != -1:
            print(f"Found {hex_str} at {offset}")
            # Header is Size (4) + Type (4). 
            # If we searched for Type, the record starts at offset - 4.
            # Data starts at offset + 4.
            
            # Read first few bytes of data (FontDescriptor Platform Header) (16 bytes)
            # offset points to Type (0x40070 etc).
            # The pattern provided was 87300400 -> Type 0x43087 (LE).
            
            data_start = offset + 4
            header_bytes = data[data_start:data_start+16]
            print(f"Data Header: {header_bytes.hex()}")
        else:
            print(f"Not found {hex_str}")

if __name__ == "__main__":
    search_hex(sys.argv[1], "00043087") # Try BE first? No, we agreed container is LE?
    search_hex(sys.argv[1], "87300400") # LE
