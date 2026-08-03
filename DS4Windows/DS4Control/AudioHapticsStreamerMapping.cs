/*
Thrum
Copyright (C) 2026  Thrum contributors

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

namespace DS4Windows;

/// <summary>
/// Pure mapping from <see cref="AudioHapticsProfileSettings"/> to the
/// <c>DualSenseControllerOptions.BTHaptics*</c> option values that drive
/// <see cref="InputDevices.DualSenseHapticsStreamer"/>.
/// </summary>
public static class AudioHapticsStreamerMapping
{
    /// <summary>
    /// Configuration values produced by mapping Thrum's Audio Haptics
    /// settings onto the Bluetooth haptics streamer options.
    /// </summary>
    public readonly struct BTHapticsOptions
    {
        public BTHapticsOptions(DualSenseControllerOptions.HapticsMode mode,
            double gain, int lowPassHz, bool hfTexture, string endpointId)
        {
            Mode = mode;
            Gain = gain;
            LowPassHz = lowPassHz;
            HFTexture = hfTexture;
            EndpointId = endpointId;
        }

        public DualSenseControllerOptions.HapticsMode Mode { get; }
        public double Gain { get; }
        public int LowPassHz { get; }
        public bool HFTexture { get; }
        public string EndpointId { get; }
    }

    /// <summary>
    /// Maps Thrum's Audio Haptics settings to Bluetooth haptics streamer
    /// options using the exact rules documented in BRIEF-STEP2.
    /// </summary>
    /// <param name="settings">The profile settings to map.</param>
    /// <returns>Streamer option values ready to assign to
    /// <c>DualSenseControllerOptions.BTHaptics*</c>.</returns>
    public static BTHapticsOptions Map(AudioHapticsProfileSettings settings)
    {
        if (settings == null || !settings.Enabled)
        {
            return new BTHapticsOptions(
                DualSenseControllerOptions.HapticsMode.Off,
                3.0, 350, false, string.Empty);
        }

        DualSenseControllerOptions.HapticsMode mode;
        switch (settings.Source)
        {
            case AudioHapticsSourceKind.SystemAudio:
            case AudioHapticsSourceKind.Endpoint:
                mode = DualSenseControllerOptions.HapticsMode.SystemAudio;
                break;
            case AudioHapticsSourceKind.AppSession:
            case AudioHapticsSourceKind.ControllerAudio:
            default:
                mode = DualSenseControllerOptions.HapticsMode.Off;
                break;
        }

        // GainPercent 0..200 → gain 0.0..6.0, clamped to streamer's 0.1..10.0.
        double gain = Math.Clamp(settings.GainPercent / 100.0 * 3.0, 0.1, 10.0);

        // BassFocus → low-pass cutoff Hz.
        int lowPassHz = settings.BassFocus switch
        {
            AudioHapticsBassFocus.Deep => 150,
            AudioHapticsBassFocus.Balanced => 350,
            AudioHapticsBassFocus.Punchy => 250,
            AudioHapticsBassFocus.Wide => 600,
            _ => 350,
        };

        return new BTHapticsOptions(mode, gain, lowPassHz, false,
            settings.EndpointId);
    }
}
