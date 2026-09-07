using DS4Windows;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Resources;

namespace DS4WindowsTests
{
    /// <summary>
    /// Guards the icon set: that every icon the application can address exists,
    /// that it loads through <em>both</em> imaging stacks the application uses,
    /// and that it still carries the frames the tray depends on.
    ///
    /// <para>The third of those is the reason this file exists rather than
    /// leaning on the resource-existence checks in
    /// <c>ProductIdentityTests</c>. A pack URI resolving proves the bytes are
    /// compiled in; it does not prove they are an icon anything can use. WPF
    /// reads PNG-compressed icon frames happily, so a WPF-only check passes on
    /// a file that the notification area cannot display — H.NotifyIcon hands
    /// the icon to the shell through GDI, which picks a frame by size before it
    /// decodes anything. That failure appears only in the tray, only at
    /// runtime, on a machine nobody is debugging.</para>
    /// </summary>
    [TestClass]
    public class IconResourceTests
    {
        /// <summary>Sizes that must be present as uncompressed BMP frames.</summary>
        private static readonly int[] RequiredBmpFrameSizes = { 16, 24, 32, 48 };

        /// <summary>
        /// Every icon file the product addresses at runtime: the three named
        /// variants plus the eleven battery levels.
        /// </summary>
        private static IEnumerable<string> AllIconFileNames()
        {
            yield return ProductInfo.AppIconFileName;
            yield return ProductInfo.WhiteTrayIconFileName;
            yield return ProductInfo.BlackTrayIconFileName;
            for (int battery = 0; battery <= 100; battery += 10)
            {
                yield return $"{battery}.ico";
            }
        }

        [TestMethod]
        public void IconFileNamesAreComposedFromTheProductName()
        {
            Assert.AreEqual(ProductInfo.ProductName + ".ico",
                ProductInfo.AppIconFileName);
            Assert.AreEqual(ProductInfo.ProductName + " - White.ico",
                ProductInfo.WhiteTrayIconFileName);
            Assert.AreEqual(ProductInfo.ProductName + " - Black.ico",
                ProductInfo.BlackTrayIconFileName);
        }

        /// <summary>
        /// The settings page offers five tray choices. Each has to point at one
        /// of our own icons — the inherited map named <c>DS4W.ico</c> and
        /// friends, and a half-finished swap would leave one entry pointing at
        /// a file that no longer exists.
        /// </summary>
        [TestMethod]
        public void EveryTrayIconChoicePointsAtAProductIcon()
        {
            var expected = new HashSet<string>(StringComparer.Ordinal)
            {
                $"{ProductInfo.ResourcesPrefix}/{ProductInfo.AppIconFileName}",
                $"{ProductInfo.ResourcesPrefix}/{ProductInfo.WhiteTrayIconFileName}",
                $"{ProductInfo.ResourcesPrefix}/{ProductInfo.BlackTrayIconFileName}",
            };

            foreach (KeyValuePair<TrayIconChoice, string> entry in
                Global.iconChoiceResources)
            {
                Assert.IsTrue(expected.Contains(entry.Value),
                    $"Tray icon choice {entry.Key} points at '{entry.Value}', " +
                    "which is not one of this product's icons.");
            }
        }

        /// <summary>
        /// GDI. This is the path the notification area actually takes.
        /// </summary>
        [TestMethod]
        public void EveryIconLoadsThroughSystemDrawingIcon()
        {
            foreach (string fileName in AllIconFileNames())
            {
                byte[] bytes = ReadIcon(fileName);
                using var stream = new MemoryStream(bytes);
                try
                {
                    using var icon = new Icon(stream);
                    Assert.IsTrue(icon.Width > 0 && icon.Height > 0,
                        $"{fileName} loaded with no dimensions.");
                }
                catch (Exception ex)
                {
                    Assert.Fail($"{fileName} did not load through " +
                        $"System.Drawing.Icon: {ex}");
                }
            }
        }

        /// <summary>
        /// WPF imaging. This is the path an <c>ImageSource</c> pack URI takes,
        /// which is how the icon reaches the view model binding.
        /// </summary>
        [TestMethod]
        public void EveryIconLoadsThroughBitmapFrame()
        {
            foreach (string fileName in AllIconFileNames())
            {
                byte[] bytes = ReadIcon(fileName);
                string captured = fileName;

                RunOnStaThread(() =>
                {
                    using var stream = new MemoryStream(bytes);
                    BitmapFrame frame = BitmapFrame.Create(stream,
                        BitmapCreateOptions.None, BitmapCacheOption.OnLoad);

                    Assert.IsTrue(frame.PixelWidth > 0 && frame.PixelHeight > 0,
                        $"{captured} decoded to an empty frame.");
                    Assert.IsTrue(frame.Decoder.Frames.Count > 1,
                        $"{captured} decoded to {frame.Decoder.Frames.Count} " +
                        "frame(s); a multi-resolution icon is expected.");
                });
            }
        }

