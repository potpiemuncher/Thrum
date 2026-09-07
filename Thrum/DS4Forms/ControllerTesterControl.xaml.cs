/*
DS4Windows
Copyright (C) 2023  Travis Nickles

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.
*/

using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using DS4Windows;
using DS4WinWPF.DS4Forms.ViewModels;
using NonFormTimer = System.Timers.Timer;

namespace DS4WinWPF.DS4Forms
{
    public partial class ControllerTesterControl : UserControl
    {
        private const double TraceWidth = 400.0;
        private const double TraceHeight = 82.0;
        private readonly NonFormTimer readingTimer;
        private CompositeDeviceModel controller;
        private DS4Device controllerDevice;
        private ControllerTesterViewModel viewModel;
        private bool timerEnabled;
        private bool tracePointsInitialized;
        private bool hostVisible = true;

        public ControllerTesterControl()
        {
            InitializeComponent();
            readingTimer = new NonFormTimer(1000.0 / 60.0)
            {
                AutoReset = false,
            };
            readingTimer.Elapsed += ReadingTimer_Elapsed;
        }

        internal int DeviceIndex => controller?.DevIndex ?? -1;

        internal bool UsesController(CompositeDeviceModel candidate) =>
            candidate != null &&
            ReferenceEquals(controllerDevice, candidate.Device);

        internal void SetHostVisible(bool value)
        {
            hostVisible = value;
            if (value)
            {
                StartTimerIfVisible();
            }
            else
            {
                StopTimer();
            }
        }

        internal void UseController(CompositeDeviceModel nextController)
        {
            StopTimer();
            controller = nextController ??
                throw new ArgumentNullException(nameof(nextController));
            controllerDevice = nextController.Device;
            ControllerUiCapabilities capabilities =
                ControllerUiCapabilities.ForDevice(controllerDevice);
            viewModel = new ControllerTesterViewModel(capabilities,
                nextController.ControllerDisplayName);
            DataContext = viewModel;
            viewModel.ApplySnapshot(CaptureSnapshot());
            UpdateTraceVisuals();
            StartTimerIfVisible();
        }

        private void ControllerTesterControl_Loaded(object sender,
            RoutedEventArgs e)
        {
            EnsureTracePoints();
            StartTimerIfVisible();
        }

        private void ControllerTesterControl_Unloaded(object sender,
            RoutedEventArgs e) => StopTimer();

        private void ControllerTesterControl_IsVisibleChanged(object sender,
            DependencyPropertyChangedEventArgs e)
        {
            if (IsVisible)
            {
                StartTimerIfVisible();
            }
            else
            {
                StopTimer();
            }
        }

        private void StartTimerIfVisible()
        {
            if (!IsLoaded || !IsVisible || !hostVisible ||
                controller == null || timerEnabled)
            {
                return;
            }

            timerEnabled = true;
            readingTimer.Start();
        }

        private void StopTimer()
        {
            timerEnabled = false;
            readingTimer.Stop();
        }

        private void ReadingTimer_Elapsed(object sender,
            System.Timers.ElapsedEventArgs e)
        {
            ControllerTesterSnapshot snapshot = CaptureSnapshot();
            if (!timerEnabled) return;

            try
            {
                // Exactly one dispatcher transition per timer tick. All live
                // reads and the immutable copy happened above this boundary.
                Dispatcher.Invoke(() =>
                {
                    viewModel?.ApplySnapshot(snapshot);
                    UpdateTraceVisuals();
                });
            }
            catch (InvalidOperationException)
            {
                // The owner can close while this non-UI timer is between its
                // visibility check and dispatcher transition.
                timerEnabled = false;
            }

            if (timerEnabled)
            {
                readingTimer.Start();
            }
        }

        private ControllerTesterSnapshot CaptureSnapshot()
        {
            int deviceIndex = DeviceIndex;
            if (!IsCurrentControllerConnected(deviceIndex))
            {
                return ControllerTesterSnapshot.Disconnected;
            }

            DS4Device device = controllerDevice;
            bool readGateOwned = false;
            try
            {
                DS4State raw = App.rootHub.getDS4State(deviceIndex);
                DS4State mapped = App.rootHub.getDS4StateTemp(deviceIndex);
                if (raw == null || mapped == null)
                {
                    return ControllerTesterSnapshot.Disconnected;
                }

                // Match the established ControllerReadingsControl gate. The
                // snapshot copies motion and touch primitives before release,
                // so no mutable DS4State reference crosses to the dispatcher.
                device.ReadWaitEv.Wait();
                device.ReadWaitEv.Reset();
                readGateOwned = true;
                if (!IsCurrentControllerConnected(deviceIndex))
                {
                    return ControllerTesterSnapshot.Disconnected;
                }

                return ControllerTesterSnapshot.Capture(raw, mapped,
                    Global.ProfilePath[deviceIndex],
                    Global.LSModInfo[deviceIndex],
                    Global.RSModInfo[deviceIndex],
                    Global.L2ModInfo[deviceIndex],
                    Global.R2ModInfo[deviceIndex]);
            }
            catch (ObjectDisposedException)
            {
                return ControllerTesterSnapshot.Disconnected;
            }
            catch (IndexOutOfRangeException)
            {
                return ControllerTesterSnapshot.Disconnected;
            }
            finally
            {
                if (readGateOwned)
                {
                    device.ReadWaitEv.Set();
                }
            }
        }

