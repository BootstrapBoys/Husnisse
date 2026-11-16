#!/usr/bin/env python3
"""Build the release zip for one version, verify it, and write the .sha256 file.

Layout matches the published packages: manifest.json, README.md, icon.png, CHANGELOG.md at the
archive root and the DLL under plugins/. Uses zipfile rather than a zip binary (the host has none).
"""
import hashlib
import json
import os
import sys
import zipfile

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
VERSION = sys.argv[1] if len(sys.argv) > 1 else "0.7.0"
OUT = os.path.join(ROOT, "artifacts/release", "Husnisse-v%s.zip" % VERSION)

FILES = [
    (os.path.join(ROOT, "resources/manifest.json"), "manifest.json"),
    (os.path.join(ROOT, "resources/README.md"), "README.md"),
    (os.path.join(ROOT, "resources/icon.png"), "icon.png"),
    (os.path.join(ROOT, "CHANGELOG.md"), "CHANGELOG.md"),
    (os.path.join(ROOT, "artifacts/debug/Husnisse.dll"), "plugins/Husnisse.dll"),
]


def sha(data):
    return hashlib.sha256(data).hexdigest()


def main():
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    with zipfile.ZipFile(OUT, "w", zipfile.ZIP_DEFLATED) as archive:
        for source, name in FILES:
            archive.write(source, name)

    with zipfile.ZipFile(OUT) as archive:
        bad = archive.testzip()
        names = archive.namelist()
        dll = archive.read("plugins/Husnisse.dll")
        manifest = json.loads(archive.read("manifest.json").decode("utf-8-sig"))

    with open(os.path.join(ROOT, "artifacts/debug/Husnisse.dll"), "rb") as handle:
        disk = handle.read()

    print("zip:", OUT)
    print("entries:", names)
    print("no_backslashes:", all("\\" not in n for n in names))
    print("testzip:", "ok" if bad is None else "BAD " + str(bad))
    print("manifest version:", manifest["version_number"])
    print("dll inside == build artifact:", sha(dll) == sha(disk), sha(disk)[:16])
    print("size:", os.path.getsize(OUT))

    with open(OUT, "rb") as handle:
        digest = sha(handle.read())
    with open(OUT + ".sha256", "w") as handle:
        handle.write(digest + "  Husnisse-v%s.zip\n" % VERSION)
    print("sha256:", digest)


if __name__ == "__main__":
    main()
