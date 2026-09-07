using DS4Windows;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;

namespace DS4WindowsTests
{
    /// <summary>
    /// Guards for <see cref="SatelliteAssemblyResolver"/>, the fix for
    /// translations silently failing to load whenever the process working
    /// directory is not the install folder (issue #6).
    ///
    /// <para>The packaged layout is
    /// <c>&lt;install&gt;\Lang\&lt;culture&gt;\Thrum.resources.dll</c>, reachable
    /// through <c>runtimeconfig.template.json</c>'s <c>"./Lang/"</c> probing
    /// path — which the host resolves against the <em>working directory</em>.
    /// Launched from a logon task or a shortcut with an empty "Start in", the
    /// application found none of its 23 satellites and stayed English with no
    /// error anywhere.</para>
    ///
    /// <para>The primary guard here is the pure mapping test: the candidate
    /// paths are a function of an explicitly supplied base directory, so a
    /// working directory cannot enter the answer even in principle. The one
    /// integration test then proves the mapping is not merely self-consistent —
    /// that a real satellite really loads through it — and it runs with the
    /// working directory moved away, which is the scenario that used to
    /// fail.</para>
    /// </summary>
    // Two tests move the process working directory. MSTest only parallelizes
    // when the assembly opts in, which this one does not, but the attribute
    // makes the requirement explicit so enabling parallelism later fails loudly
    // instead of flaking.
    [TestClass]
    [DoNotParallelize]
    public class SatelliteAssemblyResolutionTests
    {
        /// <summary>
        /// Simple name of this application's satellites, derived from the same
        /// constant the packaging and the language-pack scan use.
        /// </summary>
        private static readonly string SatelliteSimpleName =
            Path.GetFileNameWithoutExtension(ProductInfo.LanguageAssemblyName);

        /// <summary>A culture that actually ships a translation.</summary>
        private const string ShippedCulture = "de";

        private const string FakeBaseDirectory = @"X:\somewhere\install";

        [TestMethod]
        public void TheHandlerIsInstalledBeforeAnyOfThisAssemblysCodeRuns()
        {
            // Nothing in this test installs the handler. Reading the property is
            // a call into the application module, and the runtime must run that
            // module's initializer before the call can return, so a true here is
            // the guarantee the fix depends on: registered before Main, and
            // therefore before the first resource lookup.
            Assert.IsTrue(SatelliteAssemblyResolver.Installed,
                "The module initializer did not register the resolving handler. " +
                "Every launch whose working directory is not the install folder " +
                "silently loses all translations.");
        }

        [TestMethod]
        public void ASatelliteMapsUnderTheBaseDirectorysProbingFolder()
        {
            IReadOnlyList<string> candidates = SatelliteAssemblyResolver.CandidatePaths(
                SatelliteName(ShippedCulture), FakeBaseDirectory);

            // Composed from the same three constants the packaging uses, so a
            // change to any of them has to be made in both places or this fails:
            // the probing folder post-build.py creates, and the satellite file
            // name the language-pack scan looks for.
            CollectionAssert.AreEqual(
                new[]
                {
                    Path.Combine(FakeBaseDirectory, Global.PROBING_PATH,
                        ShippedCulture, ProductInfo.LanguageAssemblyName),
                },
                candidates.ToArray());
        }

        [TestMethod]
        public void EveryCandidateIsRootedAtTheGivenBaseDirectory()
        {
            // The whole point of the fix. A candidate that is relative, or that
            // is rooted anywhere else, is a candidate the working directory can
            // still move.
            foreach (string candidate in SatelliteAssemblyResolver.CandidatePaths(
                SatelliteName("pt-BR"), FakeBaseDirectory))
            {
                Assert.IsTrue(Path.IsPathFullyQualified(candidate),
                    $"Not an absolute path: {candidate}");
                StringAssert.StartsWith(candidate,
                    FakeBaseDirectory + Path.DirectorySeparatorChar);
            }
        }

