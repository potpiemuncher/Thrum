using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DS4Windows;
using DS4Windows.InputDevices;

namespace DS4WindowsTests
{
    [TestClass]
    public class DualSenseUsbAudioHapticsOwnershipTests
    {
        [TestMethod]
        public void ActiveLeaseSuppressesOnlyOrdinaryMotorOwnershipAndDisposeRestores()
        {
            int ownershipChanges = 0;
            DualSenseUsbAudioHapticsOwnership ownership =
                new DualSenseUsbAudioHapticsOwnership(
                    () => ownershipChanges++);
            byte[] activeReport = BuildOrdinaryUsbReport();

            using (ownership.Acquire())
            {
                bool written = ownership.WriteOrdinaryReport(activeReport, 0,
                    () => true);

                Assert.IsTrue(written);
                Assert.AreEqual(0, activeReport[1] & 0x03,
                    "Main-motor enable flags must stand down.");
                Assert.AreEqual(0xDC, activeReport[1],
                    "Trigger and unrelated enable flags must be preserved.");
                Assert.AreEqual(0, activeReport[3]);
                Assert.AreEqual(0, activeReport[4]);
                Assert.AreEqual(0x02, activeReport[39],
                    "Only the improved-rumble ownership bit should clear.");
                Assert.AreEqual(0xA5, activeReport[10]);
                Assert.IsTrue(ownership.Active);
            }

            byte[] restoredReport = BuildOrdinaryUsbReport();
            byte[] expected = (byte[])restoredReport.Clone();
            ownership.WriteOrdinaryReport(restoredReport, 0, () => true);

            CollectionAssert.AreEqual(expected, restoredReport);
            Assert.IsFalse(ownership.Active);
            Assert.AreEqual(2, ownershipChanges);
        }

        [TestMethod]
        public void ResetInvalidatesOutstandingLeasesAndRestoresReports()
        {
            int ownershipChanges = 0;
            DualSenseUsbAudioHapticsOwnership ownership =
                new DualSenseUsbAudioHapticsOwnership(
                    () => ownershipChanges++);
            IDisposable first = ownership.Acquire();
            IDisposable second = ownership.Acquire();

            first.Dispose();
            Assert.IsTrue(ownership.Active,
                "The remaining lease should keep USB audio ownership active.");

            ownership.Reset();
            byte[] report = BuildOrdinaryUsbReport();
            byte[] expected = (byte[])report.Clone();
            ownership.WriteOrdinaryReport(report, 0, () => true);

            CollectionAssert.AreEqual(expected, report);
            Assert.IsFalse(ownership.Active);
            Assert.AreEqual(2, ownershipChanges);

            second.Dispose();
            Assert.AreEqual(2, ownershipChanges,
                "A stale disconnect-era lease must be harmless.");
        }

        [TestMethod]
        public void ActiveUsbLeaseDoesNotModifyBluetoothShapedReport()
        {
            DualSenseUsbAudioHapticsOwnership ownership =
                new DualSenseUsbAudioHapticsOwnership(null);
            byte[] report = BuildOrdinaryUsbReport();
            report[0] = 0x31;
            byte[] expected = (byte[])report.Clone();

            using (ownership.Acquire())
            {
                ownership.WriteOrdinaryReport(report, 0, () => true);
            }

            CollectionAssert.AreEqual(expected, report);
        }

        [TestMethod]
        public void LeaseAcquisitionWaitsForInFlightOrdinaryUsbWrite()
        {
            DualSenseUsbAudioHapticsOwnership ownership =
                new DualSenseUsbAudioHapticsOwnership(null);
            byte[] firstReport = BuildOrdinaryUsbReport();
            byte[] expectedFirst = (byte[])firstReport.Clone();
            using ManualResetEventSlim writeEntered =
                new ManualResetEventSlim(false);
            using ManualResetEventSlim releaseWrite =
                new ManualResetEventSlim(false);
            using ManualResetEventSlim acquireAttempted =
                new ManualResetEventSlim(false);

            Task<bool> writeTask = Task.Run(() =>
                ownership.WriteOrdinaryReport(firstReport, 0, () =>
                {
                    writeEntered.Set();
                    if (!releaseWrite.Wait(TimeSpan.FromSeconds(2)))
                    {
                        throw new TimeoutException(
                            "Test did not release the in-flight HID write.");
                    }
                    return true;
                }));

            IDisposable lease = null;
            try
            {
                Assert.IsTrue(writeEntered.Wait(TimeSpan.FromSeconds(2)));
                Task<IDisposable> acquireTask = Task.Run(() =>
                {
                    acquireAttempted.Set();
                    return ownership.Acquire();
                });
                Assert.IsTrue(acquireAttempted.Wait(TimeSpan.FromSeconds(2)));
                Assert.IsFalse(acquireTask.Wait(TimeSpan.FromMilliseconds(100)),
                    "Ownership must not change midway through a USB HID write.");

                releaseWrite.Set();
                Assert.IsTrue(writeTask.Wait(TimeSpan.FromSeconds(2)));
                Assert.IsTrue(acquireTask.Wait(TimeSpan.FromSeconds(2)));
                lease = acquireTask.Result;

                CollectionAssert.AreEqual(expectedFirst, firstReport);
                byte[] nextReport = BuildOrdinaryUsbReport();
                ownership.WriteOrdinaryReport(nextReport, 0, () => true);
                Assert.AreEqual(0, nextReport[1] & 0x03);
                Assert.AreEqual(0, nextReport[3]);
                Assert.AreEqual(0, nextReport[4]);
            }
            finally
            {
                releaseWrite.Set();
                lease?.Dispose();
            }
        }

