using System;
using System.IO;

namespace DS4WindowsTests
{
    [TestClass]
    public class ViiperV5ContractTests
    {
        [TestMethod]
        public void OfficialV006PersonasAreTriedBeforeLegacyFallbacks()
        {
            string source = File.ReadAllText(Path.Combine(FindRepositoryRoot(),
                "DS4Windows", "DS4Control", "Viiper", "ViiperOutDevice.cs"));

            AssertBefore(source, "dualsenseaudioonlyduplexv5",
                "dualsenseaudioonlyduplexv4");
            AssertBefore(source, "dualsensecombinedaudioduplexv5",
                "dualsensecombinedaudioduplexv4");
            AssertBefore(source, "dualsenseedgecombinedaudioduplexv5",
                "dualsenseedgecombinedaudioduplexv4");
            StringAssert.Contains(source,
                "private const byte ViiperStreamFrameVersionV5 = 0x05;");
            StringAssert.Contains(source,
                "private const byte FrameVersionV5 = 0x05;");
        }

        private static void AssertBefore(string source, string first,
            string second)
        {
            int firstIndex = source.IndexOf(first, StringComparison.Ordinal);
            int secondIndex = source.IndexOf(second, StringComparison.Ordinal);
            Assert.IsTrue(firstIndex >= 0, $"Missing VIIPER persona '{first}'.");
            Assert.IsTrue(secondIndex >= 0, $"Missing fallback persona '{second}'.");
            Assert.IsTrue(firstIndex < secondIndex,
                $"VIIPER persona '{first}' must be tried before '{second}'.");
        }

        private static string FindRepositoryRoot()
        {
            DirectoryInfo directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null)
            {
                if (File.Exists(Path.Combine(directory.FullName,
                        "DS4WindowsWPF.sln")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            throw new DirectoryNotFoundException(
                "Could not find the Thrum repository root.");
        }
    }
}
