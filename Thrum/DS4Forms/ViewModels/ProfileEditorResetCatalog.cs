using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using DS4Windows;

namespace DS4WinWPF.DS4Forms.ViewModels
{
    public sealed class ProfileEditorResetEntry
    {
        private readonly Func<BackingStore, int, object> defaultValue;

        public ProfileEditorResetEntry(string settingName,
            Func<BackingStore, int, object> defaultValue)
        {
            SettingName = settingName ??
                throw new ArgumentNullException(nameof(settingName));
            this.defaultValue = defaultValue ??
                throw new ArgumentNullException(nameof(defaultValue));
        }

        public string SettingName { get; }

        public object GetDefaultValue(BackingStore store, int deviceIndex)
        {
            if (store == null)
            {
                throw new ArgumentNullException(nameof(store));
            }

            return defaultValue(store, deviceIndex);
        }

        public void Reset(object settingsTarget, BackingStore defaultStore,
            int defaultDeviceIndex)
        {
            if (settingsTarget == null)
            {
                throw new ArgumentNullException(nameof(settingsTarget));
            }

            PropertyInfo property = settingsTarget.GetType().GetProperty(
                SettingName, BindingFlags.Instance | BindingFlags.Public);
            if (property == null || !property.CanWrite)
            {
                throw new InvalidOperationException(
                    $"Setting '{SettingName}' is not writable on " +
                    settingsTarget.GetType().Name + ".");
            }

            object value = GetDefaultValue(defaultStore, defaultDeviceIndex);
            if (value != null && !property.PropertyType.IsInstanceOfType(value))
            {
                value = Convert.ChangeType(value, property.PropertyType,
                    CultureInfo.InvariantCulture);
            }

            property.SetValue(settingsTarget, value);
        }
    }

    /// <summary>
    /// Maps the numeric settings in the three dense profile-editor rails to
    /// projections of a detached store initialized by ResetProfile. The table
    /// contains no default literals; it only preserves the UI conversions
    /// already used by ProfileSettingsViewModel.
    /// </summary>
    public static class ProfileEditorResetCatalog
    {
        private static readonly IReadOnlyDictionary<string,
            ProfileEditorResetEntry> Entries = CreateEntries();

        public static IEnumerable<string> Settings => Entries.Keys;

        public static bool TryGet(string settingName,
            out ProfileEditorResetEntry entry) =>
            Entries.TryGetValue(settingName, out entry);

        public static ProfileEditorResetEntry Get(string settingName) =>
            Entries[settingName];

