using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using TorusTool.Models;

namespace TorusTool.IO;

public class PackfileReader
{
    private const uint MAGIC_PAK = 0x004B4150; // "PAK\0"
    
    public static Packfile Read(string path)
    {
        var packfile = new Packfile();
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        // 3DS is Little Endian
        using var reader = new TorusBinaryReader(fs, isBigEndian: false);

        uint magic = reader.ReadUInt32();
        if (magic != MAGIC_PAK)
        {
            throw new InvalidDataException($"Invalid magic: {magic:X8}, expected PAK\\0");
        }

        uint fileCount = reader.ReadUInt32();
        
        for (int i = 0; i < fileCount; i++)
        {
            var entry = new PackfileEntry();
            entry.NameCrc = reader.ReadUInt32();
            entry.Offset = reader.ReadUInt32();
            entry.Size = reader.ReadUInt32();

            // We can peek at the data to determine extension, but verifying every file might be slow?
            // The BMS script does 'goto offset', checks !ZLS, decompresses, checks subtype.
            // For a 'Viewer' we probably want to determine extension lazily or now?
            // Let's do it now for the list to look nice, assuming the packfile isn't massive.
            
            long returnPos = reader.Position;
            try
            {
                reader.Seek(entry.Offset, SeekOrigin.Begin);
                string sign = reader.ReadStringFixed(4);
                if (sign == "!ZLS")
                {
                    // It's compressed
                    // Peek decompression
                    // !ZLS (4) + Size (4) + ZLibStream
                    // !ZLS (4) + Size (4) + ZLibStream
                    
                    // We need at least 8 bytes for simple check (maxa or hnk header are at 0x0)
                    // If we just need first 8 bytes of uncompressed, we can try to decompress a tiny chunk?
                    // ZLib stream might complain if truncated.
                    // But we can try to read a small buffer.
                    
                    // Actually, for massive packfiles, doing this for ALL entries might be slow.
                    // But for 3DS games (~2000 files?), it might take a few seconds.
                    // Let's implement a quick helper to get first N bytes of uncompressed.
                    
                    byte[] zlsHeader = reader.ReadBytes(4); // Uncompressed size
                    // We just continue reading generic blob
                    // Reversing seek to get the stream start
                    reader.Seek(-4, SeekOrigin.Current); 
                    
                    // Read a small chunk of compressed data to feed ZLib
                    int compressedPeekSize = (int)Math.Min(entry.Size - 8, 512); 
                    byte[] compressedChunk = reader.ReadBytes(compressedPeekSize);
                    
                    string detectedExt = "zdat";
                    
                    try 
                    {
                        using var msIn = new MemoryStream(compressedChunk);
                        using var msOut = new MemoryStream();
                        using (var zlib = new ZLibStream(msIn, CompressionMode.Decompress, leaveOpen: true))
                        {
                            byte[] buffer = new byte[16];
                            // Try to read enough bytes
                            // Note: ZLibStream might fail on incomplete stream even for read?
                            // QuickBMS 'clog' handles it. .NET ZLibStream might throw 'Block length does not match..' or similar if stream ends abruptly.
                            // But usually Read() works until it needs more input.
                            // However, we only have a chunk.
                            // Let's hope Deflate allows partial input processing for the start of stream.
                            // If this fails often, we might skip it or use a tolerant decompression.
                            // ALTERNATIVELY: Just accept 'zdat' until unpacked.
                            // BUT User asked for detection.
                            
                            // Let's rely on standard read. If it throws, fallback to zdat.
                            try {
                                int read = zlib.Read(buffer, 0, buffer.Length);
                                if (read >= 4)
                                {
                                    if (buffer[0] == 'm' && buffer[1] == 'a' && buffer[2] == 'x' && buffer[3] == 'a')
                                    {
                                        detectedExt = "files";
                                    }
                                    else 
                                    {
                                        // Check for HNK: 0x40070 (LE: 70 00 04 00)
                                        // or 00 00 00 01 (Version?) logic
                                        // The BMS script checks SUBTYPE at 0x4 (bytes 4-7)
                                        if (read >= 8)
                                        {
                                            uint subtype = BitConverter.ToUInt32(buffer, 4); 
                                            // BMS: if SUBTYPE == 0x00040070 -> hnk
                                            if (subtype == 0x00040070) detectedExt = "hnk";
                                        }
                                    }
                                }
                            }
                            catch { /* Chunk might be too small or ZLib strict */ }
                        }
                    }
                    catch { }
                    
                    entry.SuggestedExtension = detectedExt;
                }
                else
                {
                     // Uncompressed check
                     if (sign.StartsWith("maxa")) entry.SuggestedExtension = "files";
                     else 
                     {
                         // Check bytes 4-7 for hnk subtype
                         // We are at offset+4
                         byte[] extra = reader.ReadBytes(4);
                         if (extra.Length == 4)
                         {
                             uint subtype = BitConverter.ToUInt32(extra, 0);
                             if (subtype == 0x00040070) entry.SuggestedExtension = "hnk";
                             else entry.SuggestedExtension = "dat";
                         }
                         else entry.SuggestedExtension = "dat";
                     }
                }
            }
            catch
            {
                // potential read error
            }
            finally
            {
                reader.Seek(returnPos, SeekOrigin.Begin);
            }
            
            packfile.Entries.Add(entry);
        }

        return packfile;
    }
    
    public static byte[] ExtractFile(string packPath, PackfileEntry entry)
    {
        using var fs = new FileStream(packPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var reader = new TorusBinaryReader(fs, isBigEndian: false);
        
        reader.Seek(entry.Offset, SeekOrigin.Begin);
        byte[] rawData = reader.ReadBytes((int)entry.Size); // Safe-ish cast for 3DS file sizes

        if (rawData.Length > 4 && Encoding.ASCII.GetString(rawData, 0, 4) == "!ZLS")
        {
            // Decompress
            // !ZLS (4 bytes)
            // Uncompressed Size (4 bytes)
            // Data...
            
            int uncompressedSize = BitConverter.ToInt32(rawData, 4);
            // 3DS is LE, BitConverter uses system endianness (usually LE on Windows), so this is fine. 
            // If running on BE system this would be wrong, but TorusTool runs on PC (LE).
            
            // DeflateStream expects just the deflate data.
            // ZLIB usually has header 0x78 0x9C (default) or other.
            // PROBABLE: It is ZLIB wrapped. DeflateStream might fail if it has headers?
            // .NET DeflateStream is raw deflate (RFC 1951). 
            // .NET ZLibStream (NET 6) handles ZLib (RFC 1950).
            
            // Assume 8 byte header (!ZLS + size) is skipped.
            // The BMS command `clog MEMORY_FILE OFFSET SIZE XSIZE` in QuickBMS defaults to ZLIB usually unless specified otherwise?
            // "If the compression is not specified... ZLIB is used."
            
            using var msInput = new MemoryStream(rawData, 8, rawData.Length - 8);
            using var msOutput = new MemoryStream(uncompressedSize);
            
            // Attempt ZLibStream first
            try 
            {
                 using var zlib = new ZLibStream(msInput, CompressionMode.Decompress);
                 zlib.CopyTo(msOutput);
            }
            catch
            {
                // Fallback or error?
                /* 
                   If ZLibStream fails, it might be raw Deflate.
                   msInput.Position = 0;
                   using var deflate = new DeflateStream(msInput, CompressionMode.Decompress);
                   deflate.CopyTo(msOutput);
                */
                throw;
            }
            
            return msOutput.ToArray();
        }
        
        return rawData;
    }
}
