"""Reject personal paths, credentials, unsafe archives, and image metadata."""
from __future__ import annotations

import re
import subprocess
from pathlib import Path, PurePosixPath
from zipfile import ZipFile


ROOT = Path(__file__).resolve().parents[1]
TEXT_SUFFIXES = {".cs", ".json", ".md", ".py", ".sh", ".txt", ".yml", ".yaml"}
EMAIL = re.compile(rb"[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}", re.IGNORECASE)
HOME_PREFIXES = (b"/home" + b"/", b"/Users" + b"/")
SECRET_MARKERS = (
    b"BEGIN " + b"PRIVATE KEY",
    b"BEGIN RSA " + b"PRIVATE KEY",
    b"github" + b"_pat_",
    b"ghp" + b"_",
    b"AK" + b"IA",
    b"local." + b"erik",
    b"mfredrik" + b"sson",
)
PNG_PRIVATE_CHUNKS = {b"tEXt", b"zTXt", b"iTXt", b"eXIf", b"tIME"}


def tracked_files() -> list[Path]:
    output = subprocess.check_output(
        ["git", "ls-files", "-z"], cwd=ROOT
    ).decode("utf-8")
    return [ROOT / name for name in output.rstrip("\0").split("\0") if name]


def check_bytes(label: str, data: bytes, *, text: bool) -> None:
    for prefix in HOME_PREFIXES:
        if prefix in data:
            raise AssertionError(f"personal home path found in {label}")
    for marker in SECRET_MARKERS:
        if marker in data:
            raise AssertionError(f"credential marker found in {label}")
    if text:
        addresses = EMAIL.findall(data)
        if addresses:
            raise AssertionError(f"email address found in {label}: {addresses[0].decode()}")


def check_png(path: Path, data: bytes) -> None:
    if not data.startswith(b"\x89PNG\r\n\x1a\n"):
        raise AssertionError(f"invalid PNG signature: {path.relative_to(ROOT)}")
    offset = 8
    while offset + 12 <= len(data):
        length = int.from_bytes(data[offset : offset + 4], "big")
        chunk = data[offset + 4 : offset + 8]
        if chunk in PNG_PRIVATE_CHUNKS:
            raise AssertionError(
                f"metadata chunk {chunk.decode()} found in {path.relative_to(ROOT)}"
            )
        offset += 12 + length


def check_zip(path: Path) -> None:
    with ZipFile(path) as archive:
        for item in archive.infolist():
            member = PurePosixPath(item.filename)
            if member.is_absolute() or ".." in member.parts:
                raise AssertionError(f"unsafe ZIP member in {path.name}: {item.filename}")
            data = archive.read(item)
            check_bytes(
                f"{path.name}:{item.filename}",
                data,
                text=member.suffix.lower() in TEXT_SUFFIXES,
            )


for path in tracked_files():
    if not path.is_file():
        continue
    relative = path.relative_to(ROOT)
    data = path.read_bytes()
    check_bytes(str(relative), data, text=path.suffix.lower() in TEXT_SUFFIXES)
    if path.suffix.lower() == ".png":
        check_png(path, data)
    elif path.suffix.lower() == ".zip":
        check_zip(path)

print("PASS: tracked files contain no personal home paths, email addresses, credential markers, unsafe ZIP paths, or private PNG metadata")
