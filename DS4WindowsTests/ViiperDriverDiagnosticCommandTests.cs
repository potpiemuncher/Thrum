using System.Collections.Generic;
using DS4Windows;
using DS4WinWPF;

namespace DS4WindowsTests;

[TestClass]
public class ViiperDriverDiagnosticCommandTests
{
    [DataTestMethod]
    [DataRow("viiperdriverdiagnostic")]
    [DataRow("-viiperdriverdiagnostic")]
    public void ArgumentParser_RecognizesDiagnosticSwitch(string argument)
    {
        var parser = new ArgumentParser();

        parser.Parse(new[] { argument });

        Assert.IsTrue(parser.ViiperDriverDiagnostic);
        Assert.IsFalse(parser.HasErrors);
    }

    [TestMethod]
    public void ResolveUsbipExecutablePath_PrefersPathLikeViiperRuntime()
    {
        string expected = @"D:\tools\usbip.exe";
        var existing = new HashSet<string>
        {
            expected,
            @"C:\Program Files\USBip\usbip.exe",
        };

        string actual =
            ViiperDriverValidationCommand.ResolveUsbipExecutablePath(
                @"C:\Windows;D:\tools",
                @"C:\Program Files",
                @"C:\Program Files (x86)",
                existing.Contains);

        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void ResolveUsbipExecutablePath_UsesProgramFilesFallback()
    {
        string expected = @"C:\Program Files\USBip\usbip.exe";

        string actual =
            ViiperDriverValidationCommand.ResolveUsbipExecutablePath(
                string.Empty,
                @"C:\Program Files",
                @"C:\Program Files (x86)",
                _ => false);

        Assert.AreEqual(expected, actual);
    }
}