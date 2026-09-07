/*
DS4Windows
Copyright (C) 2023  Travis Nickles

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.
*/

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using DS4Windows;

namespace DS4WinWPF.DS4Forms.ViewModels
{
    [Flags]
    internal enum ControllerTesterButtons : ulong
    {
        None = 0,
        Square = 1UL << 0,
        Triangle = 1UL << 1,
        Circle = 1UL << 2,
        Cross = 1UL << 3,
        DpadUp = 1UL << 4,
        DpadRight = 1UL << 5,
        DpadDown = 1UL << 6,
        DpadLeft = 1UL << 7,
        L1 = 1UL << 8,
        L2 = 1UL << 9,
        L3 = 1UL << 10,
        R1 = 1UL << 11,
        R2 = 1UL << 12,
        R3 = 1UL << 13,
        Share = 1UL << 14,
        Options = 1UL << 15,
        PS = 1UL << 16,
        Touchpad = 1UL << 17,
        Mute = 1UL << 18,
        Capture = 1UL << 19,
        SideL = 1UL << 20,
        SideR = 1UL << 21,
        FnL = 1UL << 22,
        FnR = 1UL << 23,
        BLP = 1UL << 24,
        BRP = 1UL << 25,
    }

    internal readonly struct ControllerTouchSnapshot
    {
        internal ControllerTouchSnapshot(bool isActive, byte id,
            short x, short y)
        {
            IsActive = isActive;
            Id = id;
            X = x;
            Y = y;
        }

        internal bool IsActive { get; }
        internal byte Id { get; }
        internal short X { get; }
        internal short Y { get; }
    }

    internal readonly struct StickProfileSnapshot : IEquatable<StickProfileSnapshot>
    {
        internal StickProfileSnapshot(bool isAxial,
            double deadZoneX, double deadZoneY,
            double antiDeadZoneX, double antiDeadZoneY,
            double maxZoneX, double maxZoneY)
        {
            IsAxial = isAxial;
            DeadZoneX = Math.Clamp(deadZoneX, 0.0, 1.0);
            DeadZoneY = Math.Clamp(deadZoneY, 0.0, 1.0);
            AntiDeadZoneX = Math.Clamp(antiDeadZoneX, 0.0, 1.0);
            AntiDeadZoneY = Math.Clamp(antiDeadZoneY, 0.0, 1.0);
            MaxZoneX = Math.Clamp(maxZoneX, 0.0, 1.0);
            MaxZoneY = Math.Clamp(maxZoneY, 0.0, 1.0);
        }

        internal bool IsAxial { get; }
        internal double DeadZoneX { get; }
        internal double DeadZoneY { get; }
        internal double AntiDeadZoneX { get; }
        internal double AntiDeadZoneY { get; }
        internal double MaxZoneX { get; }
        internal double MaxZoneY { get; }

        internal static StickProfileSnapshot Capture(StickDeadZoneInfo info)
        {
            if (info.deadzoneType == StickDeadZoneInfo.DeadZoneType.Axial)
            {
                return new StickProfileSnapshot(true,
                    info.xAxisDeadInfo.deadZone / 127.0,
                    info.yAxisDeadInfo.deadZone / 127.0,
                    info.xAxisDeadInfo.antiDeadZone / 100.0,
                    info.yAxisDeadInfo.antiDeadZone / 100.0,
                    info.xAxisDeadInfo.maxZone / 100.0,
                    info.yAxisDeadInfo.maxZone / 100.0);
            }

            return new StickProfileSnapshot(false,
                info.deadZone / 127.0, info.deadZone / 127.0,
                info.antiDeadZone / 100.0,
                info.antiDeadZone / 100.0,
                info.maxZone / 100.0, info.maxZone / 100.0);
        }

        public bool Equals(StickProfileSnapshot other) =>
            IsAxial == other.IsAxial &&
            DeadZoneX.Equals(other.DeadZoneX) &&
            DeadZoneY.Equals(other.DeadZoneY) &&
            AntiDeadZoneX.Equals(other.AntiDeadZoneX) &&
            AntiDeadZoneY.Equals(other.AntiDeadZoneY) &&
            MaxZoneX.Equals(other.MaxZoneX) &&
            MaxZoneY.Equals(other.MaxZoneY);

        public override bool Equals(object obj) =>
            obj is StickProfileSnapshot other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(IsAxial,
            DeadZoneX, DeadZoneY, AntiDeadZoneX, AntiDeadZoneY,
            MaxZoneX, MaxZoneY);
    }

    internal readonly struct TriggerProfileSnapshot
    {
        internal TriggerProfileSnapshot(byte deadZone, int maxZone)
        {
            DeadZone = deadZone;
            MaxZone = Math.Clamp(maxZone, 0, 100);
        }

        internal byte DeadZone { get; }
        internal int MaxZone { get; }
    }

