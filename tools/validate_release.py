"""Validate public repository metadata and both release archives."""
from __future__ import annotations

import hashlib
import json
import re
from pathlib import Path
from zipfile import ZipFile

from PIL import Image


ROOT = Path(__file__).resolve().parents[1]
manifest = json.loads((ROOT / "manifest.json").read_text(encoding="utf-8"))
version = manifest["version_number"]

required_keys = {"name", "version_number", "website_url", "description", "dependencies"}
assert set(manifest) == required_keys
assert manifest["name"] == "LoxLorax"
assert re.fullmatch(r"\d+\.\d+\.\d+", version)
assert manifest["website_url"] == "https://github.com/Erfre/LoxLorax"
assert len(manifest["description"]) <= 250
assert manifest["dependencies"] == ["denikson-BepInExPack_Valheim-5.4.2351"]

source = (ROOT / "src" / "Plugin.cs").read_text(encoding="utf-8")
plugin_version = re.search(r'BepInPlugin\(Id, "Lox Lorax", "([^"]+)"\)', source).group(1)
assert plugin_version == version

with Image.open(ROOT / "icon.png") as icon:
    assert icon.format == "PNG"
    assert icon.size == (256, 256)

manual = ROOT / "release-artifacts" / f"LoxLorax-{version}-manual.zip"
store = ROOT / "release-artifacts" / f"LoxLorax-{version}-thunderstore-hexium.zip"
plugin_names = {
    "BepInEx/plugins/LoxLorax/LoxLorax.dll",
    "BepInEx/plugins/LoxLorax/lorax.llmesh",
    "BepInEx/plugins/LoxLorax/vile-lorax.llmesh",
}
manual_names = plugin_names | {"README.md", "CHANGELOG.md"}
store_names = manual_names | {"manifest.json", "icon.png"}

for archive_path, expected in ((manual, manual_names), (store, store_names)):
    with ZipFile(archive_path) as archive:
        assert archive.testzip() is None
        assert set(archive.namelist()) == expected
        assert not any(name.lower().endswith(("assembly_valheim.dll", "unityengine.dll", "bepinex.dll")) for name in archive.namelist())
        for name in plugin_names:
            built = ROOT / "dist" / "LoxLorax" / Path(name).name
            assert archive.read(name) == built.read_bytes()

with ZipFile(store) as archive:
    assert json.loads(archive.read("manifest.json")) == manifest
    with Image.open(__import__("io").BytesIO(archive.read("icon.png"))) as packaged_icon:
        assert packaged_icon.size == (256, 256)

expected_sums = {
    path.name: hashlib.sha256(path.read_bytes()).hexdigest() for path in (manual, store)
}
listed_sums = {}
for line in (ROOT / "release-artifacts" / "SHA256SUMS.txt").read_text(encoding="utf-8").splitlines():
    digest, name = line.split("  ", 1)
    listed_sums[name] = digest
assert listed_sums == expected_sums

for image in ("living-in-game.png", "vile-in-game.png"):
    with Image.open(ROOT / "media" / "screenshots" / image) as screenshot:
        assert screenshot.format == "PNG" and min(screenshot.size) >= 900

readme = (ROOT / "README.md").read_text(encoding="utf-8")
assert "releases/latest" in readme
assert "media/screenshots/living-in-game.png" in readme
assert "media/screenshots/vile-in-game.png" in readme
assert "Steam achievement" in readme

package_readme = (ROOT / "publishing" / "PACKAGE_README.md").read_text(encoding="utf-8")
for image in ("cover.png", "living-in-game.png", "vile-in-game.png"):
    assert f"https://raw.githubusercontent.com/Erfre/LoxLorax/main/media/" in package_readme
    assert image in package_readme

workflow = ROOT / ".github" / "workflows" / "release.yml"
assert workflow.is_file()
assert (ROOT / ".github" / "workflows" / "verify.yml").is_file()
assert (ROOT / "tools" / "audit_repository.py").is_file()

print(f"PASS: Lox Lorax {version} metadata, icon, screenshots, packages, and checksums verified")
