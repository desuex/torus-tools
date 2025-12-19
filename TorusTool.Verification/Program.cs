using System;
using TorusTool.Models;
using System.Linq;

namespace TorusTool.Verification;

class Program
{
    static void Main(string[] args)
    {
        VerifyFile(@"/Volumes/CORSAIR/TorusGames/Games/WIN/Monster High New Ghoul in School/HUNKFILES/Global.hnk");
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
        bool isBigEndian = testFile.Contains("PS3") || testFile.Contains("WII");
        Console.WriteLine($"Detecting Endianness: {(isBigEndian ? "Big Endian" : "Little Endian")}");

        try
        {
            // Build tree first to find specific nodes
            var records = parser.Parse(testFile);
            var tree = HunkFileTreeBuilder.BuildTree(records);

            Console.WriteLine("Searching for 'tokentofontmap'...");

            void InspectNode(HunkFileTreeNode node)
            {
                if (node.Name.ToLower().Contains("tokentofontmap") || node.Name.ToLower().Contains("ghoulhairstyles"))
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
                                    if (dt.ElementRawData.Any())
                                    {
                                        Console.WriteLine($"  -> Split into {dt.ElementRawData.Count} binary elements (Length={dt.ElementRawData[0].Length})");
                                        Console.WriteLine($"     - Element 0: {BitConverter.ToString(dt.ElementRawData[0])}");
                                    }
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
}
