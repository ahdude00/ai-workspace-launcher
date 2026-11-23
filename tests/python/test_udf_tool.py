import json
from pathlib import Path
import subprocess
import sys
from zipfile import ZIP_DEFLATED, ZIP_STORED, ZipFile

import pytest

TOOL = Path(__file__).resolve().parents[2] / "src/AWL.Infrastructure/Documents/udf_tool.py"
XML = "<template><content><![CDATA[Örnek belge]]></content></template>".encode()


def cli(*args, success=True):
    result = subprocess.run([sys.executable, str(TOOL), *map(str, args)],
                            capture_output=True, encoding="utf-8", check=False)
    if success:
        assert result.returncode == 0, result.stderr
    else:
        assert result.returncode != 0
        assert result.stderr.startswith("Hata: ") and len(result.stderr.splitlines()) == 1
    return result.stdout


@pytest.fixture
def archive(tmp_path):
    path = tmp_path / "örnek belge.udf"
    with ZipFile(path, "w") as doc:
        doc.comment = b"AWL"
        doc.writestr("content.xml", XML, ZIP_STORED)
        doc.writestr("ekler/resim.bin", b"ABCD" * 10000, ZIP_DEFLATED, compresslevel=1)
    return path


def test_read_and_unchanged_round_trip(archive, tmp_path):
    assert cli("text", archive) == "Örnek belge"
    assert [e["compression"] for e in json.loads(cli("info", archive))] == [0, 8]
    folder, target = tmp_path / "aç", tmp_path / "kopya.udf"
    cli("unpack", archive, folder)
    cli("pack", folder, target)
    assert archive.read_bytes() == target.read_bytes()


def local_record(doc, path, name):
    start = doc.getinfo(name).header_offset
    end = min([e.header_offset for e in doc.infolist() if e.header_offset > start] + [doc.start_dir])
    return path.read_bytes()[start:end]


def test_edit_preserves_source_and_untouched_compressed_record(archive, tmp_path):
    original = archive.read_bytes()
    folder, target = tmp_path / "work", tmp_path / "edited.udf"
    cli("unpack", archive, folder)
    (folder / "content.xml").write_bytes(XML.replace(b"belge", b"metin"))
    cli("pack", folder, target)
    assert archive.read_bytes() == original
    assert cli("text", target) == "Örnek metin"
    with ZipFile(target) as new, ZipFile(archive) as old:
        assert new.testzip() is None
        assert old.namelist() == new.namelist()
        assert new.comment == old.comment
        assert local_record(old, archive, "ekler/resim.bin") == local_record(new, target, "ekler/resim.bin")


def test_protect_source_and_previous_output_on_error(archive, tmp_path):
    folder = tmp_path / "work"
    cli("unpack", archive, folder)
    cli("pack", folder, archive, success=False)
    target = tmp_path / "edited.udf"
    target.write_bytes(b"previous output")
    (folder / "content.xml").write_bytes(b"<broken")
    cli("pack", folder, target, success=False)
    assert target.read_bytes() == b"previous output"
    assert cli("text", archive) == "Örnek belge"


@pytest.mark.parametrize("name", ["../outside.xml", "/absolute.txt", "C:/invalid", "aux.txt"])
def test_unsafe_archive_rejected_before_extraction(archive, tmp_path, name):
    with ZipFile(archive, "a") as doc:
        doc.writestr(name, b"x")
    folder = tmp_path / "work"
    cli("unpack", archive, folder, success=False)
    assert not folder.exists()


def test_failure_messages_are_single_line(tmp_path):
    cli("text", tmp_path / "yok.udf", success=False)
    cli("pack", success=False)


def test_anonymized_official_form_round_trip_and_edit(tmp_path):
    # Malatya Bilirkişilik Bölge Kurulu, Nakil Talebi formu. CDATA characters
    # were masked without shifting the editor's text runs or paragraph offsets.
    source = Path(__file__).parent / "fixtures/anonim.udf"
    work, output = tmp_path / "form", tmp_path / "form-copy.udf"
    before = cli("text", source)
    cli("unpack", source, work)
    cli("pack", work, output)
    assert output.read_bytes() == source.read_bytes()
    xml = work / "content.xml"
    xml.write_bytes(xml.read_bytes().replace(b"XXX", b"AWL", 1))
    cli("pack", work, output)
    assert cli("text", output) == before.replace("XXX", "AWL", 1)
    assert cli("text", source) == before
