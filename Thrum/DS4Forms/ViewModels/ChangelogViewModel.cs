/*
DS4Windows
Copyright (C) 2023  Travis Nickles

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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Documents;
using HttpProgress;
using System.Text.Json;
using DS4Windows;
using MarkdownEngine = MdXaml.Markdown;

namespace DS4WinWPF.DS4Forms.ViewModels
{
    public class ChangelogViewModel
    {
        private string markdown;

        public string Markdown
        {
            get => markdown;
            set
            {
                markdown = value;
                MarkdownChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler MarkdownChanged;

        /// <summary>
        /// Release notes are read from this product's GitHub releases, so
        /// until it has published one there is genuinely nothing to show.
        /// An empty markdown document renders as a blank window, which reads
        /// as a broken feature rather than an accurate one.
        /// </summary>
        internal static string EmptyChangelogMarkdown =>
            $"No release notes yet.{Environment.NewLine}{Environment.NewLine}" +
            $"{ProductInfo.ProductName} has not published a release. Once it " +
            "does, the notes for each version appear here.";

        public async Task DisplayChangelog()
        {
            var changelog = await Changelog.GetChangelogMarkdown(true);
            Markdown = string.IsNullOrWhiteSpace(changelog) ?
                EmptyChangelogMarkdown : changelog;
        }
    }
}
