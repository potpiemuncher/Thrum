// Thrum icon generator — entry point.
//
// Copyright (C) 2026  Thrum project
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <https://www.gnu.org/licenses/>.

using System.Drawing;
using Thrum.IconGenerator;

// Regenerates Thrum's placeholder icon set into DS4Windows/Resources.
//
//     dotnet run --project utils/generate-thrum-icons [output-directory]
//
// The point of committing this tool alongside the icons it produces is that
// the icons are placeholders. When a real mark arrives, whoever replaces them
// needs the exact frame recipe — sizes, formats, the BMP/PNG split — not just
// the binaries, and reproducing that recipe from a .ico by inspection is
// tedious and easy to get subtly wrong.

string outputDirectory = args.Length > 0
    ? Path.GetFullPath(args[0])
    : ResolveDefaultOutputDirectory();

if (!Directory.Exists(outputDirectory))
{
    Console.Error.WriteLine($"Output directory does not exist: {outputDirectory}");
    return 1;
}

Console.WriteLine($"Writing Thrum placeholder icons to {outputDirectory}");
Console.WriteLine(
    "Frames per icon: " +
    string.Join(", ", IcoWriter.BmpFrameSizes.Select(size => $"{size}px BMP")) + ", " +
    string.Join(", ", IcoWriter.PngFrameSizes.Select(size => $"{size}px PNG")));

int written = 0;

Emit("Thrum.ico", (graphics, size) => ThrumMark.DrawColored(graphics, size));
Emit("Thrum - White.ico", (graphics, size) =>
    ThrumMark.DrawMonochrome(graphics, size, Color.White));
Emit("Thrum - Black.ico", (graphics, size) =>
    ThrumMark.DrawMonochrome(graphics, size, Color.Black));

// The battery icons keep their inherited numeric file names on purpose. The
// tray view model composes these paths arithmetically from the battery
// percentage; renaming them would mean rewriting that switch for no gain, and
// the names describe a level rather than a brand, so there is nothing to
// rebrand about them.
for (int percent = 0; percent <= 100; percent += 10)
{
    int captured = percent;
    Emit($"{percent}.ico", (graphics, size) =>
        ThrumMark.DrawBattery(graphics, size, captured));
}

Console.WriteLine($"Wrote {written} icon files.");
return 0;

void Emit(string fileName, Action<Graphics, int> render)
{
    string path = Path.Combine(outputDirectory, fileName);
    IcoWriter.Write(path, render);
    written++;
    Console.WriteLine($"  {fileName}  ({new FileInfo(path).Length:N0} bytes)");
}

// Walks up from the running binary to the repository root so the tool works
// from any working directory, which is what `dotnet run --project` needs.
static string ResolveDefaultOutputDirectory()
{
    var directory = new DirectoryInfo(AppContext.BaseDirectory);
    while (directory is not null)
    {
        string candidate = Path.Combine(directory.FullName, "DS4Windows", "Resources");
        if (Directory.Exists(candidate))
        {
            return candidate;
        }

        directory = directory.Parent;
    }

    throw new DirectoryNotFoundException(
        "Could not locate DS4Windows/Resources above " + AppContext.BaseDirectory +
        ". Pass the output directory as the first argument.");
}
