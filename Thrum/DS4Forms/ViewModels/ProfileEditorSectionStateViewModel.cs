using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
using System.Xml.Serialization;
using DS4Windows;
using DS4WinWPF.DS4Control.DTOXml;

namespace DS4WinWPF.DS4Forms.ViewModels
{
    public sealed class ProfileEditorSectionSnapshot
    {
        private static readonly XmlSerializer ProfileSerializer =
            new XmlSerializer(typeof(ProfileDTO),
                ProfileDTO.GetAttributeOverrides());

        private static readonly string[] AxisElements =
        {
            "LeftStickDriftXAxis", "LeftStickDriftYAxis",
            "RightStickDriftXAxis", "RightStickDriftYAxis",
            "LeftTriggerMiddle", "RightTriggerMiddle", "L2AntiDeadZone",
            "R2AntiDeadZone", "L2MaxZone", "R2MaxZone", "L2MaxOutput",
            "R2MaxOutput", "LSDeadZone", "RSDeadZone", "LSAntiDeadZone",
            "RSAntiDeadZone", "LSMaxZone", "RSMaxZone", "LSVerticalScale",
            "RSVerticalScale", "LSMaxOutput", "RSMaxOutput",
            "LSMaxOutputForce", "RSMaxOutputForce", "LSDeadZoneType",
            "RSDeadZoneType", "LSAxialDeadOptions", "RSAxialDeadOptions",
            "LSRotation", "RSRotation", "LSFuzz", "RSFuzz",
            "LSOuterBindDead", "RSOuterBindDead", "LSOuterBindInvert",
            "RSOuterBindInvert", "LSDeltaAccelSettings",
            "RSDeltaAccelSettings", "SXDeadZone", "SZDeadZone", "SXMaxZone",
            "SZMaxZone", "SXAntiDeadZone", "SZAntiDeadZone", "Sensitivity",
            "LSOutputCurveMode", "LSOutputCurveCustom", "RSOutputCurveMode",
            "RSOutputCurveCustom", "LSSquareStick", "RSSquareStick",
            "SquareStickRoundness", "SquareRStickRoundness",
            "LSAntiSnapback", "RSAntiSnapback", "LSAntiSnapbackDelta",
            "RSAntiSnapbackDelta", "LSAntiSnapbackTimeout",
            "RSAntiSnapbackTimeout", "LSOutputMode", "RSOutputMode",
            "LSOutputSettings", "RSOutputSettings", "L2OutputCurveMode",
            "L2OutputCurveCustom", "L2TwoStageMode", "R2TwoStageMode",
            "L2HipFireTime", "R2HipFireTime", "L2TriggerEffect",
            "L2TriggerEffectStart", "L2TriggerEffectStrength",
            "R2TriggerEffect", "R2TriggerEffectStart",
            "R2TriggerEffectStrength", "R2OutputCurveMode",
            "R2OutputCurveCustom", "SXOutputCurveMode", "SXOutputCurveCustom",
            "SZOutputCurveMode", "SZOutputCurveCustom",
        };

        private static readonly string[] GyroElements =
        {
            "SATriggers", "SATriggerCond", "SASteeringWheelEmulationAxis",
            "SASteeringWheelEmulationRange", "SASteeringWheelFuzz",
            "SASteeringWheelSmoothingOptions", "TouchDisInvTriggers",
            "GyroSensitivity", "GyroSensVerticalScale", "GyroInvert",
            "GyroTriggerTurns", "GyroControlsSettings",
            "GyroMouseSmoothingSettings", "GyroMouseHAxis",
            "GyroMouseDeadZone", "GyroMouseMinThreshold", "GyroMouseToggle",
            "GyroMouseJitterCompensation", "GyroOutputMode",
            "GyroMouseStickTriggers", "GyroMouseStickTriggerCond",
            "GyroMouseStickTriggerTurns", "GyroMouseStickHAxis",
            "GyroMouseStickDeadZone", "GyroMouseStickMaxZone",
            "GyroMouseStickOutputStick", "GyroMouseStickOutputStickAxes",
            "GyroMouseStickAntiDeadX", "GyroMouseStickAntiDeadY",
            "GyroMouseStickInvert", "GyroMouseStickToggle",
            "GyroMouseStickMaxOutput", "GyroMouseStickMaxOutputEnabled",
            "GyroMouseStickVerticalScale",
            "GyroMouseStickJitterCompensation",
            "GyroMouseStickSmoothingSettings", "GyroSwipeSettings",
            "UseDs3PitchRollSim",
        };

        private static readonly string[] TouchpadElements =
        {
            "touchSensitivity", "touchpadJitterCompensation", "lowerRCOn",
            "tapSensitivity", "doubleTap", "scrollSensitivity",
            "TouchpadInvert", "TouchpadClickPassthru", "StartTouchpadOff",
            "UseTPforControls", "TouchpadOutputMode", "TrackballMode",
            "TrackballFriction", "TouchRelMouseRotation",
            "TouchRelMouseMinThreshold", "TouchpadAbsMouseSettings",
            "TouchpadMouseStick", "TouchpadButtonMode",
        };

        private static readonly string[] AxisMappingControls =
        {
            "LSOuter", "RSOuter",
        };

        private static readonly string[] GyroMappingControls =
        {
            "GyroXPos", "GyroXNeg", "GyroZPos", "GyroZNeg",
            "GyroSwipeLeft", "GyroSwipeRight", "GyroSwipeUp",
            "GyroSwipeDown",
        };