        [TestMethod]
        public void UsbOutputStartupPublishesOwnershipBeforePlayback()
        {
            List<string> events = new List<string>();
            RecordingDisposable lease = new RecordingDisposable(events);
            IDisposable published = null;

            AudioHapticsService.SlotRuntime.StartUsbOutputWithOwnership(
                () => events.Add("initialize"),
                () =>
                {
                    events.Add("acquire");
                    return lease;
                },
                ownership =>
                {
                    events.Add("publish");
                    published = ownership;
                },
                () => events.Add("play"),
                () =>
                {
                    events.Add("verify");
                    return true;
                },
                () => events.Add("rollback"));

            CollectionAssert.AreEqual(new[]
            {
                "initialize", "acquire", "publish", "play", "verify",
            }, events);
            Assert.AreSame(lease, published);
            Assert.AreEqual(0, lease.DisposeCount);
            published.Dispose();
            Assert.AreEqual(1, lease.DisposeCount);
        }

        [TestMethod]
        public void UsbOutputOpenFailureNeverAcquiresAndRollsBack()
        {
            List<string> events = new List<string>();

            Assert.ThrowsException<InvalidOperationException>(() =>
                AudioHapticsService.SlotRuntime.StartUsbOutputWithOwnership(
                    () =>
                    {
                        events.Add("initialize");
                        throw new InvalidOperationException("open failed");
                    },
                    () =>
                    {
                        events.Add("acquire");
                        return new RecordingDisposable(events);
                    },
                    ownership => events.Add("publish"),
                    () => events.Add("play"),
                    () => true,
                    () => events.Add("rollback")));

            CollectionAssert.AreEqual(new[]
            {
                "initialize", "rollback",
            }, events);
        }

        [TestMethod]
        public void UsbPlaybackStartFailureRollsBackPublishedOwnershipOnce()
        {
            List<string> events = new List<string>();
            RecordingDisposable lease = new RecordingDisposable(events);
            IDisposable published = null;

            Assert.ThrowsException<InvalidOperationException>(() =>
                AudioHapticsService.SlotRuntime.StartUsbOutputWithOwnership(
                    () => events.Add("initialize"),
                    () =>
                    {
                        events.Add("acquire");
                        return lease;
                    },
                    ownership =>
                    {
                        events.Add("publish");
                        published = ownership;
                    },
                    () =>
                    {
                        events.Add("play");
                        throw new InvalidOperationException("play failed");
                    },
                    () => true,
                    () =>
                    {
                        events.Add("rollback");
                        published?.Dispose();
                        published = null;
                    }));

            CollectionAssert.AreEqual(new[]
            {
                "initialize", "acquire", "publish", "play", "rollback",
                "dispose",
            }, events);
            Assert.AreEqual(1, lease.DisposeCount);
        }

        [TestMethod]
        public void UsbOutputThatStopsDuringStartupRollsBackOwnershipOnce()
        {
            List<string> events = new List<string>();
            RecordingDisposable lease = new RecordingDisposable(events);
            IDisposable published = null;

            Assert.ThrowsException<InvalidOperationException>(() =>
                AudioHapticsService.SlotRuntime.StartUsbOutputWithOwnership(
                    () => events.Add("initialize"),
                    () =>
                    {
                        events.Add("acquire");
                        return lease;
                    },
                    ownership =>
                    {
                        events.Add("publish");
                        published = ownership;
                    },
                    () => events.Add("play"),
                    () =>
                    {
                        events.Add("verify");
                        return false;
                    },
                    () =>
                    {
                        events.Add("rollback");
                        published?.Dispose();
                        published = null;
                    }));

            CollectionAssert.AreEqual(new[]
            {
                "initialize", "acquire", "publish", "play", "verify",
                "rollback", "dispose",
            }, events);
            Assert.AreEqual(1, lease.DisposeCount);
        }

