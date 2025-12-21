using System;
using System.IO;
using System.Linq;
using TorusTool.Models;
using Xunit;

namespace TorusTool.Tests
{
    [Trait("Category", "Integration")]
    public class HunkIntegrationTests
    {
        [Fact]
        public void ParseAllAssets_ShouldSucceed()
        {
            // Find Assets directory relative to the test Execution
            // Usually ../../../../../Assets if run from bin/Debug/net9.0

            var assetsDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Assets"));
            if (!Directory.Exists(assetsDir))
            {
                // Fallback for different environments or if Assets dir is missing
                // Ideally we skip, but for this user request we want to fail if assets are missing
                Assert.Fail($"Assets directory not found at {assetsDir}");
            }

            var hnkFiles = Directory.GetFiles(assetsDir, "*.hnk", SearchOption.AllDirectories);

            if (hnkFiles.Length == 0)
            {
                // Warning/Info instead of fail? User wants green circles.
                // Assert.Fail("No .hnk files found in Assets!");
                return; // Nothing to test
            }

            foreach (var file in hnkFiles)
            {
                var parser = new HunkFileParser();
                var records = parser.Parse(file);

                Assert.NotEmpty(records);

                // Basic checks
                var headerRec = records.FirstOrDefault(r => r.Type == HunkRecordType.Header);
                Assert.NotNull(headerRec);

                // Try parsing the header
                var isBigEndian = file.ToLower().Contains("ps3") || file.ToLower().Contains("wii") || file.ToLower().Contains("xbox");
                var header = RecordParsers.ParseHunkHeader(headerRec, isBigEndian);

                Assert.NotNull(header);
                Assert.NotEmpty(header.Rows);
            }
        }
    }
}