    /// <summary>
    /// Immutable, UI-less input sample. The live control creates one while it
    /// owns the controller read gate; the view-model never sees a device.
    /// </summary>
    internal readonly struct ControllerTesterSnapshot
    {
        private ControllerTesterSnapshot(bool isConnected,
            ControllerTesterButtons buttons,
            byte lx, byte ly, byte rx, byte ry, byte l2, byte r2,
            byte mappedLx, byte mappedLy, byte mappedRx, byte mappedRy,
            byte mappedL2, byte mappedR2,
            double gyroYaw, double gyroPitch, double gyroRoll,
            double accelX, double accelY, double accelZ,
            ControllerTouchSnapshot touch0,
            ControllerTouchSnapshot touch1,
            string profileName, StickProfileSnapshot leftStickProfile,
            StickProfileSnapshot rightStickProfile,
            TriggerProfileSnapshot leftTriggerProfile,
            TriggerProfileSnapshot rightTriggerProfile)
        {
            IsConnected = isConnected;
            Buttons = buttons;
            LX = lx;
            LY = ly;
            RX = rx;
            RY = ry;
            L2 = l2;
            R2 = r2;
            MappedLX = mappedLx;
            MappedLY = mappedLy;
            MappedRX = mappedRx;
            MappedRY = mappedRy;
            MappedL2 = mappedL2;
            MappedR2 = mappedR2;
            GyroYaw = gyroYaw;
            GyroPitch = gyroPitch;
            GyroRoll = gyroRoll;
            AccelX = accelX;
            AccelY = accelY;
            AccelZ = accelZ;
            Touch0 = touch0;
            Touch1 = touch1;
            ProfileName = profileName ?? string.Empty;
            LeftStickProfile = leftStickProfile;
            RightStickProfile = rightStickProfile;
            LeftTriggerProfile = leftTriggerProfile;
            RightTriggerProfile = rightTriggerProfile;
        }

        internal bool IsConnected { get; }
        internal ControllerTesterButtons Buttons { get; }
        internal byte LX { get; }
        internal byte LY { get; }
        internal byte RX { get; }
        internal byte RY { get; }
        internal byte L2 { get; }
        internal byte R2 { get; }
        internal byte MappedLX { get; }
        internal byte MappedLY { get; }
        internal byte MappedRX { get; }
        internal byte MappedRY { get; }
        internal byte MappedL2 { get; }
        internal byte MappedR2 { get; }
        internal double GyroYaw { get; }
        internal double GyroPitch { get; }
        internal double GyroRoll { get; }
        internal double AccelX { get; }
        internal double AccelY { get; }
        internal double AccelZ { get; }
        internal ControllerTouchSnapshot Touch0 { get; }
        internal ControllerTouchSnapshot Touch1 { get; }
        internal string ProfileName { get; }
        internal StickProfileSnapshot LeftStickProfile { get; }
        internal StickProfileSnapshot RightStickProfile { get; }
        internal TriggerProfileSnapshot LeftTriggerProfile { get; }
        internal TriggerProfileSnapshot RightTriggerProfile { get; }

        internal static ControllerTesterSnapshot Disconnected => default;

        internal static ControllerTesterSnapshot Capture(DS4State raw,
            DS4State mapped, string profileName,
            StickDeadZoneInfo leftStick, StickDeadZoneInfo rightStick,
            TriggerDeadZoneZInfo leftTrigger,
            TriggerDeadZoneZInfo rightTrigger)
        {
            ControllerTesterButtons buttons = ControllerTesterButtons.None;
            AddButton(ref buttons, raw.Square, ControllerTesterButtons.Square);
            AddButton(ref buttons, raw.Triangle, ControllerTesterButtons.Triangle);
            AddButton(ref buttons, raw.Circle, ControllerTesterButtons.Circle);
            AddButton(ref buttons, raw.Cross, ControllerTesterButtons.Cross);
            AddButton(ref buttons, raw.DpadUp, ControllerTesterButtons.DpadUp);
            AddButton(ref buttons, raw.DpadRight, ControllerTesterButtons.DpadRight);
            AddButton(ref buttons, raw.DpadDown, ControllerTesterButtons.DpadDown);
            AddButton(ref buttons, raw.DpadLeft, ControllerTesterButtons.DpadLeft);
            AddButton(ref buttons, raw.L1, ControllerTesterButtons.L1);
            AddButton(ref buttons, raw.L2Btn, ControllerTesterButtons.L2);
            AddButton(ref buttons, raw.L3, ControllerTesterButtons.L3);
            AddButton(ref buttons, raw.R1, ControllerTesterButtons.R1);
            AddButton(ref buttons, raw.R2Btn, ControllerTesterButtons.R2);
            AddButton(ref buttons, raw.R3, ControllerTesterButtons.R3);
            AddButton(ref buttons, raw.Share, ControllerTesterButtons.Share);
            AddButton(ref buttons, raw.Options, ControllerTesterButtons.Options);
            AddButton(ref buttons, raw.PS, ControllerTesterButtons.PS);
            AddButton(ref buttons, raw.TouchButton, ControllerTesterButtons.Touchpad);
            AddButton(ref buttons, raw.Mute, ControllerTesterButtons.Mute);
            AddButton(ref buttons, raw.Capture, ControllerTesterButtons.Capture);
            AddButton(ref buttons, raw.SideL, ControllerTesterButtons.SideL);
            AddButton(ref buttons, raw.SideR, ControllerTesterButtons.SideR);
            AddButton(ref buttons, raw.FnL, ControllerTesterButtons.FnL);
            AddButton(ref buttons, raw.FnR, ControllerTesterButtons.FnR);
            AddButton(ref buttons, raw.BLP, ControllerTesterButtons.BLP);
            AddButton(ref buttons, raw.BRP, ControllerTesterButtons.BRP);

            SixAxis motion = raw.Motion;
            return new ControllerTesterSnapshot(true, buttons,
                raw.LX, raw.LY, raw.RX, raw.RY, raw.L2, raw.R2,
                mapped.LX, mapped.LY, mapped.RX, mapped.RY,
                mapped.L2, mapped.R2,
                motion?.angVelYaw ?? 0.0,
                motion?.angVelPitch ?? 0.0,
                motion?.angVelRoll ?? 0.0,
                motion?.accelXG ?? 0.0,
                motion?.accelYG ?? 0.0,
                motion?.accelZG ?? 0.0,
                CaptureTouch(raw.TrackPadTouch0),
                CaptureTouch(raw.TrackPadTouch1),
                profileName, StickProfileSnapshot.Capture(leftStick),
                StickProfileSnapshot.Capture(rightStick),
                new TriggerProfileSnapshot(leftTrigger.deadZone,
                    leftTrigger.maxZone),
                new TriggerProfileSnapshot(rightTrigger.deadZone,
                    rightTrigger.maxZone));
        }

        private static ControllerTouchSnapshot CaptureTouch(
            DS4State.TrackPadTouch touch) =>
            new(touch.IsActive, touch.Id, touch.X, touch.Y);

        private static void AddButton(ref ControllerTesterButtons buttons,
            bool pressed, ControllerTesterButtons button)
        {
            if (pressed)
            {
                buttons |= button;
            }
        }
    }

