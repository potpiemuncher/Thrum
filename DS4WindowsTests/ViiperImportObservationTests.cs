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
using System.Collections.Generic;
using System.Linq;
using DS4Windows;

namespace DS4WindowsTests;

/// <summary>
/// The usbip import pass: what it may conclude, and — above all — what it may
/// touch.
///
/// <para>Two rules carry the safety here. First, 3.3's rule survives the
/// rewrite: a machine nobody could look at is not a machine known to be clean,
/// so an unreadable port list still refuses device creation. Second, the rule
/// the 2026-07-31 incident forced: an import this session did not create
/// cannot be attributed from the port table — a controller VID/PID behind a
/// localhost URL describes another application's live pad exactly as well as
/// a dead session's leftover, and the sweep that assumed otherwise
/// disconnected a live one mid-game. So attribution now needs the exact bus
/// id, on the same server, uniquely; everything weaker is reported and left
/// alone.</para>
/// </summary>
[TestClass]
public class ViiperImportObservationTests
{
    private static ViiperUsbipPortManager.UsbipPortBlock Block(int port,
        string url) =>
        new ViiperUsbipPortManager.UsbipPortBlock(port,
            $"Port {port}: device in use at Full Speed(12Mbps)\n" +
            $"       unknown vendor : unknown product (054c:0ce6)\n" +
            $"       7-{port} -> {url}\n");

    // ---- The verdict: could the machine be looked at ----------------------

    [TestMethod]
    public void AMachineNobodyCouldLookAtIsNotProvenClean()
    {
        ViiperImportObservation observation =
            ViiperUsbipPortManager.DecideImportObservation(false,
                "usbip.exe was not found");

        Assert.IsFalse(observation.Observed);
        StringAssert.Contains(observation.Reason, "could not be read");
        StringAssert.Contains(observation.Reason, "usbip.exe was not found");
    }

    [TestMethod]
    public void AnUnreadablePortListStillRefusesWhenThereIsNoErrorText()
    {
        ViiperImportObservation observation =
            ViiperUsbipPortManager.DecideImportObservation(false, null);

        Assert.IsFalse(observation.Observed);
        StringAssert.Contains(observation.Reason, "could not be read");
    }

    [TestMethod]
    public void OneSuccessfulReadIsAnObservation()
    {
        ViiperImportObservation observation =
            ViiperUsbipPortManager.DecideImportObservation(true, null);

        Assert.IsTrue(observation.Observed);
        Assert.IsNull(observation.Reason);
    }

    [TestMethod]
    public void TheResultTypeCarriesItsOwnContract()
    {
        Assert.IsTrue(ViiperImportObservation.Seen().Observed);
        Assert.IsNull(ViiperImportObservation.Seen().Reason);
        Assert.IsFalse(ViiperImportObservation.Unobserved("why").Observed);
        Assert.AreEqual("why", ViiperImportObservation.Unobserved("why").Reason);
    }

    // ---- The log line for what was deliberately left alone ----------------

    [TestMethod]
    public void UnmanagedLocalImportsAreNamedAndPointedAtTheCard()
    {
        string line = ViiperUsbipPortManager.DescribeUnmanagedLocalImports(
            new[]
            {
                Block(1, "usbip://localhost:3240/1-7"),
                Block(3, "usbip://127.0.0.1:3241/1-2"),
            },
            new HashSet<int>());

        Assert.IsNotNull(line);
        StringAssert.Contains(line, "2 local usbip import(s)");
        StringAssert.Contains(line, "port 1, 3");
        StringAssert.Contains(line, "left untouched",
            "The line is the only trace of the decision not to act; it has " +
            "to say that the leaving was deliberate.");
        StringAssert.Contains(line, "Backend process",
            "A user with a leftover needs the path to the affordance that " +
            "can actually clear it.");
    }

    [TestMethod]
    public void ThisSessionsOwnPortsAreNotReportedAsUnmanaged()
    {
        string line = ViiperUsbipPortManager.DescribeUnmanagedLocalImports(
            new[] { Block(2, "usbip://localhost:3240/1-7") },
            new HashSet<int> { 2 });

        Assert.IsNull(line);
    }

    [TestMethod]
    public void RemoteImportsAreNoneOfOurBusiness()
    {
        string line = ViiperUsbipPortManager.DescribeUnmanagedLocalImports(
            new[] { Block(4, "usbip://192.168.1.50:3240/1-1") },
            new HashSet<int>());

        Assert.IsNull(line,
            "An import from another machine was chosen by the user; naming " +
            "it as something this app 'does not manage' would only alarm.");
    }

    [TestMethod]
    public void AnEmptyPortTableSaysNothing()
    {
        Assert.IsNull(ViiperUsbipPortManager.DescribeUnmanagedLocalImports(
            Array.Empty<ViiperUsbipPortManager.UsbipPortBlock>(),
            new HashSet<int>()));
        Assert.IsNull(ViiperUsbipPortManager.DescribeUnmanagedLocalImports(
            null, new HashSet<int>()));
    }

