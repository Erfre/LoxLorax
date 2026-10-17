"""Build upload-ready manual and Thunderstore/Hexium packages."""
from __future__ import annotations

import hashlib
import json
from pathlib import Path
from zipfile import ZIP_DEFLATED, ZipFile


ROOT = Path(__file__).resolve().parents[1]
DIST = ROOT / "dist" / "LoxLorax"
OUT = ROOT / "release-artifacts"
PLUGIN_FILES = ("LoxLorax.dll", "lorax.llmesh", "vile-lorax.llmesh")


def write_zip(path: Path, files: dict[str, Path]) -> None:
    with ZipFile(path, "w", ZIP_DEFLATED, compresslevel=9) as archive:
        for archive_name, source in files.items():
            if not source.is_file():
                raise FileNotFoundError(source)
            archive.write(source, archive_name)
    with ZipFile(path) as archive:
        if archive.testzip() is not None:
            raise RuntimeError(f"Corrupt ZIP: {path}")
        if set(archive.namelist()) != set(files):
            raise RuntimeError(f"Unexpected package contents: {path}")


manifest = json.loads((ROOT / "manifest.json").read_text(encoding="utf-8"))
version = manifest["version_number"]
OUT.mkdir(parents=True, exist_ok=True)

plugin_entries = {
    f"BepInEx/plugins/LoxLorax/{name}": DIST / name for name in PLUGIN_FILES
}
manual = OUT / f"LoxLorax-{version}-manual.zip"
store = OUT / f"LoxLorax-{version}-thunderstore-hexium.zip"

write_zip(
    manual,
    {
        **plugin_entries,
        "README.md": ROOT / "publishing" / "PACKAGE_README.md",
        "CHANGELOG.md": ROOT / "CHANGELOG.md",
    },
)
write_zip(
    store,
    {
        "manifest.json": ROOT / "manifest.json",
        "icon.png": ROOT / "icon.png",
        "README.md": ROOT / "publishing" / "PACKAGE_README.md",
        "CHANGELOG.md": ROOT / "CHANGELOG.md",
        **plugin_entries,
    },
)

lines = []
for path in (manual, store):
    lines.append(f"{hashlib.sha256(path.read_bytes()).hexdigest()}  {path.name}")
(OUT / "SHA256SUMS.txt").write_text("\n".join(lines) + "\n", encoding="utf-8")
print(f"PASS: built {manual.name} and {store.name}")
