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
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DS4Windows;

namespace DS4WinWPF
{
    public class LogItem
    {
        private DateTime datetime;
        private string message;
        private bool warning;
        private LogCategory category;

        public DateTime Datetime { get => datetime; set => datetime = value; }
        public string Message { get => message; set => message = value; }
        public bool Warning { get => warning; set => warning = value; }
        public LogCategory Category { get => category; set => category = value; }
        public string CategoryDisplay => LogClassifier.GetDisplayName(category);
        public string Color
        {
            get
            {
                return warning ? "Red" : "Black";
            }
        }

        /// <summary>
        /// UI Automation uses ToString() as a list row's accessible name, so
        /// screen readers announce the log line instead of the type name.
        /// "G" matches the Log tab's Time column format.
        /// </summary>
        public override string ToString() => $"{datetime:G} {message}";
    }
}
