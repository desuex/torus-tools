using System;
using TorusTool.Models;

namespace TorusTool.Verification;

class Program
{
    static void Main(string[] args)
    {
        VerifyFile(@"d:\TorusGames\Games\WIN\Monster High New Ghoul in School\HUNKFILES\Global.hnk");
        VerifyFile(@"d:\TorusGames\Games\WIN\Monster High New Ghoul in School\HUNKFILES\Localisation_en_US.hnk");
    }

    static void VerifyFile(string testFile)
    {
        Console.WriteLine($"\nVerifying HunkFileParser on: {testFile}");

        if (!System.IO.File.Exists(testFile))
        {
            Console.WriteLine("Error: File not found!");
            return;
        }

        var parser = new HunkFileParser();
        int count = 0;
        try
        {
            foreach (var record in parser.Parse(testFile))
            {
                if (count++ < 5)
                     Console.WriteLine($"Record #{count}: Type={record.TypeDescription} (0x{((int)record.Type):X}), Size={record.Size}");
                
                if (record.Type == HunkRecordType.Header)
                {
                    Console.WriteLine("  -> Found Header! Parsing...");
                    var header = RecordParsers.ParseHunkHeader(record);
                    if (header != null)
                    {
                        Console.WriteLine("  -> Parsed Header data.");
                        for(int i=0; i<8; i++)
                        {
                            Console.WriteLine($"     Q{i}: Name='{header.Rows[i].Name}', Size={header.Rows[i].MaxSize}, Type={header.Rows[i].Type} ({header.Rows[i].TypeDescription})");
                        }
                    }
                }
                
                if (record.Type == HunkRecordType.TSEStringTableMain)
                {
                    Console.WriteLine("  -> Found StringTable! Parsing...");
                    var table = RecordParsers.ParseStringTable(record);
                    if (table != null)
                    {
                        Console.WriteLine($"  -> Parsed {table.Rows.Count} strings.");
                        for (int i = 0; i < Math.Min(5, table.Rows.Count); i++)
                        {
                            Console.WriteLine($"     [{i}] {table.Rows[i].Value}");
                        }
                    }
                }
                
                if (record.Type == HunkRecordType.TSEFontDescriptorData)
                {
                    Console.WriteLine("  -> Found FontDescriptor! Parsing...");
                    var fd = RecordParsers.ParseFontDescriptor(record);
                    if (fd != null)
                    {
                        Console.WriteLine($"  -> Parsed FontDescriptor. Cnt1={fd.Header.Cnt1}, Cnt2={fd.Header.Cnt2}");
                        Console.WriteLine($"  -> Sign: " + BitConverter.ToString(fd.Header.Signature));
// Pattern Analysis Logic
                        Console.WriteLine("  -> Running Pattern Analysis...");
                        for (int i = 0; i < fd.Tuples.Count; i++)
                        {
                            if (i >= fd.Rows.Count) break;
                            var tuple = fd.Tuples[i];
                            var row = fd.Rows[i];
                            
                            // Check for the transition the user mentioned or interesting patterns
                            // User mentioned "FF-00-40-01..."
                            // Let's print rows that look like transitions or just a sample stride to see the trend
                            // Also check correlation between Row.X (or parts of it) and CharId
                            
                            short x = row.X;
                            ushort charId = tuple.CharId;
                            
                            // Decode X assumption: (Flags << 12) | Value??
                            // Or is it just a value?
                            
                            // Let's print if i is small (sequential part) or around the user's "random" start
                            // Or if X matches CharId
                            
                            bool isInteresting = (i < 20) || (x == 0x0152) || (row.HexDisplay.StartsWith("52-01"));
                            
                            if (isInteresting || i % 50 == 0) // sampling
                            {
                                Console.WriteLine($"    [{i:D3}] Char={tuple.CharDisplay} (0x{charId:X4}) | X=0x{x:X4} ({x}) | W={row.Width} | Aux={row.Aux} | Row={row.HexDisplay}");
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }

        // ... existing parser logic ...

        Console.WriteLine($"Total verified records: {count}");

        // Tree Builder Verification
        Console.WriteLine("\nVerifying Tree Builder...");
        try 
        {
             // Parse all for tree verify
             parser = new HunkFileParser();
             var allRecords = parser.Parse(testFile);
             var tree = HunkFileTreeBuilder.BuildTree(allRecords);
             Console.WriteLine($"Tree Root Nodes: {tree.Count}");
             
             foreach(var node in tree)
             {
                 PrintNode(node, 0);
                 
                 // RenderSprite Inspection
                 if (node.Name == "RenderSprite" && node.IsFolder)
                 {
                     Console.WriteLine("\n[RenderSprite Inspection]");
                     foreach(var child in node.Children)
                     {
                         // User mentioned "Font_UI", let's check it.
                         // But also print generic info for others to see if they share type.
                         Console.WriteLine($"  Node: {child.Name}");
                         foreach(var rec in child.Records)
                         {
                             // We don't know the type enum yet, so print raw int
                             Console.WriteLine($"    Record Type: 0x{((int)rec.Type):X} ({rec.Type}), Size: {rec.Size}");
                             if (rec.Size > 0 && rec.Size < 64) 
                             {
                                 Console.WriteLine($"    Data: {BitConverter.ToString(rec.RawData)}");
                             }
                             else if (rec.Size >= 64)
                             {
                                 // Print first 64 bytes
                                 var sample = new byte[64];
                                 Array.Copy(rec.RawData, sample, 64);
                                 Console.WriteLine($"    Data (Head): {BitConverter.ToString(sample)}...");
                             }
                         }
                     }
                 }
             }
        }
        catch(Exception ex)
        {
             Console.WriteLine($"Tree Error: {ex.Message}");
        }
    }

    static void PrintNode(HunkFileTreeNode node, int depth)
    {
        var indent = new string(' ', depth * 2);
        Console.WriteLine($"{indent}- {node.Name} (IsFolder={node.IsFolder}, Children={node.Children.Count}, Records={node.Records.Count})");
        
        // Print first 5 children only to avoid spam
        int c = 0;
        foreach(var child in node.Children)
        {
            if (c++ > 5) { Console.WriteLine($"{indent}  ..."); break; }
            PrintNode(child, depth + 1);
        }
    }
}
