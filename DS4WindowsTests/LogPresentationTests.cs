using DS4Windows;
using DS4WinWPF;

namespace DS4WindowsTests;

[TestClass]
public class LogPresentationTests
{
    [DataTestMethod]
    [DataRow("OSC LISTENER STARTED AT PORT: 26760",
        (int)LogCategory.General)]
    [DataRow("VIIPER DualSense feedback reader stopped due to socket error.",
        (int)LogCategory.ViiperBackend)]
    [DataRow("Virtual controller output stays disabled: the experimental kernel driver notice was declined. It can be accepted later in Settings.",
        (int)LogCategory.DriverUsbip)]
    [DataRow("HidHide session setup failed for DualSense: Access denied",
        (int)LogCategory.HidHide)]
    [DataRow("DualSense audio passthrough failed to start: endpoint unavailable",
        (int)LogCategory.Audio)]
    [DataRow("Found Controller: 00:11:22:33:44:55 (Bluetooth) (DualSense).",
        (int)LogCategory.Controller)]
    [DataRow("DEBUG: Auto-Profile. LoadProfile Controller 1=Default  DeviceRule=All",
        (int)LogCategory.Profile)]
    public void ClassifiesRepresentativeProductLogMessages(string message,
        int expectedCategory)
    {
        Assert.AreEqual((LogCategory)expectedCategory,
            LogClassifier.Classify(message));
    }

    [TestMethod]
    public void MixedSubsystemMessagesPreferTheFailingDependency()
    {
        Assert.AreEqual(LogCategory.DriverUsbip, LogClassifier.Classify(
            "VIIPER could not detach usbip port 3 (stale import): access denied"));
        Assert.AreEqual(LogCategory.HidHide, LogClassifier.Classify(
            "VIIPER DualSense output is active but the physical DualSense could not be hidden with HidHide."));
    }

    [TestMethod]
    public void FilterReadsTheCachedCategoryWithoutReclassifying()
    {
        LogItem item = new LogItem
        {
            Message = "VIIPER backend ready",
            Category = LogCategory.General,
        };

        Assert.IsTrue(LogFilter.Matches(item, warningsOnly: false,
            LogCategory.General, string.Empty));
        Assert.IsFalse(LogFilter.Matches(item, warningsOnly: false,
            LogCategory.ViiperBackend, string.Empty));
    }

    [TestMethod]
    public void FilterComposesSeverityCategoryAndCaseInsensitiveSearch()
    {
        LogItem item = new LogItem
        {
            Message = "DualSense audio passthrough FAILED to start",
            Warning = true,
            Category = LogCategory.Audio,
        };

        Assert.IsTrue(LogFilter.Matches(item, warningsOnly: true,
            LogCategory.Audio, "failed"));

        item.Warning = false;
        Assert.IsFalse(LogFilter.Matches(item, warningsOnly: true,
            LogCategory.Audio, "failed"), "Severity must participate.");

        item.Warning = true;
        Assert.IsFalse(LogFilter.Matches(item, warningsOnly: true,
            LogCategory.Controller, "failed"), "Category must participate.");
        Assert.IsFalse(LogFilter.Matches(item, warningsOnly: true,
            LogCategory.Audio, "HidHide"), "Search must participate.");
    }

    [TestMethod]
    public void CopyFormattingIncludesTimeCategoryAndMessageInViewOrder()
    {
        DateTime firstTime = new DateTime(2026, 8, 2, 9, 10, 11);
        DateTime secondTime = firstTime.AddSeconds(2);
        LogItem[] items =
        [
            new LogItem
            {
                Datetime = firstTime,
                Category = LogCategory.ViiperBackend,
                Message = "VIIPER backend ready",
            },
            new LogItem
            {
                Datetime = secondTime,
                Category = LogCategory.Audio,
                Message = "DualSense audio passthrough started",
            },
        ];

        string expected =
            $"{firstTime:G} [VIIPER / backend] VIIPER backend ready" +
            Environment.NewLine +
            $"{secondTime:G} [Audio] DualSense audio passthrough started";

        Assert.AreEqual(expected, LogCopyFormatter.Format(items));
    }
}
