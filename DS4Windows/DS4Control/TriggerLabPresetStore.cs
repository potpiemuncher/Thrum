/*
DS4Windows
Copyright (C) 2026  DS4Windows contributors

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DS4Windows
{
    public sealed class TriggerLabUserPreset
    {
        internal TriggerLabUserPreset(string id, string name,
            TriggerLabMode mode, int startPercent, int wallPercent,
            int forcePercent)
        {
            Id = id;
            Name = name;
            Mode = mode;
            StartPercent = startPercent;
            WallPercent = wallPercent;
            ForcePercent = forcePercent;
        }

        public string Id { get; }
        public string Name { get; }
        public TriggerLabMode Mode { get; }
        public int StartPercent { get; }
        public int WallPercent { get; }
        public int ForcePercent { get; }
        public TriggerLabPresetOrigin Origin => TriggerLabPresetOrigin.User;
        public bool CanDelete => true;

        public TriggerLabEffectSettings CreateEffect() =>
            new TriggerLabEffectSettings
            {
                ProfileId = Id,
                Mode = Mode,
                StartPercent = StartPercent,
                WallPercent = WallPercent,
                ForcePercent = ForcePercent,
            }.Normalize();
    }

    public sealed class TriggerLabPresetLoadResult
    {
        internal TriggerLabPresetLoadResult(bool success, string message,
            string quarantinePath = null)
        {
            Success = success;
            Message = message ?? string.Empty;
            QuarantinePath = quarantinePath ?? string.Empty;
        }

        public bool Success { get; }
        public string Message { get; }
        public string QuarantinePath { get; }
    }

    public sealed class TriggerLabPresetFormatException : Exception
    {
        public TriggerLabPresetFormatException(string message) : base(message)
        {
        }
    }

    /// <summary>
    /// Owns the appdata-scoped Trigger Lab library. Profile-embedded custom
    /// effects are deliberately not read or rewritten here.
    /// </summary>
    public sealed class TriggerLabPresetStore
    {
        public const int CurrentSchemaVersion = 1;
        public const string DefaultFileName = "TriggerLabPresets.json";

        private static readonly JsonSerializerOptions jsonOptions =
            new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true,
                Converters = { new JsonStringEnumConverter() },
            };

        private readonly string filePath;
        private IReadOnlyList<TriggerLabUserPreset> presets =
            Array.Empty<TriggerLabUserPreset>();
        private bool writesBlockedByFutureVersion;

        public TriggerLabPresetStore(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("A preset-store path is required.",
                    nameof(filePath));
            }

            this.filePath = Path.GetFullPath(filePath);
        }

        public static TriggerLabPresetStore ForAppData(string appDataPath) =>
            new TriggerLabPresetStore(Path.Combine(appDataPath ??
                throw new ArgumentNullException(nameof(appDataPath)),
                DefaultFileName));

        public string FilePath => filePath;
        public IReadOnlyList<TriggerLabUserPreset> Presets => presets;

        public TriggerLabPresetLoadResult Load()
        {
            if (!File.Exists(filePath))
            {
                writesBlockedByFutureVersion = false;
                presets = Array.Empty<TriggerLabUserPreset>();
                return new TriggerLabPresetLoadResult(true,
                    "No user preset library exists yet.");
            }

            try
            {
                StoreDocument document = ReadDocument<StoreDocument>(filePath,
                    "preset library");
                writesBlockedByFutureVersion = false;
                presets = NormalizeStoredPresets(document.Presets);
                return new TriggerLabPresetLoadResult(true,
                    $"Loaded {presets.Count} user preset(s).");
            }
            catch (TriggerLabPresetFormatException exception)
            {
                // A newer, otherwise valid document belongs to a newer app.
                // Leave it in place instead of destroying data we do not
                // understand.
                writesBlockedByFutureVersion = true;
                presets = Array.Empty<TriggerLabUserPreset>();
                return new TriggerLabPresetLoadResult(false,
                    exception.Message);
            }
            catch (JsonException exception)
            {
                return RecoverFromCorruptFile(exception.Message);
            }
            catch (InvalidDataException exception)
            {
                return RecoverFromCorruptFile(exception.Message);
            }
            catch (Exception exception) when (exception is IOException ||
                exception is UnauthorizedAccessException)
            {
                return new TriggerLabPresetLoadResult(false,
                    $"The Trigger Lab preset library could not be read: {exception.Message}");
            }
        }

        public TriggerLabUserPreset Add(string name,
            TriggerLabEffectSettings effect)
        {
            string normalizedName = NormalizeName(name);
            TriggerLabEffectSettings normalizedEffect = (effect ??
                throw new ArgumentNullException(nameof(effect))).Clone();
            TriggerLabUserPreset preset = new TriggerLabUserPreset(
                $"user-{Guid.NewGuid():N}", normalizedName,
                normalizedEffect.Mode, normalizedEffect.StartPercent,
                normalizedEffect.WallPercent, normalizedEffect.ForcePercent);
            List<TriggerLabUserPreset> next = presets.ToList();
            next.Add(preset);
            SaveStore(next);
            presets = next.AsReadOnly();
            return preset;
        }

        public TriggerLabUserPreset Rename(string id, string name)
        {
            int index = FindPresetIndex(id);
            TriggerLabUserPreset previous = presets[index];
            TriggerLabUserPreset renamed = new TriggerLabUserPreset(previous.Id,
                NormalizeName(name), previous.Mode, previous.StartPercent,
                previous.WallPercent, previous.ForcePercent);
            List<TriggerLabUserPreset> next = presets.ToList();
            next[index] = renamed;
            SaveStore(next);
            presets = next.AsReadOnly();
            return renamed;
        }

        public bool Delete(string id)
        {
            int index = presets.ToList().FindIndex(item =>
                string.Equals(item.Id, id, StringComparison.Ordinal));
            if (index < 0)
            {
                return false;
            }

            List<TriggerLabUserPreset> next = presets.ToList();
            next.RemoveAt(index);
            SaveStore(next);
            presets = next.AsReadOnly();
            return true;
        }

        public int Import(string importPath)
        {
            ExportDocument document = ReadDocument<ExportDocument>(importPath,
                "preset import");
            if (document.Presets == null || document.Presets.Count == 0)
            {
                throw new InvalidDataException(
                    "The preset import contains no presets.");
            }

            List<TriggerLabUserPreset> imported = document.Presets
                .Select(ToImportedPreset).ToList();
            List<TriggerLabUserPreset> next = presets.Concat(imported).ToList();
            SaveStore(next);
            presets = next.AsReadOnly();
            return imported.Count;
        }

        public void Export(string exportPath, string presetId = null)
        {
            IReadOnlyList<TriggerLabUserPreset> selected = presets;
            if (!string.IsNullOrWhiteSpace(presetId))
            {
                selected = new[] { presets[FindPresetIndex(presetId)] };
            }

            ExportDocument document = new ExportDocument
            {
                SchemaVersion = CurrentSchemaVersion,
                Presets = selected.Select(preset => new ExportedPreset
                {
                    Name = preset.Name,
                    Mode = preset.Mode,
                    StartPercent = preset.StartPercent,
                    WallPercent = preset.WallPercent,
                    ForcePercent = preset.ForcePercent,
                }).ToList(),
            };
            AtomicWrite(exportPath, JsonSerializer.Serialize(document,
                jsonOptions));
        }

        private TriggerLabPresetLoadResult RecoverFromCorruptFile(
            string detail)
        {
            writesBlockedByFutureVersion = false;
            presets = Array.Empty<TriggerLabUserPreset>();
            string quarantinePath = string.Empty;
            try
            {
                quarantinePath = filePath + ".corrupt-" +
                    DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
                File.Move(filePath, quarantinePath, false);
            }
            catch (Exception exception) when (exception is IOException ||
                exception is UnauthorizedAccessException)
            {
                quarantinePath = string.Empty;
            }

            string recovery = quarantinePath.Length > 0
                ? " It was quarantined and a new library can be created."
                : " It was ignored and a new library can replace it on the next save.";
            return new TriggerLabPresetLoadResult(false,
                $"The Trigger Lab preset library is invalid: " +
                $"{detail.TrimEnd('.')}.{recovery}",
                quarantinePath);
        }

        private void SaveStore(IReadOnlyList<TriggerLabUserPreset> values)
        {
            if (writesBlockedByFutureVersion)
            {
                throw new TriggerLabPresetFormatException(
                    "The preset library belongs to a newer schema version and cannot be overwritten by this version of Thrum.");
            }
            StoreDocument document = new StoreDocument
            {
                SchemaVersion = CurrentSchemaVersion,
                Presets = values.Select(preset => new StoredPreset
                {
                    Id = preset.Id,
                    Name = preset.Name,
                    Mode = preset.Mode,
                    StartPercent = preset.StartPercent,
                    WallPercent = preset.WallPercent,
                    ForcePercent = preset.ForcePercent,
                }).ToList(),
            };
            AtomicWrite(filePath, JsonSerializer.Serialize(document,
                jsonOptions));
        }

        private static IReadOnlyList<TriggerLabUserPreset>
            NormalizeStoredPresets(IReadOnlyList<StoredPreset> values)
        {
            if (values == null)
            {
                throw new InvalidDataException(
                    "The preset library has no presets collection");
            }

            var normalized = new List<TriggerLabUserPreset>();
            var positions = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (StoredPreset value in values)
            {
                if (value == null || string.IsNullOrWhiteSpace(value.Id) ||
                    !value.Id.StartsWith("user-", StringComparison.Ordinal))
                {
                    throw new InvalidDataException(
                        "The preset library contains an invalid user preset id");
                }

                TriggerLabUserPreset preset = CreateNormalized(value.Id,
                    value.Name, value.Mode, value.StartPercent,
                    value.WallPercent, value.ForcePercent);
                if (positions.TryGetValue(preset.Id, out int position))
                {
                    normalized[position] = preset;
                }
                else
                {
                    positions.Add(preset.Id, normalized.Count);
                    normalized.Add(preset);
                }
            }
            return normalized.AsReadOnly();
        }

        private static TriggerLabUserPreset ToImportedPreset(
            ExportedPreset value)
        {
            if (value == null)
            {
                throw new InvalidDataException(
                    "The preset import contains an empty preset");
            }

            return CreateNormalized($"user-{Guid.NewGuid():N}", value.Name,
                value.Mode, value.StartPercent, value.WallPercent,
                value.ForcePercent);
        }

        private static TriggerLabUserPreset CreateNormalized(string id,
            string name, TriggerLabMode mode, int startPercent,
            int wallPercent, int forcePercent)
        {
            TriggerLabEffectSettings effect = new TriggerLabEffectSettings
            {
                Mode = mode,
                StartPercent = startPercent,
                WallPercent = wallPercent,
                ForcePercent = forcePercent,
            }.Normalize();
            return new TriggerLabUserPreset(id, NormalizeName(name),
                effect.Mode, effect.StartPercent, effect.WallPercent,
                effect.ForcePercent);
        }

        private int FindPresetIndex(string id)
        {
            for (int index = 0; index < presets.Count; index++)
            {
                if (string.Equals(presets[index].Id, id,
                    StringComparison.Ordinal))
                {
                    return index;
                }
            }
            throw new KeyNotFoundException(
                $"User preset '{id}' does not exist.");
        }

        private static string NormalizeName(string name)
        {
            string normalized = (name ?? string.Empty).Trim();
            if (normalized.Length == 0)
            {
                throw new InvalidDataException("A preset name is required.");
            }
            return normalized.Length <= 48 ? normalized :
                normalized.Substring(0, 48);
        }

        private static T ReadDocument<T>(string path, string description)
        {
            string json = File.ReadAllText(path);
            using JsonDocument parsed = JsonDocument.Parse(json);
            if (!parsed.RootElement.TryGetProperty("schemaVersion",
                    out JsonElement versionElement) ||
                !versionElement.TryGetInt32(out int version))
            {
                throw new InvalidDataException(
                    $"The {description} has no valid schemaVersion");
            }
            if (version > CurrentSchemaVersion)
            {
                throw new TriggerLabPresetFormatException(
                    $"The {description} uses schema version {version}, which is newer than supported version {CurrentSchemaVersion}.");
            }
            if (version != CurrentSchemaVersion)
            {
                throw new TriggerLabPresetFormatException(
                    $"The {description} uses unsupported schema version {version}.");
            }
            T document = JsonSerializer.Deserialize<T>(json, jsonOptions);
            if (document == null)
            {
                throw new InvalidDataException(
                    $"The {description} is empty");
            }
            return document;
        }

        private static void AtomicWrite(string path, string content)
        {
            string fullPath = Path.GetFullPath(path);
            string directory = Path.GetDirectoryName(fullPath);
            Directory.CreateDirectory(directory);
            string temporaryPath = Path.Combine(directory,
                $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");
            try
            {
                File.WriteAllText(temporaryPath, content,
                    new UTF8Encoding(false));
                File.Move(temporaryPath, fullPath, true);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
        }

        private sealed class StoreDocument
        {
            public int SchemaVersion { get; set; }
            public List<StoredPreset> Presets { get; set; }
        }

        private sealed class StoredPreset
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public TriggerLabMode Mode { get; set; }
            public int StartPercent { get; set; }
            public int WallPercent { get; set; }
            public int ForcePercent { get; set; }
        }

        private sealed class ExportDocument
        {
            public int SchemaVersion { get; set; }
            public List<ExportedPreset> Presets { get; set; }
        }

        private sealed class ExportedPreset
        {
            public string Name { get; set; }
            public TriggerLabMode Mode { get; set; }
            public int StartPercent { get; set; }
            public int WallPercent { get; set; }
            public int ForcePercent { get; set; }
        }
    }
}