        private static readonly string[] TouchpadMappingControls =
        {
            "TouchLeft", "TouchUpper", "TouchMulti", "TouchRight",
            "SwipeLeft", "SwipeRight", "SwipeUp", "SwipeDown",
        };

        public ProfileEditorSectionSnapshot(string axisSignature,
            string gyroSignature, string touchpadSignature)
        {
            AxisSignature = axisSignature ?? string.Empty;
            GyroSignature = gyroSignature ?? string.Empty;
            TouchpadSignature = touchpadSignature ?? string.Empty;
        }

        public string AxisSignature { get; }
        public string GyroSignature { get; }
        public string TouchpadSignature { get; }

        public static ProfileEditorSectionSnapshot Capture(BackingStore store,
            int deviceIndex)
        {
            if (store == null)
            {
                throw new ArgumentNullException(nameof(store));
            }

            ProfileDTO profile = new ProfileDTO { DeviceIndex = deviceIndex };
            profile.MapFrom(store);

            XDocument document;
            using (StringWriter writer = new StringWriter())
            {
                ProfileSerializer.Serialize(writer, profile);
                document = XDocument.Parse(writer.ToString(),
                    LoadOptions.PreserveWhitespace);
            }

            return new ProfileEditorSectionSnapshot(
                BuildSignature(document.Root, AxisElements) +
                    BuildMappingSignature(document.Root, AxisMappingControls),
                BuildSignature(document.Root, GyroElements) +
                    BuildMappingSignature(document.Root, GyroMappingControls),
                BuildSignature(document.Root, TouchpadElements) +
                    BuildMappingSignature(document.Root,
                        TouchpadMappingControls));
        }

        private static string BuildSignature(XElement root,
            IEnumerable<string> elementNames)
        {
            if (root == null)
            {
                return string.Empty;
            }

            return string.Concat(elementNames.Select(name =>
            {
                XElement element = root.Element(name);
                return element == null
                    ? $"<{name}:missing>"
                    : element.ToString(SaveOptions.DisableFormatting);
            }));
        }

        private static string BuildMappingSignature(XElement root,
            IEnumerable<string> controlNames)
        {
            if (root == null)
            {
                return string.Empty;
            }

            HashSet<string> names = new HashSet<string>(controlNames,
                StringComparer.Ordinal);
            return string.Concat(root.Elements()
                .Where(element => element.Name.LocalName == "Control" ||
                    element.Name.LocalName == "ShiftControl")
                .SelectMany(element => element.Descendants())
                .Where(element => names.Contains(element.Name.LocalName))
                .Select(element => string.Join("/", element.AncestorsAndSelf()
                        .Reverse().Select(part => part.Name.LocalName)) + "=" +
                    element.ToString(SaveOptions.DisableFormatting))
                .OrderBy(value => value, StringComparer.Ordinal));
        }
    }

    public sealed class ProfileEditorSectionStateViewModel :
        INotifyPropertyChanged
    {
        private bool isAxisConfigExpanded;
        private bool isGyroExpanded;
        private bool isTouchpadExpanded;

        public ProfileEditorSectionStateViewModel(
            ProfileEditorSectionSnapshot current,
            ProfileEditorSectionSnapshot defaults)
        {
            Update(current, defaults);
        }

        public bool IsAxisConfigExpanded
        {
            get => isAxisConfigExpanded;
            set => SetField(ref isAxisConfigExpanded, value);
        }

        public bool IsGyroExpanded
        {
            get => isGyroExpanded;
            set => SetField(ref isGyroExpanded, value);
        }

        public bool IsTouchpadExpanded
        {
            get => isTouchpadExpanded;
            set => SetField(ref isTouchpadExpanded, value);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public static ProfileEditorSectionStateViewModel Create(
            BackingStore currentStore, int deviceIndex)
        {
            BackingStore defaultStore =
                ProfileEditorDefaultProvider.CreateDefaultStore();
            return new ProfileEditorSectionStateViewModel(
                ProfileEditorSectionSnapshot.Capture(currentStore, deviceIndex),
                ProfileEditorSectionSnapshot.Capture(defaultStore,
                    ProfileEditorDefaultProvider.DefaultDeviceIndex));
        }

        public void Update(ProfileEditorSectionSnapshot current,
            ProfileEditorSectionSnapshot defaults)
        {
            if (current == null)
            {
                throw new ArgumentNullException(nameof(current));
            }

            if (defaults == null)
            {
                throw new ArgumentNullException(nameof(defaults));
            }

            IsAxisConfigExpanded = !string.Equals(current.AxisSignature,
                defaults.AxisSignature, StringComparison.Ordinal);
            IsGyroExpanded = !string.Equals(current.GyroSignature,
                defaults.GyroSignature, StringComparison.Ordinal);
            IsTouchpadExpanded = !string.Equals(current.TouchpadSignature,
                defaults.TouchpadSignature, StringComparison.Ordinal);
        }

        public void Update(BackingStore currentStore, int deviceIndex)
        {
            BackingStore defaultStore =
                ProfileEditorDefaultProvider.CreateDefaultStore();
            Update(ProfileEditorSectionSnapshot.Capture(currentStore, deviceIndex),
                ProfileEditorSectionSnapshot.Capture(defaultStore,
                    ProfileEditorDefaultProvider.DefaultDeviceIndex));
        }

        private void SetField(ref bool field, bool value,
            [CallerMemberName] string propertyName = null)
        {
            if (field == value)
            {
                return;
            }

            field = value;
            PropertyChanged?.Invoke(this,
                new PropertyChangedEventArgs(propertyName));
        }
    }
}