    // ---- Attribution: exact bus id, same server, uniquely -----------------

    [TestMethod]
    public void TheLocalhostFormsAllCountAsLocal()
    {
        Assert.IsTrue(ViiperUsbipPortManager.IsLocalImport(
            Block(1, "usbip://localhost:3240/1-7")));
        Assert.IsTrue(ViiperUsbipPortManager.IsLocalImport(
            Block(1, "usbip://127.0.0.1:3240/1-7")));
        Assert.IsTrue(ViiperUsbipPortManager.IsLocalImport(
            Block(1, "usbip://[::1]:3240/1-7")));
        Assert.IsFalse(ViiperUsbipPortManager.IsLocalImport(
            Block(1, "usbip://192.168.1.50:3240/1-7")));
    }

    [TestMethod]
    public void ABusIdMatchesExactlyOrNotAtAll()
    {
        var block = Block(1, "usbip://localhost:3240/1-71");

        Assert.IsTrue(ViiperUsbipPortManager.MatchesBusId(block, "1-71"));
        Assert.IsFalse(ViiperUsbipPortManager.MatchesBusId(block, "1-7"),
            "\"/1-7\" is a prefix of \"/1-71\"; accepting it would attribute " +
            "somebody else's import to this device.");
        Assert.IsFalse(ViiperUsbipPortManager.MatchesBusId(block, null));
        Assert.IsFalse(ViiperUsbipPortManager.MatchesBusId(block, ""));
    }

    [TestMethod]
    public void AUniqueLocalBusidMatchIsAdopted()
    {
        int port = ViiperUsbipPortManager.SelectUniqueLocalBusidMatch(
            new[]
            {
                Block(1, "usbip://localhost:3240/1-7"),
                Block(2, "usbip://localhost:3240/1-8"),
            },
            "1-7", out int matches);

        Assert.AreEqual(1, port);
        Assert.AreEqual(1, matches);
    }

    /// <summary>
    /// The collision the uniqueness rule exists for: usbip bus ids are small
    /// integers every server counts from the bottom, so two local servers can
    /// both be serving a "1-7". Guessing means adopting — and at teardown,
    /// detaching — another application's device.
    /// </summary>
    [TestMethod]
    public void AnAmbiguousBusidMatchRefusesToGuess()
    {
        int port = ViiperUsbipPortManager.SelectUniqueLocalBusidMatch(
            new[]
            {
                Block(1, "usbip://localhost:3240/1-7"),
                Block(2, "usbip://localhost:3241/1-7"),
            },
            "1-7", out int matches);

        Assert.AreEqual(-1, port);
        Assert.AreEqual(2, matches);
    }

    [TestMethod]
    public void ARemoteImportNeverMatchesLocalAttribution()
    {
        int port = ViiperUsbipPortManager.SelectUniqueLocalBusidMatch(
            new[] { Block(1, "usbip://192.168.1.50:3240/1-7") },
            "1-7", out int matches);

        Assert.AreEqual(-1, port);
        Assert.AreEqual(0, matches);
    }

    [TestMethod]
    public void DuplicateDetachIsScopedToTheConfirmedImportsOwnServer()
    {
        var ports = new[]
        {
            Block(1, "usbip://localhost:3240/1-7"),   // ours, confirmed
            Block(2, "usbip://localhost:3240/1-7"),   // duplicate, same server
            Block(3, "usbip://localhost:3241/1-7"),   // same bus id, other server
        };

        IReadOnlyList<int> duplicates =
            ViiperUsbipPortManager.SelectSameServerDuplicates(ports, "1-7", 1);

        CollectionAssert.AreEqual(new List<int> { 2 }, duplicates.ToList(),
            "Port 3 carries the same bus id on a different server - that is " +
            "another program's device, not our duplicate.");
    }

    [TestMethod]
    public void NoConfirmedImportMeansNoDuplicateDetaching()
    {
        var ports = new[]
        {
            Block(2, "usbip://localhost:3240/1-7"),
        };

        Assert.AreEqual(0, ViiperUsbipPortManager.SelectSameServerDuplicates(
            ports, "1-7", 1).Count,
            "The kept port's own block is the source of the server identity; " +
            "without it there is no scope, and no scope means no detaching.");
        Assert.AreEqual(0, ViiperUsbipPortManager.SelectSameServerDuplicates(
            null, "1-7", 1).Count);
    }

    [TestMethod]
    public void TheServerPrefixIsTheUrlUpToThePath()
    {
        Assert.AreEqual("usbip://localhost:3240/",
            ViiperUsbipPortManager.ExtractServerPrefix(
                Block(1, "usbip://localhost:3240/1-7")));
        Assert.AreEqual("usbip://[::1]:3240/",
            ViiperUsbipPortManager.ExtractServerPrefix(
                Block(1, "usbip://[::1]:3240/1-7")));
        Assert.IsNull(ViiperUsbipPortManager.ExtractServerPrefix(
            new ViiperUsbipPortManager.UsbipPortBlock(1,
                "Port 1: something without a device URL")));
    }
}