        [TestMethod]
        public void UsbSampleWriteFailureIsContainedAndReportedOnce()
        {
            InvalidOperationException expected =
                new InvalidOperationException("device removed");
            Exception observed = null;
            int failureCount = 0;

            bool succeeded = AudioHapticsService.SlotRuntime
                .TryWriteUsbSamples((buffer, offset, count) =>
                {
                    throw expected;
                }, new byte[8], 8, exception =>
                {
                    failureCount++;
                    observed = exception;
                });

            Assert.IsFalse(succeeded);
            Assert.AreSame(expected, observed);
            Assert.AreEqual(1, failureCount);
        }

        [TestMethod]
        public void WiredFailureStatusIsStickyButBluetoothStatusIsUntouched()
        {
            AudioHapticsRuntimeStatus active =
                new AudioHapticsRuntimeStatus(true,
                    "Active over wired USB.", wiredOutputActive: true);

            AudioHapticsRuntimeStatus wired =
                AudioHapticsService.SlotRuntime.PreferUsbFailureStatus(
                    ConnectionType.USB, "Wired output failed.", active);
            AudioHapticsRuntimeStatus bluetooth =
                AudioHapticsService.SlotRuntime.PreferUsbFailureStatus(
                    ConnectionType.BT, "Wired output failed.", active);

            Assert.IsFalse(wired.Active);
            Assert.IsFalse(wired.WiredOutputActive);
            Assert.IsTrue(wired.Error);
            Assert.AreEqual("Wired output failed.", wired.Message);
            Assert.AreEqual(active.Active, bluetooth.Active);
            Assert.AreEqual(active.Message, bluetooth.Message);
            Assert.AreEqual(active.WiredOutputActive,
                bluetooth.WiredOutputActive);
            Assert.AreEqual(active.Error, bluetooth.Error);
        }

        [TestMethod]
        public void FailedWiredTransportIsNotReusableButBluetoothIs()
        {
            Assert.IsFalse(AudioHapticsService.SlotRuntime
                .IsOutputTransportReusable(ConnectionType.USB,
                    usbTransportReady: false));
            Assert.IsTrue(AudioHapticsService.SlotRuntime
                .IsOutputTransportReusable(ConnectionType.USB,
                    usbTransportReady: true));
            Assert.IsTrue(AudioHapticsService.SlotRuntime
                .IsOutputTransportReusable(ConnectionType.BT,
                    usbTransportReady: false));
        }

        [TestMethod]
        public void DisposeStopsWriterBeforeFinalUsbZeroThenRetiresOutput()
        {
            string source = File.ReadAllText(SourcePath("DS4Windows",
                "DS4Control", "AudioHapticsService.cs"));
            int dispose = source.LastIndexOf("public void Dispose()",
                StringComparison.Ordinal);
            int stop = source.IndexOf("stopped.Set();", dispose,
                StringComparison.Ordinal);
            int join = source.IndexOf("writerThread.Join(1200);", dispose,
                StringComparison.Ordinal);
            int finalZero = source.IndexOf(
                "Array.Clear(writerFrame, 0, writerFrame.Length);", dispose,
                StringComparison.Ordinal);
            int retireOutput = source.IndexOf(
                "RetireUsbHapticsOutputLocked(stopPlayback: true);",
                finalZero, StringComparison.Ordinal);
            int writeUsbFrame = source.IndexOf(
                "private void WriteUsbFrame(byte[] frame)",
                StringComparison.Ordinal);
            int writeLock = source.IndexOf("lock (usbFrameWriteLock)",
                writeUsbFrame, StringComparison.Ordinal);

            Assert.IsTrue(dispose >= 0 && stop > dispose && join > stop &&
                finalZero > join && retireOutput > finalZero,
                "Dispose must stop and join the writer before its final zero, " +
                "then keep the USB endpoint alive until that zero is sent.");
            Assert.IsTrue(writeUsbFrame >= 0 && writeLock > writeUsbFrame &&
                writeLock < finalZero,
                "Every USB frame must serialize access to the shared scratch " +
                "buffer.");
        }

        private static string SourcePath(params string[] parts)
        {
            DirectoryInfo directory =
                new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null && !File.Exists(Path.Combine(
                directory.FullName, "DS4WindowsWPF.sln")))
            {
                directory = directory.Parent;
            }

            Assert.IsNotNull(directory,
                "Could not find the repository root from the test output.");
            string path = directory.FullName;
            foreach (string part in parts)
            {
                path = Path.Combine(path, part);
            }
            return path;
        }

        private static byte[] BuildOrdinaryUsbReport()
        {
            byte[] report = new byte[48];
            report[0] = 0x02;
            report[1] = 0xDF;
            report[3] = 0x71;
            report[4] = 0x42;
            report[10] = 0xA5;
            report[39] = 0x06;
            return report;
        }

        private sealed class RecordingDisposable : IDisposable
        {
            private readonly List<string> events;

            internal RecordingDisposable(List<string> events)
            {
                this.events = events;
            }

            internal int DisposeCount { get; private set; }

            public void Dispose()
            {
                DisposeCount++;
                events.Add("dispose");
            }
        }
    }
}