        /// <summary>
        /// The load-bearing one, and the reason the generator writes a mixed
        /// frame set. Reads the icon directory by hand and insists the small
        /// sizes are still classic uncompressed BMP frames.
        ///
        /// <para>A future regeneration that emits PNG everywhere — which is
        /// tempting, since it is smaller and every image viewer opens it —
        /// would pass every other test in this file and break the tray.</para>
        /// </summary>
        [TestMethod]
        public void EveryIconCarriesUncompressedFramesAtTheShellSizes()
        {
            var failures = new List<string>();

            foreach (string fileName in AllIconFileNames())
            {
                Dictionary<int, bool> framesByWidth = ReadIconDirectory(
                    ReadIcon(fileName));

                foreach (int size in RequiredBmpFrameSizes)
                {
                    if (!framesByWidth.TryGetValue(size, out bool isPng))
                    {
                        failures.Add($"{fileName}: no {size}px frame");
                    }
                    else if (isPng)
                    {
                        failures.Add($"{fileName}: the {size}px frame is PNG " +
                            "compressed, which GDI may refuse");
                    }
                }
            }

            Assert.AreEqual(0, failures.Count,
                "Icons are missing the frames the shell needs: " +
                string.Join("; ", failures));
        }

        /// <summary>
        /// Parses ICONDIR / ICONDIRENTRY and reports, per frame width, whether
        /// the payload is a PNG. A width byte of 0 means 256.
        /// </summary>
        private static Dictionary<int, bool> ReadIconDirectory(byte[] bytes)
        {
            Assert.IsTrue(bytes.Length > 6, "Icon is too short to have a header.");
            Assert.AreEqual(0, BitConverter.ToUInt16(bytes, 0), "idReserved");
            Assert.AreEqual(1, BitConverter.ToUInt16(bytes, 2), "idType");

            int count = BitConverter.ToUInt16(bytes, 4);
            Assert.IsTrue(count > 0, "Icon declares no frames.");

            byte[] pngSignature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
            var result = new Dictionary<int, bool>();

            for (int index = 0; index < count; index++)
            {
                int entry = 6 + (16 * index);
                int width = bytes[entry] == 0 ? 256 : bytes[entry];
                int length = (int)BitConverter.ToUInt32(bytes, entry + 8);
                int offset = (int)BitConverter.ToUInt32(bytes, entry + 12);

                Assert.IsTrue(offset > 0 && length > 0 &&
                    offset + length <= bytes.Length,
                    $"Frame {index} points outside the file.");

                bool isPng = length >= pngSignature.Length;
                for (int i = 0; isPng && i < pngSignature.Length; i++)
                {
                    isPng = bytes[offset + i] == pngSignature[i];
                }

                result[width] = isPng;
            }

            return result;
        }

        private static byte[] ReadIcon(string fileName)
        {
            string uri = $"{ProductInfo.ResourcesPrefix}/{fileName}";
            byte[] bytes = null;

            RunOnStaThread(() =>
            {
                // Force the app assembly to load so the
                // "/<AssemblyName>;component/..." authority resolves.
                _ = typeof(Global).Assembly;

                StreamResourceInfo info = Application.GetResourceStream(
                    new Uri(uri, UriKind.Relative));
                Assert.IsNotNull(info, $"Icon resource did not resolve: {uri}");

                using var buffer = new MemoryStream();
                info.Stream.CopyTo(buffer);
                bytes = buffer.ToArray();
            });

            Assert.IsNotNull(bytes);
            Assert.IsTrue(bytes.Length > 0, $"{uri} resolved to zero bytes.");
            return bytes;
        }

        /// <summary>
        /// Runs <paramref name="action"/> on an STA thread and rethrows what it
        /// threw. Deliberately does not construct an
        /// <see cref="Application"/>: WPF permits one per process and
        /// <c>ThemeResourceTests</c> already creates and shuts one down.
        /// </summary>
        private static void RunOnStaThread(Action action)
        {
            Exception failure = null;

            var thread = new Thread(() =>
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    failure = ex;
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(30)),
                "Icon work on the STA thread did not finish.");

            if (failure != null)
            {
                if (failure is AssertFailedException)
                {
                    throw new AssertFailedException(failure.Message, failure);
                }

                Assert.Fail(failure.ToString());
            }
        }
    }
}
