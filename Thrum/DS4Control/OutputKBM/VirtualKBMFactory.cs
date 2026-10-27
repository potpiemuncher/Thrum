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

namespace DS4Windows.DS4Control
{
    public static class VirtualKBMFactory
    {
        public const string DEFAULT_IDENTIFIER = "default";

        public static VirtualKBMBase DetermineHandler(string identifier =
            SendInputHandler.IDENTIFIER)
        {
            VirtualKBMBase handler = null;
            if (identifier == DEFAULT_IDENTIFIER)
            {
                // Run through event handler discovery routine
                if (Global.fakerInputInstalled)
                {
                    handler = CreateFakerInputHandlerOrFallback();
                }
                else
                {
                    // Virtual KB+M driver not found. Use fallback system instead
                    handler = GetFallbackHandler();
                }
            }
            else if (identifier == SendInputHandler.IDENTIFIER)
            {
                handler = new SendInputHandler();
            }
            else if (identifier == FakerInputHandler.IDENTIFIER)
            {
                handler = CreateFakerInputHandlerOrFallback();
            }
            else
            {
                handler = GetFallbackHandler();
            }

            return handler;
            //return new SendInputHandler();
            //return new FakerInputMapping();
        }

        public static VirtualKBMMapping GetMappingInstance(string identifier =
            SendInputHandler.IDENTIFIER)
        {
            VirtualKBMMapping temp = null;
            if (identifier == SendInputHandler.IDENTIFIER)
            {
                temp = new SendInputMapping();
            }
            else if (identifier == FakerInputHandler.IDENTIFIER)
            {
                temp = new FakerInputMapping();
            }
            else
            {
                temp = GetFallbackMapping();
            }

            //VirtualKBMMapping temp = new VMultiMapping();
            //temp.PopulateConstants();
            return temp;
        }

        public static VirtualKBMBase GetFallbackHandler()
        {
            return new SendInputHandler();
        }

        // FakerInputDll.dll needs the Visual C++ runtime, which Thrum does not
        // ship. Its constructor used to throw out of ControlService.Start on a
        // PC with the FakerInput driver but no runtime, so the service never
        // started and no controller worked.
        private static VirtualKBMBase CreateFakerInputHandlerOrFallback()
        {
            try
            {
                return new FakerInputHandler();
            }
            catch (Exception ex) when (ex is DllNotFoundException ||
                ex is BadImageFormatException ||
                ex is EntryPointNotFoundException ||
                ex is TypeInitializationException)
            {
                AppLogger.LogToGui(
                    "FakerInput is installed but its helper DLL could not be loaded " +
                    "(the Microsoft Visual C++ Redistributable may be missing). " +
                    $"Using SendInput for keyboard and mouse output. {ex.GetType().Name}: {ex.Message}",
                    true);
                return GetFallbackHandler();
            }
        }

        public static VirtualKBMMapping GetFallbackMapping()
        {
            return new SendInputMapping();
        }

        /// <summary>
        /// Retrieves identifier string of fallback virtualkbm handler without
        /// creating an instance of the handler
        /// </summary>
        /// <returns>Identifier string of the default virtualkbm handler</returns>
        public static string GetFallbackHandlerIdentifier()
        {
            return SendInputHandler.IDENTIFIER;
        }

        public static bool IsValidHandler(string identifier)
        {
            bool result = false;
            switch (identifier)
            {
                case SendInputHandler.IDENTIFIER:
                    result = true;
                    break;
                case FakerInputHandler.IDENTIFIER:
                    result = true;
                    break;
                default:
                    break;
            }

            return result;
        }
    }
}
