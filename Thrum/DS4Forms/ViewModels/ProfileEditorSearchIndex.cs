using System;
using System.Collections.Generic;
using System.Linq;

namespace DS4WinWPF.DS4Forms.ViewModels
{
    public sealed class ProfileEditorSearchEntry
    {
        public ProfileEditorSearchEntry(string label, string sectionName,
            object target)
        {
            Label = label ?? throw new ArgumentNullException(nameof(label));
            SectionName = sectionName ??
                throw new ArgumentNullException(nameof(sectionName));
            Target = target ?? throw new ArgumentNullException(nameof(target));
        }

        public string Label { get; }
        public string SectionName { get; }
        public object Target { get; }
    }

    public sealed class ProfileEditorSearchIndex
    {
        private readonly List<ProfileEditorSearchEntry> entries = new();

        public int Count => entries.Count;

        public void Clear() => entries.Clear();

        public void Add(string label, string sectionName, object target)
        {
            string normalizedLabel = Normalize(label);
            string normalizedSection = Normalize(sectionName);
            if (normalizedLabel.Length == 0 || normalizedSection.Length == 0 ||
                target == null)
            {
                return;
            }

            entries.Add(new ProfileEditorSearchEntry(normalizedLabel,
                normalizedSection, target));
        }

        public IReadOnlyList<ProfileEditorSearchEntry> Search(string query,
            int maximumResults = 24)
        {
            string normalizedQuery = Normalize(query);
            if (normalizedQuery.Length == 0 || maximumResults <= 0)
            {
                return Array.Empty<ProfileEditorSearchEntry>();
            }

            return entries
                .Select((entry, index) => new
                {
                    Entry = entry,
                    Index = index,
                    MatchIndex = entry.Label.IndexOf(normalizedQuery,
                        StringComparison.CurrentCultureIgnoreCase),
                })
                .Where(match => match.MatchIndex >= 0)
                .OrderBy(match => match.MatchIndex == 0 ? 0 : 1)
                .ThenBy(match => match.MatchIndex)
                .ThenBy(match => match.Entry.Label.Length)
                .ThenBy(match => match.Index)
                .Take(maximumResults)
                .Select(match => match.Entry)
                .ToArray();
        }

        private static string Normalize(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return string.Join(" ", value.Replace("_", string.Empty)
                .Split((char[])null,
                    StringSplitOptions.RemoveEmptyEntries)).Trim().TrimEnd(':');
        }
    }
}
