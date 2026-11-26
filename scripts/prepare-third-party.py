"""Stage verified Windows installer dependencies in the supplied directory."""
import argparse
import hashlib
import json
from pathlib import Path
import shutil
import tempfile
import urllib.request
import zipfile


def prepare(target: Path) -> None:
    root = Path(__file__).resolve().parents[1]
    manifest = json.loads((root / "third_party/versions.json").read_text("utf-8"))
    target.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(dir=target) as temp:
        staging = Path(temp)
        for name in ("uv", "windows-terminal"):
            package = manifest[name]
            archive = staging / f"{name}.zip"
            with urllib.request.urlopen(package["url"], timeout=120) as response, archive.open("wb") as out:
                shutil.copyfileobj(response, out)
            with archive.open("rb") as stream:
                digest = hashlib.file_digest(stream, "sha256").hexdigest()
            if digest != package["sha256"].lower():
                raise ValueError(f"{name} {package['version']}: incorrect SHA256")
        with zipfile.ZipFile(staging / "uv.zip") as uv:
            with uv.open("uv.exe") as src, (staging / "uv.exe").open("wb") as dst:
                shutil.copyfileobj(src, dst)
        for filename in ("uv.exe", "windows-terminal.zip"):
            (staging / filename).replace(target / filename)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("destination", type=Path)
    prepare(parser.parse_args().destination)
