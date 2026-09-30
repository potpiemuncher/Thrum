using System;
using DS4Windows;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace DS4WindowsTests;

/// <summary>
/// A WASAPI mix format is WAVE_FORMAT_EXTENSIBLE, whose Encoding reads as
/// Extensible even when the samples are 32-bit float. Encoding against it
/// directly wrote int32 PCM into a float32 stream (wired USB Audio Haptics:
/// inaudible or full-scale spikes and NaN; wired USB speaker passthrough:
/// silence). Samples are encoded against the standard form of the format.
/// </summary>
[TestClass]
public class AudioSampleFormatTests
{
    [TestMethod]
    public void AnExtensibleFloatMixFormatEncodesAsFloat()
    {
        WaveFormat mix = new WaveFormatExtensible(48000, 32, 4);
        Assert.AreEqual(WaveFormatEncoding.Extensible, mix.Encoding,
            "precondition: the mix format reports Extensible, not IeeeFloat");

        WaveFormat sampleFormat = mix.AsStandardWaveFormat();
        byte[] buffer = new byte[4];
        AudioHapticsService.SlotRuntime.WriteSample(buffer, 0, sampleFormat, 0.5f);

        Assert.AreEqual(WaveFormatEncoding.IeeeFloat, sampleFormat.Encoding);
        Assert.AreEqual(0.5f, BitConverter.ToSingle(buffer, 0), 1e-6f);
    }

    [TestMethod]
    public void NegativeSamplesStayFiniteFloats()
    {
        WaveFormat sampleFormat = new WaveFormatExtensible(48000, 32, 4)
            .AsStandardWaveFormat();
        byte[] buffer = new byte[4];

        AudioHapticsService.SlotRuntime.WriteSample(buffer, 0, sampleFormat, -0.002f);
        float written = BitConverter.ToSingle(buffer, 0);

        Assert.IsTrue(float.IsFinite(written), "wrote " + written);
        Assert.AreEqual(-0.002f, written, 1e-6f);
    }
}
