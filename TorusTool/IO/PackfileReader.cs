using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
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
        using var reader = new TorusBinaryReader(fs, isBigEndian: false);

        // 1. HEADER
        uint magic = reader.ReadUInt32();
        if (magic != MAGIC_PAK)
        {
             throw new InvalidDataException($"Invalid magic: {magic:X8}, expected PAK\\0");
        }

        uint headerSize = reader.ReadUInt32();
        uint folderCount = reader.ReadUInt32();
        
        // 2. DIRECTORY TABLE
        reader.Seek(12, SeekOrigin.Begin);
        var directories = new List<(string Name, uint FirstFilePtr)>();

        for (int i = 0; i < folderCount; i++)
        {
            long dirStart = reader.Position;
            uint id = reader.ReadUInt32();
            uint firstFilePtr = reader.ReadUInt32();
            string dirName = ReadNullTerminatedString(reader);
            directories.Add((dirName, firstFilePtr));
            
            // Align
            long currentPos = reader.Position;
            long alignedPos = (currentPos + 3) & ~3;
            if (alignedPos != currentPos) reader.Seek(alignedPos, SeekOrigin.Begin);
        }

        // 3. SCAN FILES (Phase 1)
        // Collect metadata to find Pointer Table bounds
        var unprocessedEntries = new List<UnprocessedEntry>();
        var processedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        
        uint minPtrAddr = uint.MaxValue;
        uint maxPtrAddr = 0;

        foreach (var dir in directories)
        {
            uint nextFilePtr = dir.FirstFilePtr;
            while (nextFilePtr != 0)
            {
                 // Safety break for loops
                 if (nextFilePtr >= headerSize && headerSize > 0) 
                 {
                     // If pointer is outside header, it's suspicious, but maybe valid? 
                     // HeaderSize is total size of header section.
                     // File entries should be within header.
                 }

                 reader.Seek(nextFilePtr, SeekOrigin.Begin);
                 
                 uint nextPtrVal = reader.ReadUInt32();
                 uint totalOriginalSize = reader.ReadUInt32();
                 uint blockCount = reader.ReadUInt32();
                 uint pointerTableAddr = reader.ReadUInt32(); // Absolute Address of the pointer
                 
                 string fileName = ReadNullTerminatedString(reader);
                 string fullPath = $"{dir.Name}/{fileName}".Replace('\\', '/');

                 if (!processedPaths.Contains(fullPath))
                 {
                     processedPaths.Add(fullPath);
                     unprocessedEntries.Add(new UnprocessedEntry 
                     { 
                        FullPath = fullPath, 
                        Name = fileName,
                        PointerTableAddr = pointerTableAddr,
                        BlockCount = blockCount,
                        TotalOriginalSize = totalOriginalSize
                     });

                     if (pointerTableAddr < minPtrAddr) minPtrAddr = pointerTableAddr;
                     // The last pointer used by this file is at Addr + (BlockCount-1)*4
                     // We need the table to cover up to that.
                     // Also we need the NEXT entry for size calc, so effectively + BlockCount*4
                     uint endAddr = pointerTableAddr + (blockCount * 4); 
                     if (endAddr > maxPtrAddr) maxPtrAddr = endAddr;
                 }
                 
                 // Cycle check?
                 if (nextPtrVal == nextFilePtr) break;
                 
                 nextFilePtr = nextPtrVal;
            }
        }
        
        if (unprocessedEntries.Count == 0) return packfile;

        // 4. READ POINTER TABLE
        // Read from minPtrAddr to maxPtrAddr + some margin?
        // We usually need the "next" pointer for size calculation.
        // So read one extra uint if possible.
        
        long tableStart = minPtrAddr;
        long tableEnd = maxPtrAddr + 4; // Read one extra for the last file's size boundary
        
        // Safety clamp
        if (tableEnd > fs.Length) tableEnd = fs.Length;
        if (tableStart >= tableEnd) 
        {
            // Weird?
            return packfile;
        }
        
        int tableSize = (int)(tableEnd - tableStart);
        reader.Seek(tableStart, SeekOrigin.Begin);
        
        // Read as array
        // Index 0 corresponds to tableStart
        int entryCount = tableSize / 4;
        uint[] pointerTable = new uint[entryCount];
        for (int i = 0; i < entryCount; i++)
        {
            pointerTable[i] = reader.ReadUInt32();
        }

        // 5. RESOLVE FILES
        foreach (var u in unprocessedEntries)
        {
            // Calculate index relative to our read table
            long relativeOffset = u.PointerTableAddr - tableStart;
            if (relativeOffset < 0 || relativeOffset % 4 != 0) continue; // Should not happen
            
            int index = (int)(relativeOffset / 4);
            
            if (index < pointerTable.Length)
            {
                uint dataOffset = pointerTable[index];
                
                // Determine size
                // Logic: Size = NextOffset - CurrentOffset
                // NextOffset is usually table[index+1].
                
                uint nextOffset = 0;
                // Use the pointer after the LAST block of this file
                int nextIndex = index + (int)u.BlockCount;
                
                if (nextIndex < pointerTable.Length)
                {
                    nextOffset = pointerTable[nextIndex];
                }
                else
                {
                    // End of Heap
                    nextOffset = (uint)fs.Length;
                }
                
                // If nextOffset is 0, it might be end of file or invalid. 
                // Scan forward for non-zero? Or assume EOF?
                if (nextOffset == 0) nextOffset = (uint)fs.Length; 
                
                if (dataOffset != 0 && nextOffset >= dataOffset)
                {
                    uint size = nextOffset - dataOffset;
                    
                    // Sanity check
                    if (size > 200 * 1024 * 1024) 
                    {
                        System.Diagnostics.Debug.WriteLine($"[Warning] Huge size {size} for {u.FullPath}. Skipping.");
                        continue;
                    }

                    var entry = new PackfileEntry
                    {
                        FullPath = u.FullPath,
                        Offset = dataOffset,
                        Size = size,
                        OriginalSize = u.TotalOriginalSize,
                        PointerIndex = index, // Relative index? Or absolute? Not used much.
                        SuggestedExtension = Path.GetExtension(u.Name).TrimStart('.'),
                        IsCompressed = false
                    };

                    // Peek !CMP
                     try 
                     {
                        if (dataOffset + 4 <= fs.Length)
                        {
                            reader.Seek(dataOffset, SeekOrigin.Begin);
                            byte[] magicCheck = reader.ReadBytes(4);
                            if (magicCheck.Length == 4 && Encoding.ASCII.GetString(magicCheck) == "!CMP")
                            {
                                entry.IsCompressed = true;
                            }
                        }
                     } catch {}

                    packfile.Entries.Add(entry);
                }
            }
        }

        return packfile;
    }

    private class UnprocessedEntry
    {
        public string FullPath;
        public string Name;
        public uint PointerTableAddr;
        public uint BlockCount;
        public uint TotalOriginalSize;
    }
    
    // ... Helper methods ...
    private static string ReadNullTerminatedString(TorusBinaryReader reader)
    {
        var sb = new StringBuilder();
        char c;
        while ((c = (char)reader.ReadByte()) != '\0')
        {
            sb.Append(c);
        }
        return sb.ToString();
    }

    public static byte[] ExtractFile(string packPath, PackfileEntry entry)
    {
        // ... (Same as before) ...
        using var fs = new FileStream(packPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var reader = new BinaryReader(fs);

        reader.BaseStream.Seek(entry.Offset, SeekOrigin.Begin);
        byte[] rawData = reader.ReadBytes((int)entry.Size);

        if (rawData.Length > 4 && Encoding.ASCII.GetString(rawData, 0, 4) == "!CMP")
        {
             int uncompressedSize = BitConverter.ToInt32(rawData, 4);
             try 
             {
                 using var msInput = new MemoryStream(rawData, 8, rawData.Length - 8);
                 using var msOutput = new MemoryStream(uncompressedSize);
                 using var zlib = new ZLibStream(msInput, CompressionMode.Decompress);
                 zlib.CopyTo(msOutput);
                 return msOutput.ToArray();
             }
             catch
             {
                 return rawData;
             }
        }
        return rawData;
    }
}
