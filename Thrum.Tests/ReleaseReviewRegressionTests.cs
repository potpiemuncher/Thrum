/*
Thrum
Copyright (C) 2026  Thrum contributors

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/

using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.Sockets;
using DS4Windows;
using DS4WinWPF;
using DS4WinWPF.DS4Control.DTOXml;
using DS4WinWPF.DS4Forms.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DS4WindowsTests
{
    /// <summary>
    /// Pins the fixes from the October 2026 pre-release review where a cheap
    /// unit test can hold them in place.
    /// </summary>
    [TestClass]
    public class ReleaseReviewRegressionTests
    {
        [TestMethod]
        public void LastCheckedRoundTripsInEveryCulture()
        {
            CultureInfo saved = CultureInfo.CurrentCulture;
            try
            {
                foreach (string culture in new[] { "en-US", "en-GB", "de-DE", "fr-FR", "ja-JP" })
                {
                    CultureInfo.CurrentCulture = new CultureInfo(culture);
                    var written = new AppSettingsDTO();
                    written.MapFrom(new BackingStore
                    {
                        lastChecked = new DateTime(2026, 10, 3, 14, 5, 9),
                    });

                    var read = new AppSettingsDTO { LastCheckString = written.LastCheckString };
                    Assert.AreEqual(new DateTime(2026, 10, 3, 14, 5, 9), read.LastChecked,
                        $"LastChecked did not survive a save and load in {culture}");
                }
            }
            finally
            {
                CultureInfo.CurrentCulture = saved;
            }
        }

        [TestMethod]
        public void LastCheckedFromEarlierBuildsStillReads()
        {
            var read = new AppSettingsDTO { LastCheckString = "12/05/2023 00:24:15" };
            Assert.AreEqual(new DateTime(2023, 12, 5, 0, 24, 15), read.LastChecked);
        }

        [TestMethod]
        public void SkippedReleaseTagSurvivesLoad()
        {
            var store = new BackingStore { lastVersionChecked = "v0.9.0-beta.3" };
            store.PostProcessLoad();

            Assert.AreEqual("v0.9.0-beta.3", store.lastVersionChecked,
                "Skip this version must persist across a restart");
            Assert.IsTrue(ReleaseChannelPolicy.IsSkippedRelease("v0.9.0-beta.3",
                store.lastVersionChecked));
        }

        [TestMethod]
        public void CurveEditorAcceptsOnlyFourNumbers()
        {
            Assert.AreEqual("0.25,0.10,0.25,1.00",
                ProfileSettingsViewModel.NormalizeCurveDefinitionForEditor(" 0.25, 0.10, 0.25, 1.00 "));
            Assert.AreEqual("0,-0.5,1,1.5",
                ProfileSettingsViewModel.NormalizeCurveDefinitionForEditor("0,-0.5,1,1.5"));

            // A quote plus a tab used to end the browser argument and add switches.
            Assert.AreEqual(string.Empty,
                ProfileSettingsViewModel.NormalizeCurveDefinitionForEditor(
                    "0,0,1,1\"\t--gpu-launcher=calc\t\""));
            Assert.AreEqual(string.Empty,
                ProfileSettingsViewModel.NormalizeCurveDefinitionForEditor("0,0,1"));
            Assert.AreEqual(string.Empty,
                ProfileSettingsViewModel.NormalizeCurveDefinitionForEditor(null));
        }

        [TestMethod]
        public void GamepadOnlyVirtualDualSenseStillDispatchesGameFeedback()
        {
            // Audio endpoints off: the V5 gamepad-only persona frames its output
            // state but has no speaker. Its rumble, triggers and lightbar must
            // still be dispatched.
            Assert.IsTrue(ViiperOutDevice.FeedbackDispatchNeeded(
                directSpeaker: false, feedbackIsFramed: true));
            Assert.IsTrue(ViiperOutDevice.FeedbackDispatchNeeded(
                directSpeaker: true, feedbackIsFramed: false));
            Assert.IsFalse(ViiperOutDevice.FeedbackDispatchNeeded(
                directSpeaker: false, feedbackIsFramed: false));
        }

        [TestMethod]
        public void MissingStartupShortcutCanBeWritten()
        {
            string dir = Path.Combine(Path.GetTempPath(), "thrum-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                string link = Path.Combine(dir, "Thrum.lnk");
                Assert.IsTrue(StartupMethods.CanWriteStartEntry(link),
                    "A shortcut that does not exist yet must be writable");

                File.WriteAllText(link, "x");
                Assert.IsTrue(StartupMethods.CanWriteStartEntry(link));

                File.SetAttributes(link, FileAttributes.ReadOnly);
                Assert.IsFalse(StartupMethods.CanWriteStartEntry(link));
                File.SetAttributes(link, FileAttributes.Normal);
            }
            finally
            {
                Directory.Delete(dir, recursive: true);
            }
        }

        [TestMethod]
        public void AppCaptureWithNoIdentityDoesNotScanProcesses()
        {
            var watch = Stopwatch.StartNew();
            int pid = ProcessLoopbackWaveCapture.ResolveProcessId(
                new AudioHapticsProfileSettings());
            watch.Stop();

            Assert.AreEqual(0, pid);
        }

        [TestMethod]
        public void AppCaptureFindsAProcessByName()
        {
            using Process self = Process.GetCurrentProcess();
            int pid = ProcessLoopbackWaveCapture.ResolveProcessId(
                new AudioHapticsProfileSettings { ExecutableName = self.ProcessName });

            Assert.IsTrue(pid > 0);
        }

        [TestMethod]
        public void AppCaptureRejectsASavedPidNowUsedByAnotherProgram()
        {
            using Process self = Process.GetCurrentProcess();
            int pid = ProcessLoopbackWaveCapture.ResolveProcessId(
                new AudioHapticsProfileSettings
                {
                    ProcessId = self.Id,
                    ExecutableName = "thrum-test-no-such-game",
                });

            Assert.AreNotEqual(self.Id, pid);
        }

        [TestMethod]
        public void StreamErrorDescriptionIsNeverEmpty()
        {
            string text = ViiperOutDevice.DescribeStreamError(
                new IOException(string.Empty, new SocketException(10054)));

            StringAssert.Contains(text, "IOException");
            StringAssert.Contains(text, "ConnectionReset");
            StringAssert.Contains(text, "(no message)");
        }

        [TestMethod]
        public void WindowsElevenIsNotLoggedAsWindowsTen()
        {
            string name = Util.GetOSProductName();
            if (Environment.OSVersion.Version.Build >= 22000)
            {
                Assert.IsFalse(name.StartsWith("Windows 10", StringComparison.OrdinalIgnoreCase),
                    $"Logged as '{name}' on build {Environment.OSVersion.Version.Build}");
            }
        }
    }
}
