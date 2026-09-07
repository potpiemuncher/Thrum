using DS4Windows;
using DS4Windows.InputDevices;
using DS4WinWPF.DS4Forms.ViewModels;

namespace DS4WindowsTests
{
    [TestClass]
    public class ControllerTesterViewModelTests
    {
        [TestMethod]
        public void DeadzoneOverlayGeometryTracksAllActiveProfileValues()
        {
            var profile = new StickDeadZoneInfo
            {
                deadzoneType = StickDeadZoneInfo.DeadZoneType.Radial,
                deadZone = 32,
                antiDeadZone = 25,
                maxZone = 80,
            };

            StickProfileSnapshot snapshot =
                StickProfileSnapshot.Capture(profile);
            StickOverlayGeometry geometry =
                StickOverlayGeometry.Calculate(200.0, snapshot);

            Assert.AreEqual(32.0 / 127.0 * 200.0,
                geometry.DeadZoneWidth, 0.0001);
            Assert.AreEqual(50.0, geometry.AntiDeadZoneWidth, 0.0001,
                "Anti-deadzone is a distinct output-minimum overlay.");
            Assert.AreEqual(160.0, geometry.MaxZoneWidth, 0.0001);
            Assert.AreEqual((200.0 - geometry.DeadZoneWidth) / 2.0,
                geometry.DeadZoneLeft, 0.0001);
            Assert.IsFalse(geometry.IsAxial);
        }

        [TestMethod]
        public void AxialOverlayUsesIndependentProfileAxes()
        {
            var profile = new StickDeadZoneInfo
            {
                deadzoneType = StickDeadZoneInfo.DeadZoneType.Axial,
            };
            profile.xAxisDeadInfo.deadZone = 10;
            profile.yAxisDeadInfo.deadZone = 20;
            profile.xAxisDeadInfo.antiDeadZone = 15;
            profile.yAxisDeadInfo.antiDeadZone = 30;
            profile.xAxisDeadInfo.maxZone = 70;
            profile.yAxisDeadInfo.maxZone = 90;

            StickOverlayGeometry geometry =
                StickOverlayGeometry.Calculate(100.0,
                    StickProfileSnapshot.Capture(profile));

            Assert.IsTrue(geometry.IsAxial);
            Assert.AreEqual(10.0 / 127.0 * 100.0,
                geometry.DeadZoneWidth, 0.0001);
            Assert.AreEqual(20.0 / 127.0 * 100.0,
                geometry.DeadZoneHeight, 0.0001);
            Assert.AreEqual(15.0, geometry.AntiDeadZoneWidth, 0.0001);
            Assert.AreEqual(30.0, geometry.AntiDeadZoneHeight, 0.0001);
            Assert.AreEqual(70.0, geometry.MaxZoneWidth, 0.0001);
            Assert.AreEqual(90.0, geometry.MaxZoneHeight, 0.0001);
        }

        [TestMethod]
        public void DriftVerdictSeparatesCalmAndDriftingSyntheticSamples()
        {
            ControllerUiCapabilities capabilities =
                ControllerUiCapabilities.For(InputDeviceType.DualSense);
            var calm = new ControllerTesterViewModel(capabilities, "Calm pad");
            var drifting = new ControllerTesterViewModel(capabilities,
                "Drifting pad");

            for (int i = 0; i < 45; i++)
            {
                calm.ApplySnapshot(Sample(0.35, 0.25, 0.20));
                drifting.ApplySnapshot(Sample(3.2, 0.8, 0.4));
            }

            Assert.IsTrue(calm.DriftIsCalm);
            Assert.AreEqual("Calm", calm.DriftVerdict);
            Assert.IsTrue(calm.DriftMean <
                ControllerTesterViewModel.DriftThresholdDegreesPerSecond);
            Assert.IsFalse(drifting.DriftIsCalm);
            Assert.AreEqual("Drifting", drifting.DriftVerdict);
            Assert.IsTrue(drifting.DriftMean >
                ControllerTesterViewModel.DriftThresholdDegreesPerSecond);
        }

        [TestMethod]
        public void TouchDotsProjectTrackPadTouchCoordinatesAndIdentifiers()
        {
            ControllerUiCapabilities capabilities =
                ControllerUiCapabilities.For(InputDeviceType.DualSense);
            var viewModel = new ControllerTesterViewModel(capabilities,
                "Touch pad");
            var first = new DS4State.TrackPadTouch
            {
                IsActive = true,
                Id = 7,
                X = 960,
                Y = 540,
            };
            var second = new DS4State.TrackPadTouch
            {
                IsActive = true,
                Id = 11,
                X = 1920,
                Y = 1080,
            };

            viewModel.ApplySnapshot(Sample(0.0, 0.0, 0.0,
                first, second));

            Assert.IsTrue(viewModel.Touch0.IsActive);
            Assert.AreEqual((byte)7, viewModel.Touch0.Id);
            Assert.AreEqual((320.0 - 26.0) / 2.0,
                viewModel.Touch0.Left, 0.0001);
            Assert.AreEqual((180.0 - 26.0) / 2.0,
                viewModel.Touch0.Top, 0.0001);
            Assert.IsTrue(viewModel.Touch1.IsActive);
            Assert.AreEqual((byte)11, viewModel.Touch1.Id);
            Assert.AreEqual(294.0, viewModel.Touch1.Left, 0.0001);
            Assert.AreEqual(154.0, viewModel.Touch1.Top, 0.0001);
        }