        [TestMethod]
        public void TheAnswerDoesNotDependOnTheWorkingDirectory()
        {
            AssemblyName requested = SatelliteName(ShippedCulture);

            string previous = Directory.GetCurrentDirectory();
            try
            {
                string[] fromHere = SatelliteAssemblyResolver
                    .CandidatePaths(requested, FakeBaseDirectory).ToArray();

                Directory.SetCurrentDirectory(SystemDirectory);
                string[] fromElsewhere = SatelliteAssemblyResolver
                    .CandidatePaths(requested, FakeBaseDirectory).ToArray();

                CollectionAssert.AreEqual(fromHere, fromElsewhere);
            }
            finally
            {
                Directory.SetCurrentDirectory(previous);
            }
        }

        [TestMethod]
        public void AParentCultureIsTriedAfterTheSpecificOne()
        {
            // pt-BR ships, pt ships, and the CLR's own fallback walks from one
            // to the other. A resolver that answered only the exact culture
            // would break a machine set to a regional variant we do not carry.
            CollectionAssert.AreEqual(
                new[]
                {
                    Candidate("pt-BR"),
                    Candidate("pt"),
                },
                SatelliteAssemblyResolver.CandidatePaths(
                    SatelliteName("pt-BR"), FakeBaseDirectory).ToArray());
        }

        [TestMethod]
        public void TheChainStopsBeforeTheInvariantCulture()
        {
            // The neutral resources are compiled into the main assembly. A
            // "Lang\\<empty>" candidate would be nonsense, and an invariant
            // request is not a satellite request at all.
            CollectionAssert.AreEqual(
                new[] { Candidate(ShippedCulture) },
                SatelliteAssemblyResolver.CandidatePaths(
                    SatelliteName(ShippedCulture), FakeBaseDirectory).ToArray());

            Assert.AreEqual(0, SatelliteAssemblyResolver.CandidatePaths(
                new AssemblyName(SatelliteSimpleName), FakeBaseDirectory).Count,
                "A request with no culture is the neutral assembly, not a satellite.");
        }

        [TestMethod]
        public void NothingButASatelliteIsHandled()
        {
            // A resolving handler that answers for ordinary assemblies can
            // shadow the real one. This one must be inert for everything that
            // is not a .resources assembly, and return null rather than throw
            // for nonsense input.
            var notSatellites = new[]
            {
                ProductInfo.ExeBaseName,
                "NAudio",
                "Thrum.resourcesx",
                "resources",
                string.Empty,
            };

            foreach (string name in notSatellites)
            {
                var requested = new AssemblyName { Name = name };
                requested.CultureInfo = new CultureInfo(ShippedCulture);

                Assert.AreEqual(0,
                    SatelliteAssemblyResolver.CandidatePaths(
                        requested, FakeBaseDirectory).Count,
                    $"The resolver claimed a non-satellite assembly: '{name}'");
            }

            Assert.AreEqual(0,
                SatelliteAssemblyResolver.CandidatePaths(null, FakeBaseDirectory).Count);
            Assert.AreEqual(0,
                SatelliteAssemblyResolver.CandidatePaths(
                    SatelliteName(ShippedCulture), null).Count);
            Assert.IsNull(SatelliteAssemblyResolver.Resolve(
                AssemblyLoadContext.Default, null, FakeBaseDirectory));
        }

        [TestMethod]
        public void ACultureNameThatIsNotAFolderNameIsRefused()
        {
            // The culture arrives inside the requested assembly name, so it is
            // untrusted. CultureInfo.Name is virtual, which is the only way to
            // get such a name past the framework's own validation and therefore
            // the only way to exercise the guard.
            var requested = new AssemblyName(SatelliteSimpleName)
            {
                CultureInfo = new RenamedCulture(@"..\..\Windows\System32"),
            };

            Assert.AreEqual(0,
                SatelliteAssemblyResolver.CandidatePaths(
                    requested, FakeBaseDirectory).Count,
                "A culture name must never be combined into a path unchecked.");
        }

