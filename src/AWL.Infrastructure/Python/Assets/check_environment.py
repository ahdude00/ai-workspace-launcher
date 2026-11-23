"""Exit 2 for an invalid runtime and 1 for missing or outdated packages."""
import sys
from importlib import import_module
from importlib.metadata import PackageNotFoundError, version
from pathlib import Path

venv, runtime, requirements = map(Path, sys.argv[1:])
if (sys.version_info[:2] != (3, 13)
        or Path(sys.prefix).resolve() != venv.resolve()
        or not Path(sys.base_prefix).resolve().is_relative_to(runtime.resolve())):
    sys.exit(2)

try:
    import_module("pip")
except ImportError:
    sys.exit(2)

try:
    for line in requirements.read_text(encoding="utf-8").splitlines():
        if line and line[0].isalnum():
            name, pinned = line.split()[0].split("==")
            if version(name) != pinned:
                sys.exit(1)
    for module in ("docx", "pypdf", "pdfplumber", "openpyxl", "pandas", "reportlab", "lxml.etree"):
        import_module(module)
except (PackageNotFoundError, ImportError, OSError):
    sys.exit(1)
