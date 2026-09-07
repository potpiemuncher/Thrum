using DS4Windows;

namespace DS4WindowsTests
{
    [TestClass]
    public class AudioInputLevelMeterTests
    {
        [TestMethod]
        public void BlockPeakIsPublishedWithoutHotPathAllocation()
        {
            AudioInputLevelMeter meter = new AudioInputLevelMeter();
            float[] block = { -0.25f, 0.1f, 0.75f, -0.4f };
            meter.PublishBlock(block);
            Assert.AreEqual(0.75f, meter.Level, 0.0001f);

            for (int warmup = 0; warmup < 100; warmup++)
            {
                meter.PublishBlock(block);
            }
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int iteration = 0; iteration < 1000; iteration++)
            {
                meter.PublishBlock(block);
            }
            long after = GC.GetAllocatedBytesForCurrentThread();

            Assert.AreEqual(before, after,
                "Publishing block peaks must remain allocation-free.");
        }
    }
}
