/*
DS4Windows
Copyright (C) 2026  DS4Windows contributors

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.
*/

using System;
using System.Threading;

namespace DS4Windows
{
    /// <summary>
    /// Single-writer block peak shared with a polling UI. Publishing is one
    /// volatile integer write and never allocates, locks, or raises an event.
    /// </summary>
    internal sealed class AudioInputLevelMeter
    {
        private int levelBits;

        public float Level => BitConverter.Int32BitsToSingle(
            Volatile.Read(ref levelBits));

        public void PublishBlockPeak(float peak)
        {
            float normalized = float.IsFinite(peak)
                ? Math.Clamp(peak, 0.0f, 1.0f) : 0.0f;
            Volatile.Write(ref levelBits,
                BitConverter.SingleToInt32Bits(normalized));
        }

        public void PublishBlock(ReadOnlySpan<float> samples)
        {
            float peak = 0.0f;
            for (int index = 0; index < samples.Length; index++)
            {
                peak = Math.Max(peak, Math.Abs(samples[index]));
            }
            PublishBlockPeak(peak);
        }

        public void Reset() => Volatile.Write(ref levelBits, 0);
    }
}
