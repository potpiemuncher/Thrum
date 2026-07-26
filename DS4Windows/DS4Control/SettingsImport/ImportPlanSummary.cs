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
    /// <para>The strings are English-only on purpose: adding <c>.resx</c> keys
    /// is the localization pull request's job (plan task 1.8), and inventing
    /// keys here would put untranslated entries into 24 language files.</para>
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
                    profiles == 1 ? "{0} controller profile"
                                  : "{0} controller profiles",
                    profiles));
            }

            AddIfPresent(plan, lines, ImportItemKind.AppSettings,
                "App settings and profile assignments");
            AddIfPresent(plan, lines, ImportItemKind.AutoProfiles,
                "Auto-profile rules");
            AddIfPresent(plan, lines, ImportItemKind.Actions,
                "Special actions");
            AddIfPresent(plan, lines, ImportItemKind.LinkedProfiles,
                "Profiles linked to specific controllers");
            AddIfPresent(plan, lines, ImportItemKind.ControllerConfigs,
                "Per-controller settings");
            AddIfPresent(plan, lines, ImportItemKind.OutputSlots,
                "Output slot layout");

            int collisions = plan.CollisionCount;
            if (collisions > 0)
            {
                lines.Add(string.Format(CultureInfo.CurrentCulture,
                    collisions == 1
                        ? "{0} file is already present here and will be kept as it is"
                        : "{0} files are already present here and will be kept as they are",
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
