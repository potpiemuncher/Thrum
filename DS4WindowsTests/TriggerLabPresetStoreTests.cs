using System.Text.Json;
using DS4Windows;

namespace DS4WindowsTests
{
    [TestClass]
    public class TriggerLabPresetStoreTests
    {
        [TestMethod]
        public void UserPresetStoreRoundTripsIndependentlyOfProfiles()
        {
            using TemporaryPresetDirectory temporary = new();
            TriggerLabPresetStore store = temporary.CreateStore();
            TriggerLabUserPreset added = store.Add("Heavy brake",
                new TriggerLabEffectSettings
                {
                    Mode = TriggerLabMode.Feedback,
                    StartPercent = 15,
                    WallPercent = 55,
                    ForcePercent = 90,
                });

            TriggerLabPresetStore restored = temporary.CreateStore();
            TriggerLabPresetLoadResult load = restored.Load();

            Assert.IsTrue(load.Success, load.Message);
            Assert.AreEqual(1, restored.Presets.Count);
            Assert.AreEqual(added.Id, restored.Presets[0].Id);
            Assert.AreEqual("Heavy brake", restored.Presets[0].Name);
            Assert.AreEqual(TriggerLabMode.Feedback,
                restored.Presets[0].Mode);
            Assert.AreEqual(90, restored.Presets[0].ForcePercent);
        }

        [TestMethod]
        public void FutureSchemaVersionIsRejectedInformatively()
        {
            using TemporaryPresetDirectory temporary = new();
            File.WriteAllText(temporary.StorePath,
                "{\"schemaVersion\":2,\"presets\":[]}");

            TriggerLabPresetStore store = temporary.CreateStore();
            TriggerLabPresetLoadResult result = store.Load();

            Assert.IsFalse(result.Success);
            StringAssert.Contains(result.Message, "newer than supported");
            Assert.IsTrue(File.Exists(temporary.StorePath),
                "A future document must be left in place for the newer app that owns it.");
            Assert.ThrowsException<TriggerLabPresetFormatException>(() =>
                store.Add("Do not overwrite",
                    new TriggerLabEffectSettings()));
        }

        [TestMethod]
        public void CorruptStoreIsQuarantinedAndFreshStoreRemainsUsable()
        {
            using TemporaryPresetDirectory temporary = new();
            File.WriteAllText(temporary.StorePath,
                "{\"schemaVersion\":1,\"presets\":[");
            TriggerLabPresetStore store = temporary.CreateStore();

            TriggerLabPresetLoadResult result = store.Load();

            Assert.IsFalse(result.Success);
            StringAssert.Contains(result.Message, "invalid");
            Assert.IsFalse(File.Exists(temporary.StorePath));
            Assert.IsTrue(File.Exists(result.QuarantinePath));

            store.Add("Recovery", new TriggerLabEffectSettings());
            TriggerLabPresetStore restored = temporary.CreateStore();
            Assert.IsTrue(restored.Load().Success);
            Assert.AreEqual("Recovery", restored.Presets.Single().Name);
        }

        [TestMethod]
        public void ExportContainsOnlyVersionNamesAndEffectParameters()
        {
            using TemporaryPresetDirectory temporary = new();
            TriggerLabPresetStore store = temporary.CreateStore();
            store.Add("Impact", new TriggerLabEffectSettings
            {
                Mode = TriggerLabMode.Vibration,
                StartPercent = 10,
                WallPercent = 40,
                ForcePercent = 70,
            });
            string exportPath = Path.Combine(temporary.DirectoryPath,
                "export.json");

            store.Export(exportPath);
            string json = File.ReadAllText(exportPath);
            using JsonDocument document = JsonDocument.Parse(json);

            CollectionAssert.AreEquivalent(
                new[] { "schemaVersion", "presets" },
                document.RootElement.EnumerateObject()
                    .Select(property => property.Name).ToArray());
            JsonElement preset = document.RootElement.GetProperty("presets")[0];
            CollectionAssert.AreEquivalent(new[]
            {
                "name", "mode", "startPercent", "wallPercent",
                "forcePercent",
            }, preset.EnumerateObject().Select(property => property.Name)
                .ToArray());
            Assert.IsFalse(json.Contains(temporary.DirectoryPath,
                StringComparison.OrdinalIgnoreCase));
            Assert.IsFalse(json.Contains(Environment.MachineName,
                StringComparison.OrdinalIgnoreCase));
            Assert.IsFalse(json.Contains(Environment.UserName,
                StringComparison.OrdinalIgnoreCase));
        }

        [TestMethod]
        public void ExportedPresetsImportWithFreshLibraryIds()
        {
            using TemporaryPresetDirectory sourceDirectory = new();
            TriggerLabPresetStore source = sourceDirectory.CreateStore();
            TriggerLabUserPreset original = source.Add("Road feel",
                new TriggerLabEffectSettings
                {
                    Mode = TriggerLabMode.Vibration,
                    StartPercent = 5,
                    WallPercent = 35,
                    ForcePercent = 45,
                });
            string exportPath = Path.Combine(sourceDirectory.DirectoryPath,
                "road-feel.json");
            source.Export(exportPath, original.Id);

            using TemporaryPresetDirectory targetDirectory = new();
            TriggerLabPresetStore target = targetDirectory.CreateStore();
            int imported = target.Import(exportPath);

            Assert.AreEqual(1, imported);
            TriggerLabUserPreset restored = target.Presets.Single();
            Assert.AreEqual("Road feel", restored.Name);
            Assert.AreNotEqual(original.Id, restored.Id,
                "Export documents carry no stable per-machine or library id.");
            Assert.AreEqual(TriggerLabMode.Vibration, restored.Mode);
            Assert.AreEqual(45, restored.ForcePercent);
        }

        [TestMethod]
        public void BuiltInAndUserPresetsHaveDistinctOriginsAndPermissions()
        {
            using TemporaryPresetDirectory temporary = new();
            TriggerLabUserPreset user = temporary.CreateStore().Add("Mine",
                new TriggerLabEffectSettings());

            Assert.IsTrue(TriggerLabPresetCatalog.Presets.All(preset =>
                preset.Origin == TriggerLabPresetOrigin.BuiltIn &&
                !preset.CanDelete));
            Assert.AreEqual(TriggerLabPresetOrigin.User, user.Origin);
            Assert.IsTrue(user.CanDelete);
        }

        private sealed class TemporaryPresetDirectory : IDisposable
        {
            public TemporaryPresetDirectory()
            {
                DirectoryPath = Path.Combine(Path.GetTempPath(),
                    "ThrumPresetTests", Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(DirectoryPath);
                StorePath = Path.Combine(DirectoryPath,
                    TriggerLabPresetStore.DefaultFileName);
            }

            public string DirectoryPath { get; }
            public string StorePath { get; }
            public TriggerLabPresetStore CreateStore() => new(StorePath);

            public void Dispose()
            {
                if (Directory.Exists(DirectoryPath))
                {
                    Directory.Delete(DirectoryPath, true);
                }
            }
        }
    }
}
