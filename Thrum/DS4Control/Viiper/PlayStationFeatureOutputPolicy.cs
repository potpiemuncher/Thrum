using DS4Windows.InputDevices;

namespace DS4Windows
{
    /// <summary>
    /// Keeps PlayStation hardware features independent from the controller
    /// persona presented to games. Xbox and Switch profiles use a USB audio
    /// only VIIPER companion; PlayStation profiles reuse their primary output.
    /// </summary>
    internal static class PlayStationFeatureOutputPolicy
    {
        internal static bool IsPlayStationAudioOutput(OutContType outputType)
        {
            outputType = outputType.Normalize();
            return outputType == OutContType.ViiperDS4 ||
                outputType == OutContType.ViiperDualSense ||
                outputType == OutContType.ViiperDualSenseEdge;
        }

        internal static bool NeedsAudioOnlySidecar(OutContType outputType)
        {
            outputType = outputType.Normalize();
            return outputType == OutContType.ViiperX360 ||
                outputType == OutContType.ViiperSwitch2Pro;
        }

        internal static OutContType GetAudioOnlySidecarType(
            DS4Device source, OutContType primaryOutputType,
            bool dInputOnly, bool audioClassAllowed)
        {
            if (source?.HidDevice?.Attributes == null)
            {
                return OutContType.None;
            }

            return GetAudioOnlySidecarType(source.DeviceType,
                source.ConnectionType,
                source.HidDevice.Attributes.VendorId,
                source.HidDevice.Attributes.ProductId,
                primaryOutputType, dInputOnly, audioClassAllowed);
        }

        /// <param name="audioClassAllowed">
        /// The audio-class gate's answer. This sidecar exists only to carry
        /// virtual USB audio endpoints, so it is exactly the device the gate
        /// governs: <c>false</c> means no sidecar, full stop.
        ///
        /// <para>Historically this method answered from the hardware matrix
        /// alone, which meant a Sony pad on Bluetooth with an Xbox or Switch
        /// profile output grew a second virtual device with audio interfaces
        /// with nobody asking for it. That implicit creation is what plan task
        /// 2.3 removes: the parameter has no default, so every call site has to
        /// say what it consulted.</para>
        /// </param>
        internal static OutContType GetAudioOnlySidecarType(
            InputDeviceType deviceType, ConnectionType connectionType,
            int vendorId, int productId, OutContType primaryOutputType,
            bool dInputOnly, bool audioClassAllowed)
        {
            if (!audioClassAllowed || dInputOnly ||
                connectionType != ConnectionType.BT ||
                vendorId != DS4Devices.SONY_VID ||
                !NeedsAudioOnlySidecar(primaryOutputType))
            {
                return OutContType.None;
            }

            return deviceType switch
            {
                InputDeviceType.DS4 when productId == 0x05C4 ||
                    productId == 0x09CC => OutContType.ViiperDS4,
                InputDeviceType.DualSense when productId == 0x0CE6 ||
                    productId == 0x0DF2 => OutContType.ViiperDualSense,
                _ => OutContType.None,
            };
        }

        internal static ViiperVirtualDeviceType GetViiperType(
            OutContType outputType)
        {
            return outputType.Normalize() switch
            {
                OutContType.ViiperDS4 => ViiperVirtualDeviceType.DualShock4,
                OutContType.ViiperDualSense => ViiperVirtualDeviceType.DualSense,
                _ => throw new System.ArgumentOutOfRangeException(
                    nameof(outputType), outputType,
                    "Audio-only sidecars are available only for PlayStation outputs."),
            };
        }
    }
}