        [TestMethod]
        public void TestActionsRequireBothCapabilityAndLiveConnection()
        {
            ControllerUiCapabilities writableDualShock4 =
                ControllerUiCapabilities.For(InputDeviceType.DS4);
            ControllerUiCapabilities noOutputDualShock4 =
                ControllerUiCapabilities.For(InputDeviceType.DS4,
                    ConnectionType.USB, 0x0F0D, 0x00EE,
                    VidPidFeatureSet.NoOutputData);
            var writable = new ControllerTesterViewModel(
                writableDualShock4, "Writable pad");
            var readOnly = new ControllerTesterViewModel(
                noOutputDualShock4, "Read-only pad");

            Assert.IsFalse(writable.CanTestRumble);
            Assert.IsFalse(writable.CanTestLightbar);
            writable.ApplySnapshot(Sample(0.0, 0.0, 0.0));
            readOnly.ApplySnapshot(Sample(0.0, 0.0, 0.0));

            Assert.IsTrue(writable.CanTestRumble);
            Assert.IsTrue(writable.CanTestLightbar);
            Assert.IsFalse(readOnly.SupportsRumble);
            Assert.IsFalse(readOnly.SupportsLightbar);
            Assert.IsFalse(readOnly.CanTestRumble);
            Assert.IsFalse(readOnly.CanTestLightbar);
        }

        [TestMethod]
        public void DisconnectWhileOpenClearsLiveStateAndDisablesActions()
        {
            ControllerUiCapabilities capabilities =
                ControllerUiCapabilities.For(InputDeviceType.DualSense);
            var viewModel = new ControllerTesterViewModel(capabilities,
                "Disconnecting pad");
            DS4State.TrackPadTouch touch = new()
            {
                IsActive = true,
                Id = 3,
                X = 600,
                Y = 400,
            };
            ControllerTesterSnapshot connected = Sample(0.4, 0.2, 0.1,
                touch, default, squarePressed: true, lx: 240);

            viewModel.ApplySnapshot(connected);
            Assert.IsTrue(viewModel.IsConnected);
            Assert.IsTrue(viewModel.Buttons.Single(button =>
                button.Mask == ControllerTesterButtons.Square).IsPressed);
            Assert.AreEqual(240, viewModel.Axes[0].RawValue);
            Assert.IsTrue(viewModel.Touch0.IsActive);

            viewModel.ApplySnapshot(ControllerTesterSnapshot.Disconnected);

            Assert.IsFalse(viewModel.IsConnected);
            Assert.AreEqual("Controller disconnected", viewModel.StatusMessage);
            Assert.IsTrue(viewModel.Buttons.All(button => !button.IsPressed));
            Assert.AreEqual(128, viewModel.Axes[0].RawValue);
            Assert.IsFalse(viewModel.Touch0.IsActive);
            Assert.IsFalse(viewModel.CanTestRumble);
            Assert.IsFalse(viewModel.CanTestLightbar);
            Assert.IsFalse(viewModel.CanCalibrate);
            Assert.AreEqual("Measuring…", viewModel.DriftVerdict);
        }

        [TestMethod]
        public void ProfileChangesReplaceOverlayWithoutRecreatingTheViewModel()
        {
            ControllerUiCapabilities capabilities =
                ControllerUiCapabilities.For(InputDeviceType.DS4);
            var viewModel = new ControllerTesterViewModel(capabilities,
                "Profile pad");
            var first = new StickDeadZoneInfo
            {
                deadZone = 12,
                antiDeadZone = 10,
                maxZone = 90,
            };
            var second = new StickDeadZoneInfo
            {
                deadZone = 30,
                antiDeadZone = 40,
                maxZone = 70,
            };

            viewModel.ApplySnapshot(Sample(0.0, 0.0, 0.0,
                leftProfile: first, profileName: "First"));
            double firstAntiWidth = viewModel.LeftStick.AntiDeadZoneWidth;
            viewModel.ApplySnapshot(Sample(0.0, 0.0, 0.0,
                leftProfile: second, profileName: "Second"));

            Assert.AreEqual("Second", viewModel.ActiveProfileName);
            Assert.AreNotEqual(firstAntiWidth,
                viewModel.LeftStick.AntiDeadZoneWidth);
            Assert.AreEqual(StickDisplayState.PlotSize * 0.40,
                viewModel.LeftStick.AntiDeadZoneWidth, 0.0001);
        }

        private static ControllerTesterSnapshot Sample(double yaw,
            double pitch, double roll,
            DS4State.TrackPadTouch firstTouch = default,
            DS4State.TrackPadTouch secondTouch = default,
            bool squarePressed = false, byte lx = 128,
            StickDeadZoneInfo leftProfile = null,
            string profileName = "Default")
        {
            var raw = new DS4State
            {
                Square = squarePressed,
                LX = lx,
                TrackPadTouch0 = firstTouch,
                TrackPadTouch1 = secondTouch,
            };
            raw.Motion.angVelYaw = yaw;
            raw.Motion.angVelPitch = pitch;
            raw.Motion.angVelRoll = roll;
            raw.Motion.accelXG = 0.1;
            raw.Motion.accelYG = 0.2;
            raw.Motion.accelZG = 0.98;

            var mapped = new DS4State
            {
                LX = lx,
            };
            return ControllerTesterSnapshot.Capture(raw, mapped,
                profileName, leftProfile ?? new StickDeadZoneInfo(),
                new StickDeadZoneInfo(), new TriggerDeadZoneZInfo(),
                new TriggerDeadZoneZInfo());
        }
    }
}
