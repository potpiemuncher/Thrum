import os
from pathlib import Path
import sys
import shutil
import subprocess
import zipfile

target_dir = Path(sys.argv[1])
repo_dir = Path(sys.argv[2]).resolve()
version = sys.argv[3]

required_package_root_files = ("NOTICE.txt", "COPYING")

def copy_required_package_root_files():
    for file_name in required_package_root_files:
        source_path = repo_dir / file_name
        if not source_path.is_file():
            raise FileNotFoundError(
                f"Required release file does not exist: {source_path}")

        shutil.copy2(source_path, target_dir / file_name)


def verify_release_archive(archive_path):
    archive_root = "Thrum/"
    manifest_entry = archive_root + ".thrum-managed-files.txt"
    expected_archive_entries = {
        archive_root + file_name
        for file_name in required_package_root_files
    }

    with zipfile.ZipFile(archive_path, "r") as archive:
        archive_entries = set(archive.namelist())
        missing_entries = sorted(expected_archive_entries - archive_entries)
        if missing_entries:
            raise RuntimeError(
                "Release archive is missing required legal file(s): " +
                ", ".join(missing_entries))

        if manifest_entry not in archive_entries:
            raise RuntimeError(
                f"Release archive is missing {manifest_entry}")

        managed_files = {
            line.strip()
            for line in archive.read(manifest_entry).decode("utf-8").splitlines()
            if line.strip()
        }
        missing_managed_files = sorted(
            set(required_package_root_files) - managed_files)
        if missing_managed_files:
            raise RuntimeError(
                "Managed-files manifest is missing required legal file(s): " +
                ", ".join(missing_managed_files))


# move l18n assemblies to a separate directory
lang_dir = target_dir / "Lang"
if not lang_dir.exists():
    Path.mkdir(lang_dir)

langs = ["ar", "cs", "de", "el", "es", "fi", "fr", "he", "hu-HU", "idn", "it", "ja", "ms",
         "nl", "pl", "pt", "pt-BR", "ru", "se", "tr", "uk-UA", "vi", "zh-Hans", "zh-Hant", "zh-CN"]
for lang in langs:
    current_lang_dir = target_dir / lang
    target_lang_dir = lang_dir / lang
    if not target_lang_dir.exists():
        target_lang_dir.mkdir()

    if current_lang_dir.exists():
        for file in current_lang_dir.iterdir():
            if file.is_file():
                shutil.move(file, target_lang_dir / file.name)
        current_lang_dir.rmdir()


# run the script injecting new dependency paths to <AssemblyName>.deps.json
lang_script = repo_dir / "utils" / "inject_deps_path.py"
deps_json_path = target_dir / "Thrum.deps.json"
subprocess.run([sys.executable, str(lang_script), str(deps_json_path)], check=True)

# The archive is the conveyed product. Keep its top-level attribution and GPL
# licence beside the executable, before the updater manifest records ownership.
copy_required_package_root_files()

# Record every file owned by this package. DS4Updater uses this manifest on the
# next update to remove package files that no longer ship, without touching
# profiles, settings, plugins, or other user-created content.
manifest_name = ".thrum-managed-files.txt"
manifest_path = target_dir / manifest_name
managed_files = sorted(
    file.relative_to(target_dir).as_posix()
    for file in target_dir.rglob("*")
    if file.is_file() and file.name != manifest_name
)
manifest_path.write_text("\n".join(managed_files) + "\n", encoding="utf-8")


# write the version to newest.txt
newest_txt = repo_dir / "newest.txt"
with open(newest_txt, 'w') as file:
    file.write(version)


# rename target dir (net8.0-windows) to Thrum
renamed_dir = target_dir.parent / "Thrum"
if renamed_dir.exists():
    shutil.rmtree(renamed_dir)

os.rename(target_dir, renamed_dir)

# create a zip
arch = target_dir.parents[1].name
zip_name = f"Thrum_{version}_{arch}"
target_zip_path = target_dir.parent / f"{zip_name}.zip"
if target_zip_path.exists():
    os.remove(target_zip_path)

zip_dir = shutil.make_archive(zip_name, "zip", target_dir.parent)

# move the zip to the build directory
shutil.move(zip_dir, target_zip_path)

# CI and release builds both execute this script, so validate the real artifact
# rather than relying only on a source-level packaging assertion.
verify_release_archive(target_zip_path)
