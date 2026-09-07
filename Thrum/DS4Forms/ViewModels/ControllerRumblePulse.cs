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
    internal sealed class ControllerRumblePulse
    {
        private const byte RightLightFastMotor = 70;
        private const byte LeftHeavySlowMotor = 110;
        private readonly IControllerTransientRumbleTarget target;
        private readonly ControllerTransientRumbleLeaseState lease;
        private bool restored;

        private ControllerRumblePulse(
            IControllerTransientRumbleTarget target,
            ControllerTransientRumbleLeaseState lease)
        {
            this.target = target;
            this.lease = lease;
        }

        internal static TimeSpan Duration { get; } =
            TimeSpan.FromMilliseconds(450);

        internal static ControllerRumblePulse Begin(
            IControllerTransientRumbleTarget target)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            ControllerTransientRumbleLeaseState lease =
                target.BeginTransientRumble(RightLightFastMotor,
                    LeftHeavySlowMotor);
            return new ControllerRumblePulse(target, lease);
        }

        internal bool Restore()
        {
            if (restored) return false;
            restored = true;
            return target.RestoreTransientRumble(lease);
        }
    }
}
