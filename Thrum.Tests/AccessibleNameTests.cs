using System;
using DS4Windows;
using DS4WinWPF;
using DS4WinWPF.DS4Control;
using DS4WinWPF.DS4Forms;
using DS4WinWPF.DS4Forms.ViewModels;

namespace DS4WindowsTests;

/// <summary>
/// Accessible names of list items (Phase 2 VM validation, incidental defect 3).
/// WPF's ItemAutomationPeer falls back to the data item's ToString() for the
/// UIA Name, so every class shown through an ItemsControl without a
/// DisplayMemberPath must override ToString() with the text the row displays —
/// otherwise screen readers announce "DS4WinWPF.DS4Forms.ViewModels.…".
/// These tests pin the composed text for the item classes that can be built
/// without hardware or a running WPF application.
/// </summary>
[TestClass]
public class AccessibleNameTests
{
    [TestMethod]
    public void SlotDeviceEntry_EmptySlot_AnnouncesSlotStateNotTypeName()
    {
        var entry = new SlotDeviceEntry(new OutSlotDevice(0), 0);

        Assert.AreEqual("Slot 1: Empty, requested Dynamic", entry.ToString());
    }

    [TestMethod]
    public void SlotDeviceEntry_BoundInput_AppendsInputDescription()
    {
        var slot = new OutSlotDevice(2)
        {
            InputIndex = 1,
            InputDisplayString = "DualSense",
        };
        var entry = new SlotDeviceEntry(slot, 2);

        Assert.AreEqual("Slot 3: Empty, requested Dynamic, input 2 (DualSense)",
            entry.ToString());
    }

    [TestMethod]
    public void ViiperDriverIdentityField_AnnouncesRenderedLine()
    {
        var field = new ViiperDriverIdentityField("INF name", "usbip2_ude.inf");

        Assert.AreEqual("INF name: usbip2_ude.inf", field.ToString());
        Assert.AreEqual(field.Display, field.ToString());
    }

    [TestMethod]
    public void ViiperDriverComponentIdentity_AnnouncesComponentName()
    {
        var identity = new ViiperDriverComponentIdentity(
            "UDE host controller", true,
            new[] { new ViiperDriverIdentityField("Service", "usbip2_ude") });

        Assert.AreEqual("UDE host controller", identity.ToString());
    }

    [TestMethod]
    public void LogItem_AnnouncesTimeAndMessage()
    {
        var when = new DateTime(2026, 7, 30, 19, 47, 0);
        var item = new LogItem { Datetime = when, Message = "Stopping DS4 Input" };

        Assert.AreEqual($"{when:G} Stopping DS4 Input", item.ToString());
    }

    [TestMethod]
    public void ProfileEntity_AnnouncesProfileName()
    {
        var entity = new ProfileEntity { Name = "Default" };

        Assert.AreEqual("Default", entity.ToString());
    }

    [TestMethod]
    public void SwipeProfileItem_AnnouncesProfileName()
    {
        var item = new SwipeProfileItem { Name = "Racing", IsAllowed = true };

        Assert.AreEqual("Racing", item.ToString());
    }

    [TestMethod]
    public void LangPackItem_AnnouncesNativeLanguageName()
    {
        var item = new LangPackItem("en", "English");

        Assert.AreEqual("English", item.ToString());
    }
}
