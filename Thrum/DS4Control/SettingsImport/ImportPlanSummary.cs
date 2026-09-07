/*
DS4Windows
Copyright (C) 2026  DS4Windows contributors

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

using System.Collections.Generic;
using System.Globalization;

namespace DS4Windows
{
    /// <summary>
    /// Turns a plan into the lines the first-run dialog shows.
    ///
    /// <para>It lives beside the planner rather than in the dialog so that the
    /// wording is unit-testable and so a later Settings-page entry point can
    /// reuse it verbatim instead of paraphrasing it.</para>
    ///
    /// <para>The wording lives in <c>Translations/Strings.resx</c> under the
    /// <c>Import.*</c> keys, neutral only; the translated files fall back to it
    /// until a translator fills them in.</para>
    /// </summary>
    public static class ImportPlanSummary
    {
        /// <summary>
        /// One line per kind of thing found, profiles first because that is
        /// what a user actually recognises as "my settings".
        /// </summary>
        public static IReadOnlyList<string> Describe(ImportPlan plan)
        {
            var lines = new List<string>();
            if (plan == null || plan.IsEmpty)
            {
                return lines;
            }

            int profiles = plan.ProfileCount;
            if (profiles > 0)
            {
                lines.Add(string.Format(CultureInfo.CurrentCulture,
                    profiles == 1
                        ? DS4WinWPF.Translations.Strings.Import_ProfileCountSingular
                        : DS4WinWPF.Translations.Strings.Import_ProfileCountPlural,
                    profiles));
            }

            AddIfPresent(plan, lines, ImportItemKind.AppSettings,
                DS4WinWPF.Translations.Strings.Import_KindAppSettings);
            AddIfPresent(plan, lines, ImportItemKind.AutoProfiles,
                DS4WinWPF.Translations.Strings.Import_KindAutoProfiles);
            AddIfPresent(plan, lines, ImportItemKind.Actions,
                DS4WinWPF.Translations.Strings.Import_KindActions);
            AddIfPresent(plan, lines, ImportItemKind.LinkedProfiles,
                DS4WinWPF.Translations.Strings.Import_KindLinkedProfiles);
            AddIfPresent(plan, lines, ImportItemKind.ControllerConfigs,
                DS4WinWPF.Translations.Strings.Import_KindControllerConfigs);
            AddIfPresent(plan, lines, ImportItemKind.OutputSlots,
                DS4WinWPF.Translations.Strings.Import_KindOutputSlots);

            int collisions = plan.CollisionCount;
            if (collisions > 0)
            {
                lines.Add(string.Format(CultureInfo.CurrentCulture,
                    collisions == 1
                        ? DS4WinWPF.Translations.Strings.Import_CollisionCountSingular
                        : DS4WinWPF.Translations.Strings.Import_CollisionCountPlural,
                    collisions));
            }

            return lines;
        }

        private static void AddIfPresent(ImportPlan plan, List<string> lines,
            ImportItemKind kind, string description)
        {
            if (plan.Contains(kind))
            {
                lines.Add(description);
            }
        }
    }
}