    internal readonly struct StickOverlayGeometry
    {
        private StickOverlayGeometry(double plotSize,
            StickProfileSnapshot profile)
        {
            PlotSize = plotSize;
            IsAxial = profile.IsAxial;
            DeadZoneWidth = profile.DeadZoneX * plotSize;
            DeadZoneHeight = profile.DeadZoneY * plotSize;
            AntiDeadZoneWidth = profile.AntiDeadZoneX * plotSize;
            AntiDeadZoneHeight = profile.AntiDeadZoneY * plotSize;
            MaxZoneWidth = profile.MaxZoneX * plotSize;
            MaxZoneHeight = profile.MaxZoneY * plotSize;
        }

        internal double PlotSize { get; }
        internal bool IsAxial { get; }
        internal double DeadZoneWidth { get; }
        internal double DeadZoneHeight { get; }
        internal double AntiDeadZoneWidth { get; }
        internal double AntiDeadZoneHeight { get; }
        internal double MaxZoneWidth { get; }
        internal double MaxZoneHeight { get; }
        internal double DeadZoneLeft => Center(DeadZoneWidth);
        internal double DeadZoneTop => Center(DeadZoneHeight);
        internal double AntiDeadZoneLeft => Center(AntiDeadZoneWidth);
        internal double AntiDeadZoneTop => Center(AntiDeadZoneHeight);
        internal double MaxZoneLeft => Center(MaxZoneWidth);
        internal double MaxZoneTop => Center(MaxZoneHeight);

        internal static StickOverlayGeometry Calculate(double plotSize,
            StickProfileSnapshot profile) =>
            new(Math.Max(0.0, plotSize), profile);

        private double Center(double size) => (PlotSize - size) / 2.0;
    }