        [TestMethod]
        public void TheHandlerLoadsARealSatelliteWithTheWorkingDirectoryElsewhere()
        {
            // The narrow end-to-end case: a genuine satellite assembly, in the
            // packaged Lang\<culture>\ layout, resolved while the working
            // directory is C:\Windows\System32 — the working directory a logon
            // scheduled task hands the process, and the case that produced zero
            // loaded satellites before this fix.
            string shipped = Path.Combine(AppContext.BaseDirectory,
                ShippedCulture, ProductInfo.LanguageAssemblyName);
            Assert.IsTrue(File.Exists(shipped),
                $"Expected the build to place a satellite at {shipped}.");

            string installRoot = Path.Combine(Path.GetTempPath(),
                "ThrumSatelliteResolverTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(installRoot,
                Global.PROBING_PATH, ShippedCulture));
            File.Copy(shipped, Path.Combine(installRoot, Global.PROBING_PATH,
                ShippedCulture, ProductInfo.LanguageAssemblyName));

            // Loaded into a context of its own: the same satellite is already
            // in the default context in this process, and loading a second copy
            // of one identity there would fail for reasons that have nothing to
            // do with what is being tested.
            var context = new AssemblyLoadContext(
                "SatelliteAssemblyResolutionTests", isCollectible: true);
            string previousWorkingDirectory = Directory.GetCurrentDirectory();
            try
            {
                Directory.SetCurrentDirectory(SystemDirectory);

                Assembly satellite = SatelliteAssemblyResolver.Resolve(
                    context, SatelliteName(ShippedCulture), installRoot);

                Assert.IsNotNull(satellite,
                    "The resolver did not find the satellite it was pointed at.");
                Assert.AreEqual(ShippedCulture, satellite.GetName().CultureName);
                Assert.AreEqual(SatelliteSimpleName, satellite.GetName().Name);
            }
            finally
            {
                Directory.SetCurrentDirectory(previousWorkingDirectory);
                context.Unload();
                TryDelete(installRoot);
            }
        }

        [TestMethod]
        public void AMissingSatelliteIsNullAndNotAnException()
        {
            // Returning null is what lets the runtime fall back to the neutral
            // resources. Throwing out of a resolving handler would take the
            // resource lookup, and whatever was rendering, down with it.
            Assert.IsNull(SatelliteAssemblyResolver.Resolve(
                AssemblyLoadContext.Default,
                SatelliteName(ShippedCulture),
                Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))));
        }

        private static string SystemDirectory =>
            Environment.GetFolderPath(Environment.SpecialFolder.System);

        private static AssemblyName SatelliteName(string culture)
        {
            return new AssemblyName(SatelliteSimpleName)
            {
                CultureInfo = new CultureInfo(culture),
            };
        }

        private static string Candidate(string culture)
        {
            return Path.Combine(FakeBaseDirectory, Global.PROBING_PATH,
                culture, ProductInfo.LanguageAssemblyName);
        }

        private static void TryDelete(string directory)
        {
            // A loaded assembly keeps its file mapped until the collectible
            // context is finished with, which is not synchronous. Cleanup is
            // best effort; the temp folder is per-run and empty of anything
            // that matters.
            for (int attempt = 0; attempt < 3; attempt++)
            {
                try
                {
                    Directory.Delete(directory, recursive: true);
                    return;
                }
                catch (DirectoryNotFoundException)
                {
                    return;
                }
                catch (UnauthorizedAccessException)
                {
                    return;
                }
                catch (IOException)
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                }
            }
        }

        /// <summary>
        /// A culture that reports a name the framework would never produce.
        /// <see cref="CultureInfo.Name"/> is virtual, so this is the one way to
        /// hand the resolver a culture name that is not a legal folder name.
        /// </summary>
        private sealed class RenamedCulture : CultureInfo
        {
            private readonly string name;

            public RenamedCulture(string name)
                : base(ShippedCulture)
            {
                this.name = name;
            }

            public override string Name => name;
        }
    }
}
