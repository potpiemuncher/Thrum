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
using NAudio.Wave;

namespace DS4Windows
{
    /// <summary>
    /// Folds a quad, 5.1 or 7.1 loopback stream down to stereo for a
    /// controller speaker.
    ///
    /// <para>The speaker lanes used to keep only front left and right, so on
    /// a surround playback device (many gaming headsets present a virtual 7.1
    /// endpoint) the centre channel, where games and films put dialogue, never
    /// reached the controller. Front channels keep full level, so stereo
    /// content in a surround stream is exactly as loud as before; centre and
    /// surrounds are added at -3 dB (-6 dB for the four 7.1 surrounds). LFE is
    /// left out: a controller speaker cannot reproduce it. Peaks above full
    /// scale are clamped where the lanes convert to 16-bit PCM.</para>
    ///
    /// <para>Channel order is the Windows default for each count (FL FR BL BR;
    /// FL FR FC LFE BL BR; FL FR FC LFE BL BR SL SR).</para>
    /// </summary>
    internal sealed class SurroundDownmixSampleProvider : ISampleProvider
    {
        private const float MinusThreeDb = 0.70710678f;
        private const float MinusSixDb = 0.5f;

        private readonly ISampleProvider source;
        private readonly int inputChannels;
        private readonly float[] leftGains;
        private readonly float[] rightGains;
        private float[] sourceBuffer = Array.Empty<float>();

        public SurroundDownmixSampleProvider(ISampleProvider source)
        {
            this.source = source ?? throw new ArgumentNullException(nameof(source));
            inputChannels = source.WaveFormat.Channels;
            if (!CanDownmix(inputChannels))
            {
                throw new ArgumentException(
                    $"No downmix for {inputChannels} channels.", nameof(source));
            }

            (leftGains, rightGains) = BuildGains(inputChannels);
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(
                source.WaveFormat.SampleRate, 2);
        }

        public WaveFormat WaveFormat { get; }

        public static bool CanDownmix(int channels) =>
            channels == 4 || channels == 6 || channels == 8;

        internal static (float[] Left, float[] Right) BuildGains(int channels)
        {
            float[] left = new float[channels];
            float[] right = new float[channels];
            left[0] = 1.0f;
            right[1] = 1.0f;
            switch (channels)
            {
                case 4:
                    left[2] = MinusThreeDb;
                    right[3] = MinusThreeDb;
                    break;
                case 6:
                    left[2] = MinusThreeDb;
                    right[2] = MinusThreeDb;
                    left[4] = MinusThreeDb;
                    right[5] = MinusThreeDb;
                    break;
                case 8:
                    left[2] = MinusThreeDb;
                    right[2] = MinusThreeDb;
                    left[4] = MinusSixDb;
                    right[5] = MinusSixDb;
                    left[6] = MinusSixDb;
                    right[7] = MinusSixDb;
                    break;
            }

            return (left, right);
        }

        public int Read(float[] buffer, int offset, int count)
        {
            int frames = count / 2;
            int needed = frames * inputChannels;
            if (sourceBuffer.Length < needed)
            {
                sourceBuffer = new float[needed];
            }

            int read = source.Read(sourceBuffer, 0, needed);
            int framesRead = read / inputChannels;
            int input = 0;
            int output = offset;
            for (int frame = 0; frame < framesRead; frame++)
            {
                float left = 0.0f;
                float right = 0.0f;
                for (int channel = 0; channel < inputChannels; channel++)
                {
                    float sample = sourceBuffer[input + channel];
                    left += sample * leftGains[channel];
                    right += sample * rightGains[channel];
                }

                buffer[output++] = left;
                buffer[output++] = right;
                input += inputChannels;
            }

            return framesRead * 2;
        }
    }
}