    internal readonly struct ControllerTouchProjection
    {
        private ControllerTouchProjection(bool isActive, byte id,
            double left, double top)
        {
            IsActive = isActive;
            Id = id;
            Left = left;
            Top = top;
        }

        internal bool IsActive { get; }
        internal byte Id { get; }
        internal double Left { get; }
        internal double Top { get; }

        internal static ControllerTouchProjection Project(
            ControllerTouchSnapshot touch, int sourceWidth, int sourceHeight,
            double targetWidth, double targetHeight, double dotSize)
        {
            if (!touch.IsActive || sourceWidth <= 0 || sourceHeight <= 0)
            {
                return default;
            }

            double availableWidth = Math.Max(0.0, targetWidth - dotSize);
            double availableHeight = Math.Max(0.0, targetHeight - dotSize);
            double left = Math.Clamp(touch.X, (short)0,
                (short)sourceWidth) / (double)sourceWidth * availableWidth;
            double top = Math.Clamp(touch.Y, (short)0,
                (short)sourceHeight) / (double)sourceHeight * availableHeight;
            return new ControllerTouchProjection(true, touch.Id, left, top);
        }
    }

    internal abstract class ControllerTesterNotifyBase : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected void Raise([CallerMemberName] string propertyName = null) =>
            PropertyChanged?.Invoke(this,
                new PropertyChangedEventArgs(propertyName));

