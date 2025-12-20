using System;
using TorusTool.Models;
using System.Linq;

namespace TorusTool.Verification;

class Program
{
    static void Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Usage:");
            Console.WriteLine("  verify <hnk_file>");
            Console.WriteLine("  export <hnk_file> <output_dir>");
            Console.WriteLine("  import <manifest_file> <output_hnk>");
            return;
        }

        string command = args[0].ToLower();
        try
        {
            switch (command)
            {
                case "verify":
                    if (args.Length < 2) { Console.WriteLine("Missing file path"); return; }
                    VerifyFile(args[1]);
                    break;
                case "export":
                    if (args.Length < 3) { Console.WriteLine("Missing arguments"); return; }
                    Console.WriteLine($"Exporting {args[1]} to {args[2]}...");
                    new HunkExporter().Export(args[1], args[2]);
                    Console.WriteLine("Export complete.");
                    break;
                case "import":
                    if (args.Length < 3) { Console.WriteLine("Missing arguments"); return; }
                    Console.WriteLine($"Importing {args[1]} to {args[2]}...");
                    new HunkImporter().Import(args[1], args[2]);
                    Console.WriteLine("Import complete.");
                    break;
                case "testpck":
                    if (args.Length < 2) { Console.WriteLine("Missing file path"); return; }
                    VerifyPackfile(args[1]);
                    break;
                default:
                    Console.WriteLine("Unknown command.");
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
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
        bool isBigEndian = testFile.ToLower().Contains("ps3") || testFile.ToLower().Contains("wii") || testFile.ToLower().Contains("xbox");
        Console.WriteLine($"Detecting Endianness: {(isBigEndian ? "Big Endian" : "Little Endian")}");

        try
        {
            // Build tree first to find specific nodes
            var records = parser.Parse(testFile);
            var tree = HunkFileTreeBuilder.BuildTree(records);

            Console.WriteLine("Searching for 'tokentofontmap' or 'font'...");

            void InspectNode(HunkFileTreeNode node)
            {
                if (node.Name.ToLower().Contains("font") || node.Name.ToLower().Contains("global") || node.Name.ToLower().Contains("tokentofontmap"))
                {
                    Console.WriteLine($"\n--- Inspecting Node: {node.Name} ---");
                    // Print all records in this node to see structure
                    foreach (var r in node.Records)
                    {
                        Console.WriteLine($"  Record: {r.Type} (Size: {r.Size})");

                        if (r.Type == HunkRecordType.TSEDataTableData1 ||
                            r.Type == HunkRecordType.TSEDataTableData2)
                        {
                            var dt = RecordParsers.ParseDataTable(r, isBigEndian);
                            if (dt != null)
                            {
                                Console.WriteLine($"  -> Parsed DataTable! Count={dt.Count}, DataSize={dt.DataSize}, BodyLen={dt.Body.Length}");
                                if (dt.StringValues.Any())
                                {
                                    Console.WriteLine($"  -> Found {dt.StringValues.Count} strings:");
                                    foreach (var s in dt.StringValues.Take(5)) Console.WriteLine($"     - {s}");
                                }
                                else
                                {
                                    Console.WriteLine("  -> No strings found (Binary Table).");
                                }
                            }
                        }
                        else if (r.Type == HunkRecordType.TSEFontDescriptorData)
                        {
                            var fd = RecordParsers.ParseFontDescriptor(r, isBigEndian);
                            if (fd != null)
                            {
                                Console.WriteLine($"  -> Parsed FontDescriptor!");
                                Console.WriteLine($"     Header: Flag={fd.PlatformHeader.FlagsOrVersion}, Height={fd.PlatformHeader.EmOrLineHeight}, Count={fd.PlatformHeader.GlyphCount}");
                                Console.WriteLine($"     FileHdr: PreGlyphs={fd.FileHeader.PreGlyphOffset}, Glyphs={fd.FileHeader.GlyphTableOffset}");
                                if (fd.PreGlyphs.Count > 0)
                                {
                                    var first = fd.PreGlyphs[0];
                                    Console.WriteLine($"     First PreGlyph: {first.V0}, {first.V1}, {first.V2}, {first.V3}");
                                }
                            }
                        }
                    }
                }

                foreach (var child in node.Children) InspectNode(child);
            }

            foreach (var node in tree) InspectNode(node);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
    }

    static void VerifyPackfile(string path)
    {
        Console.WriteLine($"Testing Packfile: {path}");
        try
        {
            var pack = TorusTool.IO.PackfileReader.Read(path);
            Console.WriteLine($"Found {pack.Entries.Count} entries.");
            
            int limit = 5;
            foreach (var entry in pack.Entries.Take(limit))
            {
                Console.WriteLine($"Entry: {entry.DisplayName} | Offset: {entry.Offset:X} | Size: {entry.Size} | Ext: {entry.SuggestedExtension}");
            }
            
            if (pack.Entries.Any())
            {
                var first = pack.Entries.First();
                Console.WriteLine($"Extracting first entry: {first.DisplayName}...");
                var bytes = TorusTool.IO.PackfileReader.ExtractFile(path, first);
                Console.WriteLine($"Extracted {bytes.Length} bytes.");
                string head = BitConverter.ToString(bytes.Take(Math.Min(bytes.Length, 16)).ToArray());
                Console.WriteLine($"Head: {head}");
            }
        }
        catch (Exception ex)
        {
             Console.WriteLine($"Error: {ex}");
        }
    }
}
