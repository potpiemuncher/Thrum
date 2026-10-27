using DS4Windows;
using NAudio.Wave;

namespace DS4WindowsTests
{
    [TestClass]
    public class SurroundDownmixTests
    {
        [TestMethod]
        public void CentreChannelReachesBothSpeakerChannels()
        {
            // 5.1: FL FR FC LFE BL BR, one frame with only the centre playing.
            float[] output = Downmix(6, new[] { 0f, 0f, 0.5f, 0f, 0f, 0f });

            Assert.AreEqual(0.5f * 0.70710678f, output[0], 1e-6f);
            Assert.AreEqual(0.5f * 0.70710678f, output[1], 1e-6f);
        }

        [TestMethod]
        public void FrontChannelsKeepFullLevelAndLfeIsLeftOut()
        {
            float[] output = Downmix(8,
                new[] { 0.25f, -0.75f, 0f, 1f, 0f, 0f, 0f, 0f });

            Assert.AreEqual(0.25f, output[0], 1e-6f);
            Assert.AreEqual(-0.75f, output[1], 1e-6f);
        }

        [TestMethod]
        public void SurroundsStayOnTheirSide()
        {
            // Quad: FL FR BL BR, only back-left playing.
            float[] output = Downmix(4, new[] { 0f, 0f, 1f, 0f });

            Assert.AreEqual(0.70710678f, output[0], 1e-6f);
            Assert.AreEqual(0f, output[1], 1e-6f);
        }

        [TestMethod]
        public void OnlyStandardSurroundLayoutsAreFolded()
        {
            Assert.IsTrue(SurroundDownmixSampleProvider.CanDownmix(6));
            Assert.IsFalse(SurroundDownmixSampleProvider.CanDownmix(2));
            Assert.IsFalse(SurroundDownmixSampleProvider.CanDownmix(3));
        }

        private static float[] Downmix(int channels, float[] frame)
        {
            var source = new ArraySampleProvider(frame,
                WaveFormat.CreateIeeeFloatWaveFormat(48000, channels));
            var downmix = new SurroundDownmixSampleProvider(source);
            float[] output = new float[2];
            Assert.AreEqual(2, downmix.Read(output, 0, 2));
            return output;
        }

        private sealed class ArraySampleProvider : ISampleProvider
        {
            private readonly float[] samples;
            private int position;

            public ArraySampleProvider(float[] samples, WaveFormat format)
            {
                this.samples = samples;
                WaveFormat = format;
            }

            public WaveFormat WaveFormat { get; }

            public int Read(float[] buffer, int offset, int count)
            {
                int read = Math.Min(count, samples.Length - position);
                Array.Copy(samples, position, buffer, offset, read);
                position += read;
                return read;
            }
        }
    }
}
