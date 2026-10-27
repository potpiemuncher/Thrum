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
using System.IO;
using DS4Windows;
using DS4Windows.InputDevices;
using DS4WinWPF.DS4Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DS4WindowsTests
{
    /// <summary>
    /// The behaviour changes the owner approved on 2026-10-18.
    /// </summary>
    [TestClass]
    public class ReleaseReviewDecisionTests
    {
        private string dir;

        [TestInitialize]
        public void CreateFolder()
        {
            dir = Path.Combine(Path.GetTempPath(), "thrum-decisions-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
        }

        [TestCleanup]
        public void RemoveFolder()
        {
            try
            {
                Directory.Delete(dir, recursive: true);
            }
            catch (IOException)
            {
            }
        }

        [TestMethod]
        public void HidHideRecordRemembersOnlyThrumsEntries()
        {
            HidHideOwnershipRecord fresh = HidHideOwnershipRecord.Load(dir);
            Assert.IsFalse(fresh.Existed, "No record yet: older builds' entries are adopted once");

            fresh.AddHiddenDevice(@"HID\VID_054C&PID_0CE6\1");
            fresh.AddAllowedApplication(@"\Device\HarddiskVolume3\Games\game.exe");
            fresh.SetCloakingBefore(false);

            HidHideOwnershipRecord reloaded = HidHideOwnershipRecord.Load(dir);
            Assert.IsTrue(reloaded.Existed);
            Assert.IsTrue(reloaded.OwnsHiddenDevice(@"hid\vid_054c&pid_0ce6\1"),
                "Instance IDs compare without case, as HidHide's do");
            Assert.IsFalse(reloaded.OwnsHiddenDevice(@"HID\VID_054C&PID_0CE6\2"));
            Assert.IsTrue(reloaded.OwnsAllowedApplication(@"\Device\HarddiskVolume3\Games\game.exe"));
            Assert.IsFalse(reloaded.OwnsAllowedApplication(@"\Device\HarddiskVolume3\Steam\steam.exe"));
            Assert.AreEqual(false, reloaded.CloakingBefore);

            reloaded.RemoveHiddenDevices(new[] { @"HID\VID_054C&PID_0CE6\1" });
            reloaded.SetCloakingBefore(null);
            HidHideOwnershipRecord released = HidHideOwnershipRecord.Load(dir);
            Assert.IsTrue(released.Existed,
                "The record stays once written, so later user entries are never adopted");
            Assert.AreEqual(0, released.HiddenDevices.Count);
            Assert.IsNull(released.CloakingBefore);
        }

        [TestMethod]
        public void DamagedFileIsRestoredOnlyWhenTheBackupLoads()
        {
            string path = Path.Combine(dir, "Auto Profiles.xml");
            File.WriteAllText(path, "damaged");
            File.WriteAllText(path + ".bak", "good");

            bool restored = SettingsFileRecovery.TryRestoreFromBackup(path, "Test file",
                candidate => File.ReadAllText(candidate) == "good");

            Assert.IsTrue(restored);
            Assert.AreEqual("good", File.ReadAllText(path));
            Assert.AreEqual(1, Directory.GetFiles(dir, "Auto Profiles.xml.damaged-*").Length,
                "The damaged copy is kept");
            SettingsFileRecovery.TakePendingNotice();
        }

        [TestMethod]
        public void DamagedFileStaysWhenTheBackupDoesNotLoad()
        {
            // Auto Profiles.xml is also the first-run marker: moving it aside
            // without a working backup would rerun the setup wizard.
            string path = Path.Combine(dir, "Auto Profiles.xml");
            File.WriteAllText(path, "damaged");
            File.WriteAllText(path + ".bak", "also damaged");

            Assert.IsFalse(SettingsFileRecovery.TryRestoreFromBackup(path, "Test file",
                candidate => false));
            Assert.AreEqual("damaged", File.ReadAllText(path));

            File.Delete(path + ".bak");
            Assert.IsFalse(SettingsFileRecovery.TryRestoreFromBackup(path, "Test file",
                candidate => true));
            Assert.AreEqual("damaged", File.ReadAllText(path));
        }

        [TestMethod]
        public void DualSenseBatteryUsesTenthsScale()
        {
            Assert.AreEqual(5, DualSenseDevice.DualSenseBatteryPercent(0x00));
            Assert.AreEqual(65, DualSenseDevice.DualSenseBatteryPercent(0x06));
            Assert.AreEqual(85, DualSenseDevice.DualSenseBatteryPercent(0x08),
                "Level 8 (80-89 %) used to read 100 %");
            Assert.AreEqual(100, DualSenseDevice.DualSenseBatteryPercent(0x0A));
            Assert.AreEqual(100, DualSenseDevice.DualSenseBatteryPercent(0x1F),
                "Only the low nibble is the level");
        }

        [TestMethod]
        public void SavedRealTimePriorityRunsAsHigh()
        {
            Assert.AreEqual(3, MainWindow.ProcessPriorityClasses.Count);
            Assert.IsFalse(MainWindow.ProcessPriorityClasses.Contains(ProcessPriorityClass.RealTime));
            Assert.AreEqual(ProcessPriorityClass.High, MainWindow.PriorityForSetting(3));
            Assert.AreEqual(ProcessPriorityClass.Normal, MainWindow.PriorityForSetting(-1));
            Assert.AreEqual(ProcessPriorityClass.AboveNormal, MainWindow.PriorityForSetting(1));
        }

        [TestMethod]
        public void HidHideWarningNamesTheProgramThatStillSeesThePad()
        {
            string message = ControllerHolderHint.BuildHidHideMessage("DualSense (AA:BB)",
                new[] { "Steam" });

            StringAssert.Contains(message, "DualSense (AA:BB)");
            StringAssert.Contains(message, "Steam");
            StringAssert.Contains(message, "still sees it");
        }

        [TestMethod]
        public void EditsGoToTheProfileTheControllerIsRunning()
        {
            string savedPath = Global.ProfilePath[0];
            string savedTemp = Global.tempprofilename[0];
            bool savedUse = Global.useTempProfile[0];
            try
            {
                Global.ProfilePath[0] = "Default";
                Global.useTempProfile[0] = true;
                Global.tempprofilename[0] = "Racing";
                Assert.AreEqual("Racing", Global.ActiveProfileName(0, out bool temporary));
                Assert.IsTrue(temporary);

                Global.useTempProfile[0] = false;
                Assert.AreEqual("Default", Global.ActiveProfileName(0, out temporary));
                Assert.IsFalse(temporary);
            }
            finally
            {
                Global.ProfilePath[0] = savedPath;
                Global.tempprofilename[0] = savedTemp;
                Global.useTempProfile[0] = savedUse;
            }
        }

        [TestMethod]
        public void AppAudioIsCapturedAt48Kilohertz()
        {
            Assert.AreEqual(48000, ProcessLoopbackWaveCapture.CaptureSampleRate);
        }
    }
}
