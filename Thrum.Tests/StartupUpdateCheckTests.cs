using System;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DS4WindowsTests;

/// <summary>
/// Structural guard on the unsolicited startup update check in
/// <c>MainWindow.LateChecks</c>. Issue #97: with no network it opened a modal
/// "Failed to retrieve latest version" box that blocked the window from
/// closing until dismissed. Nobody asked for that check, so its failure must
/// be a log line. The manual Check for updates button is allowed its dialog,
/// because there the person is waiting for an answer.
/// </summary>
[TestClass]
public class StartupUpdateCheckTests
{
    private static string source;

    [ClassInitialize]
    public static void LoadSource(TestContext context)
    {
        string path = FindMainWindowSource();
        Assert.IsNotNull(path,
            "Thrum/DS4Forms/MainWindow.xaml.cs was not found above " +
            AppContext.BaseDirectory + ".");
        source = File.ReadAllText(path);
    }

    [TestMethod]
    public void TheStartupUpdateCheckNeverOpensADialogOnFailure()
    {
        string lateChecks = MethodBody("LateChecks");

        Assert.IsFalse(lateChecks.Contains("FailedToRetrieveLatestVersion"),
            "the unsolicited startup check must not show the failure dialog");
        Assert.IsFalse(Regex.IsMatch(lateChecks, @"MessageBox\.Show"),
            "the unsolicited startup check must not open any message box");
        Assert.IsTrue(lateChecks.Contains("AppLogger.LogToGui"),
            "a failed startup check has to be visible somewhere: the log");
    }

    [TestMethod]
    public void TheManualCheckStillAnswersThePersonWhoAskedForIt()
    {
        string manual = MethodBody("CheckUpdatesBtn_Click");

        Assert.IsTrue(manual.Contains("FailedToRetrieveLatestVersion"),
            "a check the person asked for has to report its failure to them");
        Assert.IsTrue(manual.Contains("UpToDate"),
            "a check the person asked for has to report a negative result too");
    }

    private static string MethodBody(string name)
    {
        Match start = Regex.Match(source,
            @"\b" + Regex.Escape(name) + @"\s*\([^)]*\)\s*\r?\n\s*\{");
        Assert.IsTrue(start.Success, "method " + name + " not found");

        int depth = 0;
        for (int i = start.Index + start.Length - 1; i < source.Length; i++)
        {
            if (source[i] == '{') depth++;
            else if (source[i] == '}' && --depth == 0)
            {
                return source.Substring(start.Index, i - start.Index + 1);
            }
        }

        Assert.Fail("method " + name + " has no closing brace");
        return null;
    }

    private static string FindMainWindowSource()
    {
        DirectoryInfo directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            string candidate = Path.Combine(directory.FullName, "Thrum",
                "DS4Forms", "MainWindow.xaml.cs");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return null;
    }
}
