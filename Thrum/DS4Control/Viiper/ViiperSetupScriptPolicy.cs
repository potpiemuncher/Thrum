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
using System.Text;

namespace DS4Windows
{
    /// <summary>
    /// How the elevated VIIPER setup script may be run.
    ///
    /// <para>The script lives in the program folder, which the user (and any
    /// program running as the user) can write, and runs as administrator. It
    /// used to run with <c>-ExecutionPolicy Bypass</c>, so an edit made before
    /// the UAC prompt ran elevated too, and Defender flags that pattern. A
    /// signed release (Thrum.exe carries a signature) now runs it with
    /// <c>AllSigned</c>: PowerShell checks the script's signature in the
    /// elevated process, after any such edit. The script must be signed by the
    /// same publisher as Thrum.exe; otherwise it is not run at all, with a
    /// plain explanation instead of PowerShell's.</para>
    ///
    /// <para>Under <c>AllSigned</c>, PowerShell asks once whether to run
    /// software from a publisher the user has not trusted yet, and its default
    /// answer is "Do not run". The setup window therefore says what to answer
    /// first, and says so again, and waits, if the script was not run.</para>
    ///
    /// <para>An unsigned build (a development or unsigned beta build) keeps
    /// <c>Bypass</c>: <c>AllSigned</c> would refuse its unsigned script.</para>
    /// </summary>
    internal static class ViiperSetupScriptPolicy
    {
        internal const string SignedPolicy = "AllSigned";
        internal const string UnsignedPolicy = "Bypass";

        internal readonly struct Decision
        {
            public Decision(bool launch, string executionPolicy, string refusal,
                string publisher = null)
            {
                Launch = launch;
                ExecutionPolicy = executionPolicy;
                Refusal = refusal;
                Publisher = publisher;
            }

            public bool Launch { get; }
            public string ExecutionPolicy { get; }

            /// <summary>Signer of Thrum.exe and the script; null when unsigned.</summary>
            public string Publisher { get; }

            /// <summary>Why setup was not started, for the user; null when it was.</summary>
            public string Refusal { get; }
        }

        /// <param name="app">Signature of the running Thrum.exe.</param>
        /// <param name="script">Signature of the setup script.</param>
        internal static Decision Decide(ViiperSignatureTrust app,
            ViiperSignatureTrust script)
        {
            // A signer name is read whenever a signature and chain exist, even
            // if revocation could not be checked offline; that still counts as
            // a signed build.
            string appSigner = app?.ObservedSignerCommonName;
            if (string.IsNullOrEmpty(appSigner))
            {
                return new Decision(true, UnsignedPolicy, null);
            }

            string scriptSigner = script?.ObservedSignerCommonName;
            if (!string.Equals(appSigner, scriptSigner, StringComparison.Ordinal))
            {
                string found = string.IsNullOrEmpty(scriptSigner)
                    ? "is not signed"
                    : $"is signed by \"{scriptSigner}\", not \"{appSigner}\"";
                return new Decision(false, null,
                    $"The VIIPER setup script in this copy of {ProductInfo.ProductName} {found}, " +
                    "so it was not run. It may have been changed after download. " +
                    $"Download {ProductInfo.ProductName} again from its release page and run setup from the new copy.");
            }

            return new Decision(true, SignedPolicy, null, appSigner);
        }

        /// <summary>powershell.exe arguments for a launch <paramref name="decision"/> allows.</summary>
        internal static string Arguments(Decision decision, string scriptPath)
        {
            if (decision.ExecutionPolicy != SignedPolicy)
            {
                return $"-NoProfile -ExecutionPolicy {decision.ExecutionPolicy} -File \"{scriptPath}\" -NoPause";
            }

            // The execution policy governs script files, not this command
            // text, so the explanation prints before the publisher prompt and
            // a refusal is shown instead of the window closing on it.
            string command =
                "$ErrorActionPreference = 'Stop'\n" +
                $"Write-Host 'Windows PowerShell may ask whether to run software from {Quote(decision.Publisher)}. Type R (Run once) or A (Always run), then press Enter.'\n" +
                "Write-Host ''\n" +
                "try {\n" +
                $"    & '{Quote(scriptPath)}' -NoPause\n" +
                "    exit $LASTEXITCODE\n" +
                "}\n" +
                "catch {\n" +
                "    Write-Host ''\n" +
                "    Write-Host ('VIIPER setup did not run: ' + $_.Exception.Message) -ForegroundColor Red\n" +
                "    Read-Host 'Press Enter to close'\n" +
                "    exit 1\n" +
                "}\n";
            string encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(command));
            return $"-NoProfile -ExecutionPolicy {SignedPolicy} -EncodedCommand {encoded}";
        }

        // Inside a single-quoted PowerShell string only the quote is special;
        // PowerShell also accepts the typographic single quotes as quotes.
        internal static string Quote(string text)
        {
            StringBuilder quoted = new StringBuilder();
            foreach (char c in text ?? string.Empty)
            {
                quoted.Append(c);
                if (c == '\'' || c == '\u2018' || c == '\u2019' ||
                    c == '\u201A' || c == '\u201B')
                {
                    quoted.Append(c);
                }
            }

            return quoted.ToString();
        }
    }
}
