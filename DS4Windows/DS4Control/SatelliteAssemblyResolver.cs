/*
DS4Windows
Copyright (C) 2026  DS4Windows contributors

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
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;

namespace DS4Windows
{
    /// <summary>
    /// Finds this application's satellite (translation) assemblies without
    /// depending on the process working directory.
    ///
    /// <para><b>The bug this exists to fix.</b> The satellites are packaged as
    /// <c>&lt;install&gt;\Lang\&lt;culture&gt;\Thrum.resources.dll</c> —
    /// <c>utils/post-build.py</c> moves every culture folder MSBuild emits into
    /// a single <c>Lang</c> folder so the install root stays readable. Nothing
    /// in the CLR looks there by default, so
    /// <c>runtimeconfig.template.json</c> declares
    /// <c>"additionalProbingPaths": [ "./Lang/" ]</c>. That path is
    /// <em>relative</em>, and the host resolves it against the process
    /// <b>current working directory</b> at startup, before any managed code
    /// runs. Launch the application from anywhere other than its install folder
    /// — a logon scheduled task (working directory
    /// <c>C:\Windows\System32</c>), a shortcut with an empty "Start in", a
    /// terminal in another folder — and every satellite silently fails to
    /// resolve. The UI stays English with no error and no log entry, however
    /// the language is set.</para>
    ///
    /// <para>Setting <c>Environment.CurrentDirectory</c> during startup does
    /// not help, and the fact that
    /// <c>App.Application_Startup</c> already does exactly that is the proof:
    /// the host has already turned the probing paths into absolute paths by
    /// then.</para>
    ///
    /// <para><b>The fix.</b> Resolve the satellite ourselves, from
    /// <see cref="AppContext.BaseDirectory"/>, which is the folder holding the
    /// executable no matter where the process was started. The host's probing
    /// path is deliberately kept: when the working directory happens to be the
    /// install folder the host still resolves everything and this handler is
    /// never called, and keeping the template identical to upstream's keeps the
    /// fork delta small.</para>
    ///
    /// <para>This handler is a fallback by construction. The default
    /// <see cref="AssemblyLoadContext"/> raises
    /// <see cref="AssemblyLoadContext.Resolving"/> only after the host probing
    /// paths and the CLR's own <c>&lt;base&gt;\&lt;culture&gt;\</c> satellite
    /// probe have both come up empty, so it can never shadow an assembly the
    /// runtime would otherwise have found.</para>
    /// </summary>
    public static class SatelliteAssemblyResolver
    {
        /// <summary>
        /// Simple-name suffix that marks an assembly as a resources satellite.
        /// The handler ignores everything else, so it cannot interfere with
        /// ordinary assembly resolution.
        /// </summary>
        public const string ResourcesAssemblySuffix = ".resources";

        /// <summary>
        /// Upper bound on the parent-culture walk. Real chains are two or three
        /// long (<c>zh-Hant-TW</c> → <c>zh-Hant</c> → <c>zh</c>); the cap only
        /// exists so a pathological culture name cannot spin here.
        /// </summary>
        private const int MaxCultureChainDepth = 8;

        /// <summary>
        /// Whether <see cref="Install"/> has run and the handler is attached.
        ///
        /// <para>This exists to be asserted. Reading it is itself a call into
        /// this module, so the runtime has to have run the module initializer
        /// before the read can return — which makes
        /// <c>SatelliteAssemblyResolutionTests</c>' registration test a real
        /// check that the handler is in place before any of this assembly's
        /// code runs, rather than a restatement of the attribute.</para>
        /// </summary>
        public static bool Installed { get; private set; }

        /// <summary>
        /// Registers the resolver on the default load context.
        ///
        /// <para><b>Why a module initializer.</b> The handler has to be in place
        /// before the first resource lookup, and by the time
        /// <c>App.Application_Startup</c> runs that is no longer guaranteed:
        /// the WPF entry point has already constructed <c>App</c> — running its
        /// static field initializers — and <c>InitializeComponent</c> has
        /// applied <c>App.xaml</c>, whose merged resource dictionaries and
        /// <c>WPFLocalizeExtension</c> markup can reach the resource manager.
        /// A static constructor on <c>App</c> is earlier, but still runs after
        /// that type's own field initializers. A module initializer is emitted
        /// into the module's <c>.cctor</c>, which the runtime guarantees to run
        /// before <em>any</em> method of this assembly executes — including
        /// <c>Main</c>. There is nothing earlier that is still managed code in
        /// this assembly.</para>
        ///
        /// <para>Consequently this method must not fail: an exception here
        /// becomes a <c>TypeInitializationException</c> before <c>Main</c> and
        /// the process never starts. It does one thing, and it swallows.</para>
        /// </summary>
        [ModuleInitializer]
        internal static void Install()
        {
            try
            {
                if (Installed)
                {
                    return;
                }

                AssemblyLoadContext.Default.Resolving += OnResolving;
                Installed = true;
            }
            catch
            {
                // Losing the handler costs translations, which is exactly the
                // state the application has shipped in until now. Losing the
                // process costs everything.
            }
        }

        /// <summary>
        /// The paths a satellite could live at, most specific culture first.
        /// Pure: no file system access, no working directory, no ambient state,
        /// which is what makes the mapping testable on its own.
        /// </summary>
        /// <param name="requested">The assembly the runtime asked for.</param>
        /// <param name="baseDirectory">
        /// Folder to resolve against — <see cref="AppContext.BaseDirectory"/>
        /// in production.
        /// </param>
        /// <returns>
        /// An empty list for anything this resolver has no business answering:
        /// a null request, a name that is not a <c>.resources</c> assembly, a
        /// request with no culture (that is the neutral assembly, which is not
        /// a satellite), or a culture name that is not usable as a folder name.
        /// </returns>
        public static IReadOnlyList<string> CandidatePaths(
            AssemblyName requested, string baseDirectory)
        {
            var paths = new List<string>();

            if (requested == null || string.IsNullOrEmpty(baseDirectory))
            {
                return paths;
            }

            string simpleName = requested.Name;
            if (string.IsNullOrEmpty(simpleName) ||
                !simpleName.EndsWith(ResourcesAssemblySuffix,
                    StringComparison.OrdinalIgnoreCase))
            {
                return paths;
            }

            // The file name is composed from the *requested* simple name rather
            // than from ProductInfo.LanguageAssemblyName, because a resolver
            // has to answer the question it was actually asked: post-build.py
            // sweeps every culture folder into Lang/, so a dependency's
            // satellites end up there too and are broken in exactly the same
            // way. SatelliteAssemblyResolutionTests pins that this composition
            // still produces ProductInfo.LanguageAssemblyName for our own
            // satellites, so the two cannot drift apart unnoticed.
            string fileName = simpleName + ".dll";

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string culture in CultureChain(requested.CultureName))
            {
                if (!IsUsableFolderName(culture))
                {
                    continue;
                }

                // Global.PROBING_PATH is a ';'-separated list, and is split the
                // same way by LanguagePackViewModel when it enumerates the
                // installed language packs. Sharing the constant is the point:
                // the folder the packs are listed from and the folder they are
                // loaded from cannot drift apart.
                foreach (string probingPath in Global.PROBING_PATH.Split(';'))
                {
                    if (string.IsNullOrWhiteSpace(probingPath))
                    {
                        continue;
                    }

                    string candidate = Path.Combine(
                        baseDirectory, probingPath.Trim(), culture, fileName);
                    if (seen.Add(candidate))
                    {
                        paths.Add(candidate);
                    }
                }
            }

            return paths;
        }

        /// <summary>
        /// The handler body, with its two ambient inputs — the load context and
        /// the base directory — passed in so it can be exercised against a
        /// directory tree of the test's own making.
        /// </summary>
        /// <returns>
        /// The satellite, or <c>null</c> when there is nothing to load, so that
        /// the runtime's default behaviour (ultimately: fall back to the
        /// neutral resources) still applies.
        /// </returns>
        public static Assembly Resolve(AssemblyLoadContext context,
            AssemblyName requested, string baseDirectory)
        {
            if (context == null)
            {
                return null;
            }

            IReadOnlyList<string> candidates;
            try
            {
                candidates = CandidatePaths(requested, baseDirectory);
            }
            catch
            {
                return null;
            }

            foreach (string candidate in candidates)
            {
                try
                {
                    if (!File.Exists(candidate))
                    {
                        continue;
                    }

                    return context.LoadFromAssemblyPath(candidate);
                }
                catch
                {
                    // An unreadable or corrupt satellite is not worth failing a
                    // resource lookup over; try the parent culture instead. No
                    // logging: this runs inside assembly resolution, and the
                    // logger's own initialisation loads assemblies.
                }
            }

            return null;
        }

        private static Assembly OnResolving(
            AssemblyLoadContext context, AssemblyName requested)
        {
            return Resolve(context, requested, AppContext.BaseDirectory);
        }

        /// <summary>
        /// A culture and its parents, most specific first, the way the CLR's
        /// own resource fallback walks them (<c>pt-BR</c> → <c>pt</c>), stopping
        /// before the invariant culture — the invariant resources are compiled
        /// into the main assembly and are never a satellite.
        /// </summary>
        private static IEnumerable<string> CultureChain(string cultureName)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string current = cultureName;

            for (int depth = 0;
                depth < MaxCultureChainDepth && !string.IsNullOrEmpty(current);
                depth++)
            {
                if (!seen.Add(current))
                {
                    yield break;
                }

                yield return current;
                current = ParentCultureName(current);
            }
        }

        /// <summary>
        /// <see cref="CultureInfo.Parent"/> where the culture is known, because
        /// that is what the CLR uses and it is not always the name with the last
        /// segment removed (<c>zh-TW</c>'s parent is <c>zh-Hant</c>). Falls back
        /// to stripping the last segment for a name ICU does not recognise, and
        /// returns an empty string at the top of the chain.
        /// </summary>
        private static string ParentCultureName(string cultureName)
        {
            try
            {
                string parent = CultureInfo.GetCultureInfo(cultureName).Parent.Name;
                if (!string.Equals(parent, cultureName, StringComparison.OrdinalIgnoreCase))
                {
                    return parent;
                }
            }
            catch (CultureNotFoundException)
            {
                // Not a culture this machine knows. The textual walk below is
                // still the right guess for a well-formed name.
            }
            catch (ArgumentException)
            {
            }

            int lastSeparator = cultureName.LastIndexOf('-');
            return lastSeparator > 0
                ? cultureName.Substring(0, lastSeparator)
                : string.Empty;
        }

        /// <summary>
        /// Whether a culture name can be used as a single folder name. The
        /// culture comes from the requested assembly name, so it is untrusted
        /// input: anything that could escape the probing folder is refused
        /// rather than combined into a path.
        /// </summary>
        private static bool IsUsableFolderName(string culture)
        {
            return !string.IsNullOrWhiteSpace(culture) &&
                culture != "." &&
                culture != ".." &&
                culture.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;
        }
    }
}