        private static IReadOnlyDictionary<string,
            ProfileEditorResetEntry> CreateEntries()
        {
            Dictionary<string, ProfileEditorResetEntry> entries = new(
                StringComparer.Ordinal);

            void Add(string name, Func<BackingStore, int, object> value) =>
                entries.Add(name, new ProfileEditorResetEntry(name, value));

            Add("LeftStickDriftXAxis", (store, index) =>
                store.leftStickDriftXAxis[index]);
            Add("LeftStickDriftYAxis", (store, index) =>
                store.leftStickDriftYAxis[index]);
            Add("RightStickDriftXAxis", (store, index) =>
                store.rightStickDriftXAxis[index]);
            Add("RightStickDriftYAxis", (store, index) =>
                store.rightStickDriftYAxis[index]);

            Add("LSDeadZone", (store, index) =>
                Math.Round(store.lsModInfo[index].deadZone / 127d, 2));
            Add("LSMaxZone", (store, index) =>
                store.lsModInfo[index].maxZone / 100.0);
            Add("LSAntiDeadZone", (store, index) =>
                store.lsModInfo[index].antiDeadZone / 100.0);
            Add("LSMaxOutput", (store, index) =>
                store.lsModInfo[index].maxOutput / 100.0);
            Add("LSVerticalScale", (store, index) =>
                store.lsModInfo[index].verticalScale / 100.0);
            Add("LSSens", (store, index) => store.LSSens[index]);
            Add("LSSquareRoundness", (store, index) =>
                store.squStickInfo[index].lsRoundness);
            Add("LSRotation", (store, index) =>
                store.LSRotation[index] * 180.0 / Math.PI);
            Add("LSFuzz", (store, index) =>
                store.lsModInfo[index].fuzz);
            Add("LSAntiSnapbackDelta", (store, index) =>
                store.lsAntiSnapbackInfo[index].delta);
            Add("LSAntiSnapbackTimeout", (store, index) =>
                store.lsAntiSnapbackInfo[index].timeout);
            Add("LSOuterBindDead", (store, index) =>
                store.lsModInfo[index].outerBindDeadZone / 100.0);
            Add("LSDeltaMultiplier", (store, index) => store
                .lsOutputSettings[index].outputSettings.controlSettings
                .deltaAccelSettings.multiplier);
            Add("LSDeltaMaxTravel", (store, index) => store
                .lsOutputSettings[index].outputSettings.controlSettings
                .deltaAccelSettings.maxTravel);
            Add("LSDeltaMinTravel", (store, index) => store
                .lsOutputSettings[index].outputSettings.controlSettings
                .deltaAccelSettings.minTravel);
            Add("LSDeltaEasingDuration", (store, index) => store
                .lsOutputSettings[index].outputSettings.controlSettings
                .deltaAccelSettings.easingDuration);
            Add("LSDeltaMinFactor", (store, index) => store
                .lsOutputSettings[index].outputSettings.controlSettings
                .deltaAccelSettings.minfactor);
            Add("LSFlickRWC", (store, index) => store
                .lsOutputSettings[index].outputSettings.flickSettings
                .realWorldCalibration);
            Add("LSFlickThreshold", (store, index) => store
                .lsOutputSettings[index].outputSettings.flickSettings
                .flickThreshold);
            Add("LSFlickTime", (store, index) => store
                .lsOutputSettings[index].outputSettings.flickSettings
                .flickTime);
            Add("LSMinAngleThreshold", (store, index) => store
                .lsOutputSettings[index].outputSettings.flickSettings
                .minAngleThreshold);

            Add("RSDeadZone", (store, index) =>
                Math.Round(store.rsModInfo[index].deadZone / 127d, 2));
            Add("RSMaxZone", (store, index) =>
                store.rsModInfo[index].maxZone / 100.0);
            Add("RSAntiDeadZone", (store, index) =>
                store.rsModInfo[index].antiDeadZone / 100.0);
            Add("RSMaxOutput", (store, index) =>
                store.rsModInfo[index].maxOutput / 100.0);
            Add("RSVerticalScale", (store, index) =>
                store.rsModInfo[index].verticalScale / 100.0);
            Add("RSSens", (store, index) => store.RSSens[index]);
            Add("RSSquareRoundness", (store, index) =>
                store.squStickInfo[index].rsRoundness);
            Add("RSRotation", (store, index) =>
                store.RSRotation[index] * 180.0 / Math.PI);
            Add("RSFuzz", (store, index) =>
                store.rsModInfo[index].fuzz);
            Add("RSAntiSnapbackDelta", (store, index) =>
                store.rsAntiSnapbackInfo[index].delta);
            Add("RSAntiSnapbackTimeout", (store, index) =>
                store.rsAntiSnapbackInfo[index].timeout);
            Add("RSOuterBindDead", (store, index) =>
                store.rsModInfo[index].outerBindDeadZone / 100.0);
            Add("RSDeltaMultiplier", (store, index) => store
                .rsOutputSettings[index].outputSettings.controlSettings
                .deltaAccelSettings.multiplier);
            Add("RSDeltaMaxTravel", (store, index) => store
                .rsOutputSettings[index].outputSettings.controlSettings
                .deltaAccelSettings.maxTravel);
            Add("RSDeltaMinTravel", (store, index) => store
                .rsOutputSettings[index].outputSettings.controlSettings
                .deltaAccelSettings.minTravel);
            Add("RSDeltaEasingDuration", (store, index) => store
                .rsOutputSettings[index].outputSettings.controlSettings
                .deltaAccelSettings.easingDuration);
            Add("RSDeltaMinFactor", (store, index) => store
                .rsOutputSettings[index].outputSettings.controlSettings
                .deltaAccelSettings.minfactor);
            Add("RSFlickRWC", (store, index) => store
                .rsOutputSettings[index].outputSettings.flickSettings
                .realWorldCalibration);
            Add("RSFlickThreshold", (store, index) => store
                .rsOutputSettings[index].outputSettings.flickSettings
                .flickThreshold);
            Add("RSFlickTime", (store, index) => store
                .rsOutputSettings[index].outputSettings.flickSettings
                .flickTime);
            Add("RSMinAngleThreshold", (store, index) => store
                .rsOutputSettings[index].outputSettings.flickSettings
                .minAngleThreshold);

            Add("L2DeadZone", (store, index) =>
                store.l2ModInfo[index].deadZone / 255.0);
            Add("R2DeadZone", (store, index) =>
                store.r2ModInfo[index].deadZone / 255.0);
            Add("L2MaxZone", (store, index) =>
                store.l2ModInfo[index].MaxZone / 100.0);
            Add("R2MaxZone", (store, index) =>
                store.r2ModInfo[index].MaxZone / 100.0);
            Add("L2AntiDeadZone", (store, index) =>
                store.l2ModInfo[index].antiDeadZone / 100.0);
            Add("R2AntiDeadZone", (store, index) =>
                store.r2ModInfo[index].antiDeadZone / 100.0);
            Add("L2MaxOutput", (store, index) =>
                store.l2ModInfo[index].MaxOutput / 100.0);
            Add("R2MaxOutput", (store, index) =>
                store.r2ModInfo[index].MaxOutput / 100.0);
            Add("L2Sens", (store, index) => store.l2Sens[index]);
            Add("R2Sens", (store, index) => store.r2Sens[index]);
            Add("L2TriggerEffectStart", (store, index) =>
                store.l2OutputSettings[index].effectSettings.startValue);
            Add("R2TriggerEffectStart", (store, index) =>
                store.r2OutputSettings[index].effectSettings.startValue);
            Add("L2TriggerEffectStrength", (store, index) =>
                store.l2OutputSettings[index].effectSettings.maxValue);
            Add("R2TriggerEffectStrength", (store, index) =>
                store.r2OutputSettings[index].effectSettings.maxValue);

            Add("SXDeadZone", (store, index) => store.SXDeadzone[index]);
            Add("SZDeadZone", (store, index) => store.SZDeadzone[index]);
            Add("SXMaxZone", (store, index) => store.SXMaxzone[index]);
            Add("SZMaxZone", (store, index) => store.SZMaxzone[index]);
            Add("SXAntiDeadZone", (store, index) =>
                store.SXAntiDeadzone[index]);
            Add("SZAntiDeadZone", (store, index) =>
                store.SZAntiDeadzone[index]);
            Add("SXSens", (store, index) => store.SXSens[index]);
            Add("SZSens", (store, index) => store.SZSens[index]);

            Add("TouchSens", (store, index) =>
                store.touchSensitivity[index]);
            Add("TouchTap", (store, index) => store.tapSensitivity[index]);
            Add("TouchScroll", (store, index) =>
                store.scrollSensitivity[index]);
            Add("TouchRelMouseRotation", (store, index) =>
                store.touchpadRelMouse[index].rotation * 180.0 / Math.PI);
            Add("TouchRelMouseMinThreshold", (store, index) =>
                store.touchpadRelMouse[index].minThreshold);
            Add("TouchTrackballFriction", (store, index) =>
                store.trackballFriction[index]);
            Add("TouchMouseStickDeadZone", (store, index) =>
                store.touchMStickInfo[index].deadZone);
            Add("TouchMouseStickMaxZone", (store, index) =>
                store.touchMStickInfo[index].maxZone);
            Add("TouchMouseStickAntiDeadX", (store, index) =>
                store.touchMStickInfo[index].antiDeadX * 100.0);
            Add("TouchMouseStickAntiDeadY", (store, index) =>
                store.touchMStickInfo[index].antiDeadY * 100.0);
            Add("TouchMouseStickVertScale", (store, index) =>
                store.touchMStickInfo[index].vertScale);
            Add("TouchMouseStickTrackballFriction", (store, index) =>
                store.touchMStickInfo[index].trackballFriction);
            Add("TouchMouseStickMaxOutput", (store, index) =>
                store.touchMStickInfo[index].maxOutput);
            Add("TouchMouseStickRotation", (store, index) =>
                store.touchMStickInfo[index].rotationRad * 180.0 / Math.PI);
            Add("TouchMouseStickOneEuroMinCutoff", (store, index) =>
                store.touchMStickInfo[index].MinCutoff);
            Add("TouchMouseStickOneEuroBeta", (store, index) =>
                store.touchMStickInfo[index].Beta);
            Add("TouchAbsMouseMaxZoneX", (store, index) =>
                store.touchpadAbsMouse[index].maxZoneX);
            Add("TouchAbsMouseMaxZoneY", (store, index) =>
                store.touchpadAbsMouse[index].maxZoneY);

            Add("SASteeringWheelFuzz", (store, index) =>
                store.saWheelFuzzValues[index]);
            Add("SASteeringWheelSmoothMinCutoff", (store, index) =>
                store.wheelSmoothInfo[index].MinCutoff);
            Add("SASteeringWheelSmoothBeta", (store, index) =>
                store.wheelSmoothInfo[index].Beta);
            Add("GyroSensitivity", (store, index) =>
                store.gyroSensitivity[index]);
            Add("GyroVertScale", (store, index) =>
                store.gyroSensVerticalScale[index]);
            Add("GyroMouseDeadZone", (store, index) =>
                store.gyroMouseDZ[index]);
            Add("GyroMouseMinThreshold", (store, index) =>
                store.gyroMouseInfo[index].minThreshold);
            Add("GyroMouseOneEuroMinCutoff", (store, index) =>
                store.gyroMouseInfo[index].MinCutoff);
            Add("GyroMouseOneEuroBeta", (store, index) =>
                store.gyroMouseInfo[index].Beta);
            Add("GyroMouseSmoothWeight", (store, index) =>
                store.gyroMouseInfo[index].smoothingWeight);
            Add("GyroMouseStickDeadZone", (store, index) =>
                store.gyroMStickInfo[index].deadZone);
            Add("GyroMouseStickMaxZone", (store, index) =>
                store.gyroMStickInfo[index].maxZone);
            Add("GyroMouseStickAntiDeadX", (store, index) =>
                store.gyroMStickInfo[index].antiDeadX * 100.0);
            Add("GyroMouseStickAntiDeadY", (store, index) =>
                store.gyroMStickInfo[index].antiDeadY * 100.0);
            Add("GyroMouseStickVertScale", (store, index) =>
                store.gyroMStickInfo[index].vertScale);
            Add("GyroMouseStickMaxOutput", (store, index) =>
                store.gyroMStickInfo[index].maxOutput);
            Add("GyroMouseStickOneEuroMinCutoff", (store, index) =>
                store.gyroMStickInfo[index].MinCutoff);
            Add("GyroMouseStickOneEuroBeta", (store, index) =>
                store.gyroMStickInfo[index].Beta);
            Add("GyroMouseStickSmoothWeight", (store, index) =>
                store.gyroMStickInfo[index].smoothWeight);
            Add("GyroSwipeDeadZoneX", (store, index) =>
                store.gyroSwipeInfo[index].deadzoneX);
            Add("GyroSwipeDeadZoneY", (store, index) =>
                store.gyroSwipeInfo[index].deadzoneY);
            Add("GyroSwipeDelayTime", (store, index) =>
                store.gyroSwipeInfo[index].delayTime);

            return entries;
        }
    }
}
