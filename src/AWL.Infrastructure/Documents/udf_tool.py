"""UYAP belgelerini inceleyip düzenlenmiş kopyalar oluşturur."""
import argparse
import copy
import hashlib
import json
import ntpath
import os
from pathlib import Path
import shutil
import zlib
import sys
import tempfile
import xml.etree.ElementTree as ET
from zipfile import BadZipFile, ZipFile

MANIFEST = ".awl-udf.json"


class DocumentError(Exception):
    pass


def digest(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def members(archive):
    entries = archive.infolist()
    if "content.xml" not in archive.namelist():
        raise DocumentError("UDF içinde content.xml bulunamadı.")
    seen = set()
    for item in entries:
        name = item.orig_filename
        parts = name.removesuffix("/").split("/")
        if (name != item.filename or any(c in name for c in '\\:<>"|?*')
                or any(ord(c) < 32 for c in name)
                or any(not p or p.endswith((".", " ")) or ntpath.isreserved(p)
                       for p in parts)
                or parts[0].casefold() == MANIFEST or name.casefold() in seen):
            raise DocumentError("Arşivde geçersiz veya yinelenen bir girdi adı var.")
        seen.add(name.casefold())
    return entries


def text(source):
    with ZipFile(source) as archive:
        members(archive)
        with archive.open("content.xml") as stream:
            root = ET.parse(stream).getroot()
        content = root.find("{*}content")
        if content is None:
            raise DocumentError("Belgenin metin alanı bulunamadı.")
        return "".join(content.itertext())


def info(source):
    with ZipFile(source) as archive:
        return [{"name": e.filename, "size": e.file_size, "compressed_size": e.compress_size,
                 "compression": e.compress_type} for e in members(archive)]


def unpack(source, folder):
    if folder.is_symlink() or (folder.exists() and (not folder.is_dir() or any(folder.iterdir()))):
        raise DocumentError("Açma hedefi yeni veya boş bir klasör olmalıdır.")
    source = source.resolve(strict=True)
    with tempfile.TemporaryDirectory(dir=folder.parent) as staging:
        work = Path(staging) / "work"
        work.mkdir()
        with ZipFile(source) as archive:
            entries = members(archive)
            for entry in entries:
                archive.extract(entry, work)
            manifest = {"source": str(source), "sha256": digest(source),
                        "entries": [entry.filename for entry in entries]}
            (work / MANIFEST).write_text(json.dumps(manifest, ensure_ascii=False), encoding="utf-8")
        if folder.exists():
            folder.rmdir()
        work.rename(folder)


class CopyingZipFile(ZipFile):
    def copy_record(self, source, entry, end):
        """Retain the original local header, compressed stream and any data descriptor.

        zipfile writes the central directory on close using these registered offsets.
        """
        saved = copy.copy(entry)
        saved.header_offset = self.fp.tell()
        source.seek(entry.header_offset)
        remaining = end - entry.header_offset
        while remaining > 0:
            chunk = source.read(min(remaining, 1024 * 1024))
            if not chunk:
                raise DocumentError("Arşiv girdisi eksik.")
            self.fp.write(chunk)
            remaining -= len(chunk)
        self.filelist.append(saved)
        self.NameToInfo[saved.filename] = saved
        self.start_dir = self.fp.tell()


def same_content(archive, entry, path):
    if entry.is_dir():
        if not path.is_dir():
            raise DocumentError("Manifestteki klasör eksik.")
        return True
    if not path.is_file():
        raise DocumentError("Manifestteki dosya eksik.")
    if path.stat().st_size != entry.file_size:
        return False
    with archive.open(entry) as old, path.open("rb") as new:
        while chunk := old.read(1024 * 1024):
            if chunk != new.read(len(chunk)):
                return False
        return not new.read(1)


def pack(folder, output):
    folder = folder.resolve(strict=True)
    manifest = json.loads((folder / MANIFEST).read_text(encoding="utf-8"))
    source = Path(manifest["source"]).resolve(strict=True)
    target = output.resolve()
    if target == source or (output.exists() and output.samefile(source)):
        raise DocumentError("Kaynak UDF dosyasının üzerine yazılamaz.")
    if target.is_relative_to(folder) or output.is_symlink():
        raise DocumentError("Çıktı için çalışma klasörü dışında yeni bir dosya seçin.")
    if digest(source) != manifest["sha256"]:
        raise DocumentError("Kaynak UDF açıldıktan sonra değişmiş.")
    with tempfile.TemporaryDirectory(dir=target.parent) as staging:
        temporary = Path(staging) / "result.udf"
        with ZipFile(source) as original:
            entries = members(original)
            if manifest["entries"] != [entry.filename for entry in entries]:
                raise DocumentError("Manifest ile kaynak arşiv uyuşmuyor.")
            changed = set()
            for entry in entries:
                path = folder / entry.filename
                if path.resolve() != path or not path.resolve().is_relative_to(folder):
                    raise DocumentError("Çalışma klasöründe bağlantılı dosya kullanılamaz.")
                if not same_content(original, entry, path):
                    changed.add(entry.filename)
            if "content.xml" in changed:
                ET.parse(folder / "content.xml")
            if not changed:
                shutil.copyfile(source, temporary)
            else:
                offsets = sorted(entry.header_offset for entry in entries)
                ends = dict(zip(offsets, offsets[1:] + [original.start_dir]))
                with source.open("rb") as raw, CopyingZipFile(temporary, "w") as result:
                    result.comment = original.comment
                    for entry in entries:
                        if entry.filename in changed:
                            with (folder / entry.filename).open("rb") as data:
                                with result.open(copy.copy(entry), "w") as dest:
                                    shutil.copyfileobj(data, dest)
                        else:
                            result.copy_record(raw, entry, ends[entry.header_offset])
        os.replace(temporary, target)


class Parser(argparse.ArgumentParser):
    def error(self, message):
        raise DocumentError("Komut veya bağımsız değişken eksik ya da geçersiz. --help ile kullanımı görüntüleyin.")


def main():
    parser = Parser(description=__doc__)
    commands = parser.add_subparsers(dest="command", required=True)
    for command, arguments in (("info", ("source",)), ("text", ("source",)),
                               ("unpack", ("source", "folder")), ("pack", ("folder", "output"))):
        sub = commands.add_parser(command)
        for argument in arguments:
            sub.add_argument(argument, type=Path)
    args = parser.parse_args()
    if args.command == "info":
        print(json.dumps(info(args.source), ensure_ascii=False))
    elif args.command == "text":
        sys.stdout.write(text(args.source))
    elif args.command == "unpack":
        unpack(args.source, args.folder)
    else:
        pack(args.folder, args.output)


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8", newline="")
    try:
        main()
    except DocumentError as exc:
        sys.exit(f"Hata: {exc}")
    except (BadZipFile, OSError, ValueError, KeyError, TypeError, RuntimeError, ET.ParseError, EOFError, zlib.error):
        sys.exit("Hata: Dosya okunamadı veya yazılamadı. UDF, XML, manifest ve dosya yollarını kontrol edin.")