        protected void RaiseMany(params string[] propertyNames)
        {
            foreach (string propertyName in propertyNames)
            {
                Raise(propertyName);
            }
        }
    }

    internal sealed class ControllerButtonState : ControllerTesterNotifyBase
    {
        private bool isPressed;

        internal ControllerButtonState(string name,
            ControllerTesterButtons mask, bool isVisible = true)
        {
            Name = name;
            Mask = mask;
            IsVisible = isVisible;
        }

        public string Name { get; }
        internal ControllerTesterButtons Mask { get; }
        public bool IsVisible { get; }
        public bool IsPressed
        {
            get => isPressed;
            private set
            {
                if (isPressed == value) return;
                isPressed = value;
                Raise();
            }
        }

        internal void Update(ControllerTesterButtons buttons) =>
            IsPressed = (buttons & Mask) != 0;
    }

    internal sealed class ControllerAxisState : ControllerTesterNotifyBase
    {
        private int rawValue;
        private int mappedValue;

        internal ControllerAxisState(string name)
        {
            Name = name;
        }

        public string Name { get; }
        public int RawValue => rawValue;
        public int MappedValue => mappedValue;

        internal void Update(byte raw, byte mapped)
        {
            if (rawValue != raw)
            {
                rawValue = raw;
                Raise(nameof(RawValue));
            }

            if (mappedValue != mapped)
            {
                mappedValue = mapped;
                Raise(nameof(MappedValue));
            }
        }
    }

    internal sealed class StickDisplayState : ControllerTesterNotifyBase
    {
        internal const double PlotSize = 180.0;
        private const double DotSize = 10.0;
        private StickProfileSnapshot profile;
        private StickOverlayGeometry geometry;
        private double rawLeft;
        private double rawTop;
        private double mappedLeft;
        private double mappedTop;
        private string overlaySummary = string.Empty;

        internal StickDisplayState(string name)
        {
            Name = name;
            geometry = StickOverlayGeometry.Calculate(PlotSize, profile);
        }

        public string Name { get; }
        public bool IsAxial => geometry.IsAxial;
        public bool IsRadial => !geometry.IsAxial;
        public double DeadZoneWidth => geometry.DeadZoneWidth;
        public double DeadZoneHeight => geometry.DeadZoneHeight;
        public double DeadZoneLeft => geometry.DeadZoneLeft;
        public double DeadZoneTop => geometry.DeadZoneTop;
        public double AntiDeadZoneWidth => geometry.AntiDeadZoneWidth;
        public double AntiDeadZoneHeight => geometry.AntiDeadZoneHeight;
        public double AntiDeadZoneLeft => geometry.AntiDeadZoneLeft;
        public double AntiDeadZoneTop => geometry.AntiDeadZoneTop;
        public double MaxZoneWidth => geometry.MaxZoneWidth;
        public double MaxZoneHeight => geometry.MaxZoneHeight;
        public double MaxZoneLeft => geometry.MaxZoneLeft;
        public double MaxZoneTop => geometry.MaxZoneTop;
        public double RawLeft => rawLeft;
        public double RawTop => rawTop;
        public double MappedLeft => mappedLeft;
        public double MappedTop => mappedTop;
        public string OverlaySummary => overlaySummary;

        internal void Update(byte rawX, byte rawY, byte mappedX,
            byte mappedY, StickProfileSnapshot nextProfile)
        {
            UpdatePoint(rawX, rawY, ref rawLeft, ref rawTop,
                nameof(RawLeft), nameof(RawTop));
            UpdatePoint(mappedX, mappedY, ref mappedLeft, ref mappedTop,
                nameof(MappedLeft), nameof(MappedTop));

            if (profile.Equals(nextProfile)) return;
            profile = nextProfile;
            geometry = StickOverlayGeometry.Calculate(PlotSize, profile);
            overlaySummary = profile.IsAxial
                ? $"Axial · dead {profile.DeadZoneX:P0}/{profile.DeadZoneY:P0} · anti {profile.AntiDeadZoneX:P0}/{profile.AntiDeadZoneY:P0} · max {profile.MaxZoneX:P0}/{profile.MaxZoneY:P0}"
                : $"Radial · dead {profile.DeadZoneX:P0} · anti output {profile.AntiDeadZoneX:P0} · max {profile.MaxZoneX:P0}";
            RaiseMany(nameof(IsAxial), nameof(IsRadial),
                nameof(DeadZoneWidth), nameof(DeadZoneHeight),
                nameof(DeadZoneLeft), nameof(DeadZoneTop),
                nameof(AntiDeadZoneWidth), nameof(AntiDeadZoneHeight),
                nameof(AntiDeadZoneLeft), nameof(AntiDeadZoneTop),
                nameof(MaxZoneWidth), nameof(MaxZoneHeight),
                nameof(MaxZoneLeft), nameof(MaxZoneTop),
                nameof(OverlaySummary));
        }

        internal void ResetPosition()
        {
            UpdatePoint(128, 128, ref rawLeft, ref rawTop,
                nameof(RawLeft), nameof(RawTop));
            UpdatePoint(128, 128, ref mappedLeft, ref mappedTop,
                nameof(MappedLeft), nameof(MappedTop));
        }

        private void UpdatePoint(byte x, byte y, ref double left,
            ref double top, string leftProperty, string topProperty)
        {
            double nextLeft = x / 255.0 * PlotSize - DotSize / 2.0;
            double nextTop = y / 255.0 * PlotSize - DotSize / 2.0;
            if (!left.Equals(nextLeft))
            {
                left = nextLeft;
                Raise(leftProperty);
            }

            if (!top.Equals(nextTop))
            {
                top = nextTop;
                Raise(topProperty);
            }
        }
    }

    internal sealed class TriggerDisplayState : ControllerTesterNotifyBase
    {
        internal const double BarWidth = 280.0;
        private int rawValue;
        private int mappedValue;
        private byte deadZone;
        private int maxZone = 100;

        internal TriggerDisplayState(string name)
        {
            Name = name;
        }

        public string Name { get; }
        public int RawValue => rawValue;
        public int MappedValue => mappedValue;
        public double DeadZonePosition => deadZone / 255.0 * BarWidth;
        public double MaxZonePosition => maxZone / 100.0 * BarWidth;
        public string ZoneSummary =>
            $"Dead {deadZone / 255.0:P0} · max {maxZone / 100.0:P0}";

        internal void Update(byte raw, byte mapped,
            TriggerProfileSnapshot profile)
        {
            if (rawValue != raw)
            {
                rawValue = raw;
                Raise(nameof(RawValue));
            }

            if (mappedValue != mapped)
            {
                mappedValue = mapped;
                Raise(nameof(MappedValue));
            }

            if (deadZone != profile.DeadZone || maxZone != profile.MaxZone)
            {
                deadZone = profile.DeadZone;
                maxZone = profile.MaxZone;
                RaiseMany(nameof(DeadZonePosition), nameof(MaxZonePosition),
                    nameof(ZoneSummary));
            }
        }

        internal void ResetValues()
        {
            if (rawValue != 0)
            {
                rawValue = 0;
                Raise(nameof(RawValue));
            }

            if (mappedValue != 0)
            {
                mappedValue = 0;
                Raise(nameof(MappedValue));
            }
        }
    }

    internal sealed class TouchPointState : ControllerTesterNotifyBase
    {
        private bool isActive;
        private byte id;
        private double left;
        private double top;

        public bool IsActive => isActive;
        public byte Id => id;
        public string Label => $"#{id}";
        public double Left => left;
        public double Top => top;

        internal void Update(ControllerTouchProjection projection)
        {
            if (isActive != projection.IsActive)
            {
                isActive = projection.IsActive;
                Raise(nameof(IsActive));
            }

            if (id != projection.Id)
            {
                id = projection.Id;
                RaiseMany(nameof(Id), nameof(Label));
            }

            if (!left.Equals(projection.Left))
            {
                left = projection.Left;
                Raise(nameof(Left));
            }

            if (!top.Equals(projection.Top))
            {
                top = projection.Top;
                Raise(nameof(Top));
            }
        }
    }

    internal sealed class FixedRollingTrace
    {
        private readonly double[] samples;
        private int nextIndex;
        private int count;

        internal FixedRollingTrace(int capacity)
        {
            samples = new double[capacity];
        }

        internal int Capacity => samples.Length;
        internal int Count => count;

        internal void Add(double sample)
        {
            samples[nextIndex] = sample;
            nextIndex = (nextIndex + 1) % samples.Length;
            if (count < samples.Length) count++;
        }

        internal double GetChronological(int index)
        {
            int padding = samples.Length - count;
            if (index < padding) return 0.0;
            int oldest = count == samples.Length ? nextIndex : 0;
            return samples[(oldest + index - padding) % samples.Length];
        }

        internal void Clear()
        {
            Array.Clear(samples, 0, samples.Length);
            nextIndex = 0;
            count = 0;
        }
    }

    internal sealed class ControllerTesterViewModel : ControllerTesterNotifyBase
    {
        internal const double DriftThresholdDegreesPerSecond = 2.0;
        internal const int TraceLength = 90;
        private const int MinimumDriftSamples = 30;
        private const double TouchWidth = 320.0;
        private const double TouchDotSize = 26.0;

        private readonly ControllerUiCapabilities capabilities;
        private readonly double[] driftMagnitudes = new double[TraceLength];
        private int driftNextIndex;
        private int driftCount;
        private double driftSum;
        private bool isConnected;
        private string statusMessage = "Controller disconnected";
        private string activeProfileName = string.Empty;
        private double gyroYaw;
        private double gyroPitch;
        private double gyroRoll;
        private double accelX;
        private double accelY;
        private double accelZ;
        private double driftMean;
        private string driftVerdict = "Measuring…";
        private bool driftIsCalm;
        private bool rumbleTestInProgress;
        private bool lightbarTestInProgress;

        internal ControllerTesterViewModel(ControllerUiCapabilities capabilities,
            string controllerName)
        {
            this.capabilities = capabilities ??
                throw new ArgumentNullException(nameof(capabilities));
            ControllerName = string.IsNullOrWhiteSpace(controllerName)
                ? capabilities.ControllerName
                : controllerName;
            SupportsGyro = capabilities.SupportsGyro;
            SupportsTouchpad = capabilities.SupportsTouchpad;
            SupportsRumble = capabilities.SupportsRumble;
            SupportsLightbar = capabilities.SupportsLightbar;
            TouchpadHeight = capabilities.SupportsTouchpad
                ? TouchWidth * capabilities.TouchpadHeight /
                    capabilities.TouchpadWidth
                : 0.0;

            Buttons = new ObservableCollection<ControllerButtonState>
            {
                new("Square / X", ControllerTesterButtons.Square),
                new("Triangle / Y", ControllerTesterButtons.Triangle),
                new("Circle / B", ControllerTesterButtons.Circle),
                new("Cross / A", ControllerTesterButtons.Cross),
                new("D-pad up", ControllerTesterButtons.DpadUp),
                new("D-pad right", ControllerTesterButtons.DpadRight),
                new("D-pad down", ControllerTesterButtons.DpadDown),
                new("D-pad left", ControllerTesterButtons.DpadLeft),
                new("L1", ControllerTesterButtons.L1),
                new("L2 button", ControllerTesterButtons.L2),
                new("L3", ControllerTesterButtons.L3),
                new("R1", ControllerTesterButtons.R1),
                new("R2 button", ControllerTesterButtons.R2),
                new("R3", ControllerTesterButtons.R3),
                new("Share / Minus", ControllerTesterButtons.Share),
                new("Options / Plus", ControllerTesterButtons.Options),
                new("PS / Home", ControllerTesterButtons.PS),
                new("Touchpad click", ControllerTesterButtons.Touchpad,
                    capabilities.SupportsTouchpad),
                new("Mute", ControllerTesterButtons.Mute,
                    capabilities.SupportsMuteButton),
                new("Capture", ControllerTesterButtons.Capture,
                    capabilities.IsLiveTesterControlAvailable(
                        DS4Controls.Capture)),
                new("Side L", ControllerTesterButtons.SideL,
                    capabilities.IsLiveTesterControlAvailable(
                        DS4Controls.SideL)),
                new("Side R", ControllerTesterButtons.SideR,
                    capabilities.IsLiveTesterControlAvailable(
                        DS4Controls.SideR)),
                new("Function L", ControllerTesterButtons.FnL,
                    capabilities.IsLiveTesterControlAvailable(
                        DS4Controls.FnL)),
                new("Function R", ControllerTesterButtons.FnR,
                    capabilities.IsLiveTesterControlAvailable(
                        DS4Controls.FnR)),
                new("Back paddle L", ControllerTesterButtons.BLP,
                    capabilities.IsLiveTesterControlAvailable(
                        DS4Controls.BLP)),
                new("Back paddle R", ControllerTesterButtons.BRP,
                    capabilities.IsLiveTesterControlAvailable(
                        DS4Controls.BRP)),
            };

            Axes = new ObservableCollection<ControllerAxisState>
            {
                new("LX"), new("LY"), new("RX"), new("RY"),
            };
            LeftStick = new StickDisplayState("Left stick");
            RightStick = new StickDisplayState("Right stick");
            Sticks = new ObservableCollection<StickDisplayState>
            {
                LeftStick, RightStick,
            };
            LeftTrigger = new TriggerDisplayState("L2");
            RightTrigger = new TriggerDisplayState("R2");
            Triggers = new ObservableCollection<TriggerDisplayState>
            {
                LeftTrigger, RightTrigger,
            };
            Touch0 = new TouchPointState();
            Touch1 = new TouchPointState();
            GyroTrace = new FixedRollingTrace(TraceLength);
            AccelTrace = new FixedRollingTrace(TraceLength);
        }

        public string ControllerName { get; }
        public bool SupportsGyro { get; }
        public bool SupportsTouchpad { get; }
        public bool SupportsRumble { get; }
        public bool SupportsLightbar { get; }
        public double TouchpadWidth => TouchWidth;
        public double TouchpadHeight { get; }
        public ObservableCollection<ControllerButtonState> Buttons { get; }
        public ObservableCollection<ControllerAxisState> Axes { get; }
        public StickDisplayState LeftStick { get; }
        public StickDisplayState RightStick { get; }
        public ObservableCollection<StickDisplayState> Sticks { get; }
        public TriggerDisplayState LeftTrigger { get; }
        public TriggerDisplayState RightTrigger { get; }
        public ObservableCollection<TriggerDisplayState> Triggers { get; }
        public TouchPointState Touch0 { get; }
        public TouchPointState Touch1 { get; }
        internal FixedRollingTrace GyroTrace { get; }
        internal FixedRollingTrace AccelTrace { get; }
        public bool IsConnected => isConnected;
        public bool IsDisconnected => !isConnected;
        public string StatusMessage => statusMessage;
        public string ActiveProfileName => activeProfileName;
        public double GyroYaw => gyroYaw;
        public double GyroPitch => gyroPitch;
        public double GyroRoll => gyroRoll;
        public double AccelX => accelX;
        public double AccelY => accelY;
        public double AccelZ => accelZ;
        public double DriftMean => driftMean;
        public string DriftVerdict => driftVerdict;
        public bool DriftIsCalm => driftIsCalm;
        public bool CanTestRumble => isConnected && SupportsRumble &&
            !rumbleTestInProgress;
        public bool CanTestLightbar => isConnected && SupportsLightbar &&
            !lightbarTestInProgress;
        public bool CanCalibrate => isConnected;

        internal void ApplySnapshot(ControllerTesterSnapshot snapshot)
        {
            if (!snapshot.IsConnected)
            {
                ApplyDisconnected();
                return;
            }

            SetConnected(true, "Reading live input");
            if (activeProfileName != snapshot.ProfileName)
            {
                activeProfileName = snapshot.ProfileName;
                Raise(nameof(ActiveProfileName));
            }

            foreach (ControllerButtonState button in Buttons)
            {
                button.Update(snapshot.Buttons);
            }

            Axes[0].Update(snapshot.LX, snapshot.MappedLX);
            Axes[1].Update(snapshot.LY, snapshot.MappedLY);
            Axes[2].Update(snapshot.RX, snapshot.MappedRX);
            Axes[3].Update(snapshot.RY, snapshot.MappedRY);
            LeftStick.Update(snapshot.LX, snapshot.LY,
                snapshot.MappedLX, snapshot.MappedLY,
                snapshot.LeftStickProfile);
            RightStick.Update(snapshot.RX, snapshot.RY,
                snapshot.MappedRX, snapshot.MappedRY,
                snapshot.RightStickProfile);
            LeftTrigger.Update(snapshot.L2, snapshot.MappedL2,
                snapshot.LeftTriggerProfile);
            RightTrigger.Update(snapshot.R2, snapshot.MappedR2,
                snapshot.RightTriggerProfile);
            UpdateMotion(snapshot);
            UpdateTouch(snapshot.Touch0, snapshot.Touch1);
        }

        internal void SetRumbleTestInProgress(bool value)
        {
            if (rumbleTestInProgress == value) return;
            rumbleTestInProgress = value;
            Raise(nameof(CanTestRumble));
        }

        internal void SetLightbarTestInProgress(bool value)
        {
            if (lightbarTestInProgress == value) return;
            lightbarTestInProgress = value;
            Raise(nameof(CanTestLightbar));
        }

        private void ApplyDisconnected()
        {
            SetConnected(false, "Controller disconnected");
            foreach (ControllerButtonState button in Buttons)
            {
                button.Update(ControllerTesterButtons.None);
            }

            foreach (ControllerAxisState axis in Axes)
            {
                axis.Update(128, 128);
            }

            LeftStick.ResetPosition();
            RightStick.ResetPosition();
            LeftTrigger.ResetValues();
            RightTrigger.ResetValues();
            UpdateNumber(ref gyroYaw, 0.0, nameof(GyroYaw));
            UpdateNumber(ref gyroPitch, 0.0, nameof(GyroPitch));
            UpdateNumber(ref gyroRoll, 0.0, nameof(GyroRoll));
            UpdateNumber(ref accelX, 0.0, nameof(AccelX));
            UpdateNumber(ref accelY, 0.0, nameof(AccelY));
            UpdateNumber(ref accelZ, 0.0, nameof(AccelZ));
            Touch0.Update(default);
            Touch1.Update(default);
            ResetDrift();
        }

        private void SetConnected(bool value, string message)
        {
            bool connectionChanged = isConnected != value;
            if (connectionChanged)
            {
                isConnected = value;
                RaiseMany(nameof(IsConnected), nameof(IsDisconnected),
                    nameof(CanTestRumble), nameof(CanTestLightbar),
                    nameof(CanCalibrate));
            }

            if (statusMessage != message)
            {
                statusMessage = message;
                Raise(nameof(StatusMessage));
            }
        }

        private void UpdateMotion(ControllerTesterSnapshot snapshot)
        {
            UpdateNumber(ref gyroYaw, snapshot.GyroYaw, nameof(GyroYaw));
            UpdateNumber(ref gyroPitch, snapshot.GyroPitch,
                nameof(GyroPitch));
            UpdateNumber(ref gyroRoll, snapshot.GyroRoll,
                nameof(GyroRoll));
            UpdateNumber(ref accelX, snapshot.AccelX, nameof(AccelX));
            UpdateNumber(ref accelY, snapshot.AccelY, nameof(AccelY));
            UpdateNumber(ref accelZ, snapshot.AccelZ, nameof(AccelZ));

            double gyroMagnitude = Math.Sqrt(
                snapshot.GyroYaw * snapshot.GyroYaw +
                snapshot.GyroPitch * snapshot.GyroPitch +
                snapshot.GyroRoll * snapshot.GyroRoll);
            double accelMagnitude = Math.Sqrt(
                snapshot.AccelX * snapshot.AccelX +
                snapshot.AccelY * snapshot.AccelY +
                snapshot.AccelZ * snapshot.AccelZ);
            GyroTrace.Add(gyroMagnitude);
            AccelTrace.Add(accelMagnitude);
            AddDriftMagnitude(gyroMagnitude);
        }

        private void AddDriftMagnitude(double magnitude)
        {
            if (driftCount == driftMagnitudes.Length)
            {
                driftSum -= driftMagnitudes[driftNextIndex];
            }
            else
            {
                driftCount++;
            }

            driftMagnitudes[driftNextIndex] = magnitude;
            driftSum += magnitude;
            driftNextIndex = (driftNextIndex + 1) % driftMagnitudes.Length;
            UpdateNumber(ref driftMean, driftSum / driftCount,
                nameof(DriftMean));

            string nextVerdict;
            bool nextCalm;
            if (driftCount < MinimumDriftSamples)
            {
                nextVerdict = "Measuring…";
                nextCalm = false;
            }
            else
            {
                nextCalm = driftMean <= DriftThresholdDegreesPerSecond;
                nextVerdict = nextCalm ? "Calm" : "Drifting";
            }

            if (driftIsCalm != nextCalm)
            {
                driftIsCalm = nextCalm;
                Raise(nameof(DriftIsCalm));
            }

            if (driftVerdict != nextVerdict)
            {
                driftVerdict = nextVerdict;
                Raise(nameof(DriftVerdict));
            }
        }

        private void UpdateTouch(ControllerTouchSnapshot first,
            ControllerTouchSnapshot second)
        {
            Touch0.Update(ControllerTouchProjection.Project(first,
                capabilities.TouchpadWidth, capabilities.TouchpadHeight,
                TouchpadWidth, TouchpadHeight, TouchDotSize));
            Touch1.Update(ControllerTouchProjection.Project(second,
                capabilities.TouchpadWidth, capabilities.TouchpadHeight,
                TouchpadWidth, TouchpadHeight, TouchDotSize));
        }

        private void ResetDrift()
        {
            Array.Clear(driftMagnitudes, 0, driftMagnitudes.Length);
            driftNextIndex = 0;
            driftCount = 0;
            driftSum = 0.0;
            GyroTrace.Clear();
            AccelTrace.Clear();
            UpdateNumber(ref driftMean, 0.0, nameof(DriftMean));
            if (driftVerdict != "Measuring…")
            {
                driftVerdict = "Measuring…";
                Raise(nameof(DriftVerdict));
            }

            if (driftIsCalm)
            {
                driftIsCalm = false;
                Raise(nameof(DriftIsCalm));
            }
        }

        private void UpdateNumber(ref double field, double value,
            string propertyName)
        {
            if (field.Equals(value)) return;
            field = value;
            Raise(propertyName);
        }
    }
}
