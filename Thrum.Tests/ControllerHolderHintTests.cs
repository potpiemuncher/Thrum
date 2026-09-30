using DS4Windows;

namespace DS4WindowsTests
{
    [TestClass]
    public class ControllerHolderHintTests
    {
        [TestMethod]
        public void NamesKnownProgramsOnceIgnoringCase()
        {
            var programs = ControllerHolderHint.MatchKnownPrograms(new[]
            {
                "STEAM", "steamwebhelper", "chrome", "reWASDEngine", "reWASDTray", "",
            });

            CollectionAssert.AreEqual(new[] { "Steam", "reWASD" }, programs.ToArray());
        }

        [TestMethod]
        public void MessageNamesRunningPrograms()
        {
            string message = ControllerHolderHint.BuildMessage("DualSense (AA:BB)",
                new[] { "Steam", "DS4Windows" });

            StringAssert.StartsWith(message, "DualSense (AA:BB) is open in another program");
            StringAssert.Contains(message, "shared mode");
            StringAssert.Contains(message, "Steam and DS4Windows are running and may be using it. Close them,");
        }

        [TestMethod]
        public void MessageGivesExamplesWhenNothingKnownIsRunning()
        {
            string message = ControllerHolderHint.BuildMessage("DualSense (AA:BB)",
                System.Array.Empty<string>());

            StringAssert.Contains(message, "Close any program that uses controllers");
            StringAssert.EndsWith(message, "then reconnect the controller.");
            Assert.IsFalse(message.Contains("Administrator"));
        }
    }
}
