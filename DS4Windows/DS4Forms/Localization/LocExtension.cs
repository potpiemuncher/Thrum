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

using DS4WinWPF.Translations;
using System;
using System.Globalization;
using System.Resources;
using System.Windows.Markup;

namespace DS4WinWPF.DS4Forms.Localization
{
    /// <summary>
    /// Resolves a resource key once, while its XAML object is being created.
    /// Language changes intentionally take effect after an application restart.
    /// </summary>
    [MarkupExtensionReturnType(typeof(string))]
    public sealed class LocExtension : MarkupExtension
    {
        private const string ResourcesPrefix = "Resources:";

        public LocExtension()
        {
        }

        public LocExtension(string key)
        {
            Key = key;
        }

        [ConstructorArgument("key")]
        public string Key { get; set; }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            if (string.IsNullOrWhiteSpace(Key))
            {
                return MissingKeyPlaceholder(Key);
            }

            ResourceManager manager;
            string resourceKey;
            if (Key.StartsWith(ResourcesPrefix, StringComparison.Ordinal))
            {
                manager = DS4WinWPF.Properties.Resources.ResourceManager;
                resourceKey = Key.Substring(ResourcesPrefix.Length);
            }
            else
            {
                manager = Strings.ResourceManager;
                resourceKey = Key;
            }

            if (string.IsNullOrWhiteSpace(resourceKey))
            {
                return MissingKeyPlaceholder(Key);
            }

            string value = manager.GetString(resourceKey,
                CultureInfo.CurrentUICulture);
            return value ?? MissingKeyPlaceholder(Key);
        }

        private static string MissingKeyPlaceholder(string key)
        {
            return $"[[Missing localization: {key ?? "<empty>"}]]";
        }
    }
}
