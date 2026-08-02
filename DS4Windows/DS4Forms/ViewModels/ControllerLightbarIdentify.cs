/*
DS4Windows
Copyright (C) 2023  Travis Nickles

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.
*/

using System;
using DS4Windows;

namespace DS4WinWPF.DS4Forms.ViewModels
{
    /// <summary>
    /// Applies an identify flash through the existing forced-lightbar path.
    /// Profile lightbar settings are never mutated, and a newer forced effect
    /// wins instead of being overwritten when the identify lease expires.
    /// </summary>
    internal sealed class ControllerLightbarIdentify
    {
        private static readonly DS4Color IdentifyColor =
            new(255, 255, 255);
        private const byte IdentifyFlash = 20;

        private readonly int deviceIndex;
        private readonly bool previousForceLight;
        private readonly DS4Color previousColor;
        private readonly byte previousFlash;
        private bool restored;

        private ControllerLightbarIdentify(int deviceIndex,
            bool previousForceLight, DS4Color previousColor,
            byte previousFlash)
        {
            this.deviceIndex = deviceIndex;
            this.previousForceLight = previousForceLight;
            this.previousColor = previousColor;
            this.previousFlash = previousFlash;
        }

        internal static TimeSpan Duration { get; } =
            TimeSpan.FromMilliseconds(1200);

        internal static ControllerLightbarIdentify Begin(int deviceIndex)
        {
            // The forced-light arrays cover physical controller slots only;
            // fail before an offline-profile index can touch them.
            if (deviceIndex < 0 ||
                deviceIndex >= DS4LightBar.forcelight.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(deviceIndex));
            }

            lock (DS4LightBar.forcedColor)
            lock (DS4LightBar.forcelight)
            lock (DS4LightBar.forcedFlash)
            {
                var lease = new ControllerLightbarIdentify(deviceIndex,
                    DS4LightBar.forcelight[deviceIndex],
                    DS4LightBar.forcedColor[deviceIndex],
                    DS4LightBar.forcedFlash[deviceIndex]);

                DS4LightBar.forcedColor[deviceIndex] = IdentifyColor;
                DS4LightBar.forcedFlash[deviceIndex] = IdentifyFlash;
                DS4LightBar.forcelight[deviceIndex] = true;
                return lease;
            }
        }

        internal void Restore()
        {
            lock (DS4LightBar.forcedColor)
            lock (DS4LightBar.forcelight)
            lock (DS4LightBar.forcedFlash)
            {
                // Restore can run from more than one cleanup path; only the
                // first call is allowed to hand ownership back.
                if (restored)
                {
                    return;
                }

                restored = true;

                // A macro or preview that replaced our exact state owns the
                // lightbar now. Restoring here would clobber that newer effect.
                if (!DS4LightBar.forcelight[deviceIndex] ||
                    !DS4LightBar.forcedColor[deviceIndex]
                        .Equals(IdentifyColor) ||
                    DS4LightBar.forcedFlash[deviceIndex] != IdentifyFlash)
                {
                    return;
                }

                if (!previousForceLight)
                {
                    DS4LightBar.forcelight[deviceIndex] = false;
                }

                DS4LightBar.forcedColor[deviceIndex] = previousColor;
                DS4LightBar.forcedFlash[deviceIndex] = previousFlash;
                DS4LightBar.forcelight[deviceIndex] = previousForceLight;
            }
        }
    }
}
