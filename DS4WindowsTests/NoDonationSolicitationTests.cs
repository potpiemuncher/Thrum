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

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace DS4WindowsTests;

/// <summary>
/// Thrum asks nobody for money, and in particular never routes a payment to a
/// third party's personal account.
///
/// <para>The inherited Settings page carried a card reading "Support
/// DS4Windows" whose button opened the upstream maintainer's personal PayPal.
/// It shipped in 0.9.0-beta.1 that way. Two things were wrong with it: a Thrum
/// user clicking Support inside Thrum could not tell whose pocket it filled,
/// and it solicited money for a third party, from a fork, without their
/// agreement. Upstream credit belongs in NOTICE.txt, the README and the About
/// window, where it is attribution rather than a payment prompt.</para>
///
/// <para>This guard exists because the card was not introduced deliberately —
/// it was inherited and never revisited, which is exactly how it would come
/// back.</para>
/// </summary>
[TestClass]
public class NoDonationSolicitationTests
{
    /// <summary>
    /// Payment processors and donation platforms. A URL to any of these in
    /// shipped UI code is the thing this guard is looking for.
    /// </summary>
    private static readonly string[] PaymentHosts =
    {
        "paypal.com", "paypal.me", "ko-fi.com", "patreon.com",
        "buymeacoffee.com", "opencollective.com", "liberapay.com",
        "github.com/sponsors", "donorbox.org", "gofundme.com",
    };

    [TestMethod]
    public void NoShippedCodeLinksToAPaymentOrDonationService()
    {
        List<string> hits = new();
        int filesScanned = 0;

        string root = Path.Combine(FindRepositoryRoot(), "DS4Windows");
        foreach (string file in Directory.GetFiles(root, "*.*",
            SearchOption.AllDirectories))
        {
            string extension = Path.GetExtension(file);
            if (extension is not (".cs" or ".xaml" or ".resx"))
            {
                continue;
            }

            // Build output is a copy of the sources, not a separate surface.
            if (file.Contains(Path.DirectorySeparatorChar + "obj" +
                    Path.DirectorySeparatorChar) ||
                file.Contains(Path.DirectorySeparatorChar + "bin" +
                    Path.DirectorySeparatorChar))
            {
                continue;
            }

            filesScanned++;
            string[] lines = File.ReadAllLines(file);
            for (int index = 0; index < lines.Length; index++)
            {
                string line = lines[index];

                // A comment explaining why the card was removed must not itself
                // trip the guard - it is documentation, not a solicitation.
                string trimmed = line.TrimStart();
                if (trimmed.StartsWith("//") || trimmed.StartsWith("///") ||
                    trimmed.StartsWith("*") || trimmed.StartsWith("<!--"))
                {
                    continue;
                }

                foreach (string host in PaymentHosts)
                {
                    if (line.Contains(host,
                        StringComparison.OrdinalIgnoreCase))
                    {
                        hits.Add(Path.GetFileName(file) + " line " +
                            (index + 1) + ": " + host);
                    }
                }
            }
        }

        Assert.IsTrue(filesScanned > 50,
            "Only " + filesScanned + " source files were scanned, which is too " +
            "few for this repository - the guard is not looking where it thinks " +
            "it is. Fix the scan rather than trusting the pass.");

        Assert.AreEqual(0, hits.Count,
            "Shipped code links to a payment or donation service:\n  " +
            string.Join("\n  ", hits) +
            "\n\nThrum solicits no donations, and must never route a payment " +
            "to a third party's personal account. If upstream deserves credit " +
            "- and it does - put it in NOTICE.txt, the README or the About " +
            "window as attribution, not as a payment prompt.");
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo directory = new(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName,
                "DS4WindowsWPF.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root not found.");
    }
}
