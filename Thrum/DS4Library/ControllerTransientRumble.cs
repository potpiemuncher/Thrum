/*
DS4Windows
Copyright (C) 2023  Travis Nickles

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.
*/

namespace DS4Windows
{
    internal static class ControllerTransientRumblePolicy
    {
        internal static DS4ForceFeedbackState PrepareRestoreState(
            DS4ForceFeedbackState previous)
        {
            if (!previous.IsRumbleSet())
            {
                // Restoring logical zero still needs one explicit output
                // report to stop the pulse. MergeStates clears this marker.
                previous.RumbleMotorsExplicitlyOff = true;
            }

            return previous;
        }
    }

    internal readonly struct ControllerTransientRumbleLeaseState
    {
        internal ControllerTransientRumbleLeaseState(
            DS4ForceFeedbackState previousState, long revision)
        {
            PreviousState = previousState;
            Revision = revision;
        }

        internal DS4ForceFeedbackState PreviousState { get; }
        internal long Revision { get; }
    }

    /// <summary>
    /// Narrow seam used by bounded UI feedback. Implementations compose with
    /// the ordinary output state and reject restoration after newer feedback.
    /// </summary>
    internal interface IControllerTransientRumbleTarget
    {
        ControllerTransientRumbleLeaseState BeginTransientRumble(
            byte rightLightFastMotor, byte leftHeavySlowMotor);

        bool RestoreTransientRumble(
            ControllerTransientRumbleLeaseState lease);
    }
}
