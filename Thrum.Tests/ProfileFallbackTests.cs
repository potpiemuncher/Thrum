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

using DS4Windows;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;

namespace DS4WindowsTests;

/// <summary>
/// Covers the fallback that keeps a controller usable when its slot has no
/// remembered profile.
///
/// <para>Found on hardware (2026-08-02, issue #56): enabling a device family
/// the configuration had never seen left the pad connected, visible, correctly
/// reporting battery and transport — and doing nothing at all, because an empty
/// slot assignment loaded no profile and therefore no mappings, no lightbar
/// routine and no output. Nothing on screen explained it. That is the ordinary
/// first-run path for any non-DualShock 4 controller, since the setup wizard
/// enables DS4 only by default.</para>
/// </summary>
[TestClass]
public class ProfileFallbackTests
{
    private string previousAppDataPath;
    private string tempRoot;

    [TestInitialize]
    public void RedirectAppDataToTemp()
    {
        previousAppDataPath = Global.appdatapath;
        tempRoot = Path.Combine(Path.GetTempPath(),
            "thrum-profilefallback-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(tempRoot, "Profiles"));
        Global.appdatapath = tempRoot;
    }

    [TestCleanup]
    public void RestoreAppDataPath()
    {
        Global.appdatapath = previousAppDataPath;
        try { Directory.Delete(tempRoot, true); } catch (Exception) { }
    }

    private void WriteProfile(string name) =>
        File.WriteAllText(Path.Combine(tempRoot, "Profiles", name + ".xml"),
            "<Profile />");

    [TestMethod]
    public void AnExistingProfileIsReturnedUnchanged()
    {
        WriteProfile("Racing");
        WriteProfile(Global.DefaultProfileName);

        Assert.AreEqual("Racing", Global.ResolveProfileOrDefault("Racing"));
    }

    [TestMethod]
    public void AnEmptySlotFallsBackToTheDefaultProfile()
    {
        // The exact shape of #56: a slot the configuration has never used.
        WriteProfile(Global.DefaultProfileName);

        Assert.AreEqual(Global.DefaultProfileName,
            Global.ResolveProfileOrDefault(string.Empty));
        Assert.AreEqual(Global.DefaultProfileName,
            Global.ResolveProfileOrDefault(null));
    }

    [TestMethod]
    public void ADanglingProfileNameFallsBackToTheDefaultProfile()
    {
        // A profile the user deleted while it was still assigned to a slot.
        WriteProfile(Global.DefaultProfileName);

        Assert.AreEqual(Global.DefaultProfileName,
            Global.ResolveProfileOrDefault("DeletedLastWeek"));
    }

    [TestMethod]
    public void WithNoDefaultProfileTheInputIsReturnedUnchanged()
    {
        // Nothing to fall back to. Returning the original keeps the caller's
        // existing "not using a profile" reporting truthful rather than
        // inventing a profile that does not exist.
        Assert.AreEqual("Missing", Global.ResolveProfileOrDefault("Missing"));
        Assert.AreEqual(string.Empty,
            Global.ResolveProfileOrDefault(string.Empty));
    }

    [TestMethod]
    public void AMalformedNameDoesNotThrowAndFallsBack()
    {
        // Profiles.xml is hand-editable; an illegal path must not stop a
        // controller from connecting.
        WriteProfile(Global.DefaultProfileName);

        Assert.AreEqual(Global.DefaultProfileName,
            Global.ResolveProfileOrDefault("bad<>name|?"));
    }
}