        private bool IsCurrentControllerConnected(int deviceIndex)
        {
            return deviceIndex >= 0 &&
                deviceIndex < ControlService.CURRENT_DS4_CONTROLLER_LIMIT &&
                App.rootHub?.DS4Controllers != null &&
                ReferenceEquals(App.rootHub.DS4Controllers[deviceIndex],
                    controllerDevice);
        }

        private async void RumbleTestButton_Click(object sender,
            RoutedEventArgs e)
        {
            int deviceIndex = DeviceIndex;
            if (viewModel?.CanTestRumble != true ||
                !IsCurrentControllerConnected(deviceIndex) ||
                controllerDevice is not IControllerTransientRumbleTarget target)
            {
                return;
            }

            viewModel.SetRumbleTestInProgress(true);
            ControllerRumblePulse pulse = null;
            try
            {
                pulse = ControllerRumblePulse.Begin(target);
                await Task.Delay(ControllerRumblePulse.Duration);
            }
            finally
            {
                pulse?.Restore();
                viewModel.SetRumbleTestInProgress(false);
            }
        }

        private async void LightbarTestButton_Click(object sender,
            RoutedEventArgs e)
        {
            int deviceIndex = DeviceIndex;
            if (viewModel?.CanTestLightbar != true ||
                !IsCurrentControllerConnected(deviceIndex))
            {
                return;
            }

            viewModel.SetLightbarTestInProgress(true);
            try
            {
                // This is the 4.2 path: its lease flashes through the existing
                // forced-lightbar composition and conditionally restores it.
                await controller.IdentifyLightbarAsync();
            }
            finally
            {
                viewModel.SetLightbarTestInProgress(false);
            }
        }

        private void CalibrationButton_Click(object sender,
            RoutedEventArgs e)
        {
            if (viewModel?.CanCalibrate != true ||
                sender is not Button button ||
                !int.TryParse(button.Tag?.ToString(), out int stickIndex))
            {
                return;
            }

            Stick stick = stickIndex == 0 ? Stick.Left : Stick.Right;
            var window = new StickCalibrationWindow(stick, DeviceIndex,
                SaveCalibration)
            {
                Owner = Window.GetWindow(this),
            };
            window.ShowDialog();
        }

        private void SaveCalibration(Stick stick, sbyte xOffset,
            sbyte yOffset)
        {
            int deviceIndex = DeviceIndex;
            if (!IsCurrentControllerConnected(deviceIndex)) return;

            if (stick == Stick.Left)
            {
                Global.LeftStickDriftXAxis[deviceIndex] = xOffset;
                Global.LeftStickDriftYAxis[deviceIndex] = yOffset;
            }
            else
            {
                Global.RightStickDriftXAxis[deviceIndex] = xOffset;
                Global.RightStickDriftYAxis[deviceIndex] = yOffset;
            }

            string profileName = Global.ProfilePath[deviceIndex];
            if (!string.IsNullOrWhiteSpace(profileName))
            {
                Global.SaveProfile(deviceIndex, profileName);
                Global.CacheExtraProfileInfo(deviceIndex);
            }
        }

        private void EnsureTracePoints()
        {
            if (tracePointsInitialized) return;
            for (int i = 0; i < ControllerTesterViewModel.TraceLength; i++)
            {
                double x = i / (double)(ControllerTesterViewModel.TraceLength - 1) *
                    TraceWidth;
                GyroTraceLine.Points.Add(new Point(x, TraceHeight));
                AccelTraceLine.Points.Add(new Point(x, TraceHeight));
            }

            tracePointsInitialized = true;
        }

        private void UpdateTraceVisuals()
        {
            if (viewModel == null) return;
            EnsureTracePoints();
            UpdateTrace(GyroTraceLine, viewModel.GyroTrace,
                maximum: 60.0);
            UpdateTrace(AccelTraceLine, viewModel.AccelTrace,
                maximum: 2.0);
        }

        private static void UpdateTrace(Polyline polyline,
            FixedRollingTrace trace, double maximum)
        {
            for (int i = 0; i < trace.Capacity; i++)
            {
                Point point = polyline.Points[i];
                double sample = Math.Clamp(trace.GetChronological(i),
                    0.0, maximum);
                point.Y = TraceHeight - sample / maximum * TraceHeight;
                polyline.Points[i] = point;
            }
        }
    }
}
