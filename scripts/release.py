"""Build release archives for zorduzd.

Produces:
  target/dist/zorduzd-{version}-win-x86_64.tar.gz  (binaries)
  target/dist/zorduzd-{version}-src.zip             (sources)
  target/dist/zorduzd-{version}-src.tar.gz          (sources)
"""

import json
import shutil
import subprocess
import sys
import tarfile
import zipfile
from pathlib import Path

# Plugin files that go in the root of Zorduzd/
ROOT_FILES = ["zorduzd.dll", "zorduzd.pdb", "zorduzd.deps.json"]

# The Rust binary that goes inside DCS World/bin/
DCS_EXE = "DCS.exe"


def run(*args: str) -> str:
    result = subprocess.run(args, capture_output=True, text=True)
    if result.returncode != 0:
        sys.exit(f"{args[0]} failed:\n{result.stderr}")
    return result.stdout


def get_version() -> str:
    meta = json.loads(run("cargo", "metadata", "--no-deps", "--format-version=1"))
    return meta["packages"][0]["version"]


ICO_SIZES = [(16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)]


def generate_ico():
    """Convert assets/zorduzd.png to assets/zorduzd.ico."""
    from PIL import Image

    png = Path("assets/zorduzd.png")
    ico = Path("assets/zorduzd.ico")
    img = Image.open(png)
    img.save(ico, format="ICO", sizes=ICO_SIZES)
    print(f"Generated {ico} from {png}")


def build_release():
    generate_ico()
    print("Building release...")
    subprocess.run(["cargo", "build", "--release"], check=True)


def pack_binaries(release_dir: Path, dist_dir: Path, name: str):
    """Create a tarball mirroring the installed plugin layout.

    Zorduzd/
    ├── DCS World/bin/
    │   ├── DCS.exe
    │   └── zorduzd.cfg      (if assets/zorduzd.cfg exists)
    ├── zorduzd.dll
    ├── zorduzd.deps.json
    └── zorduzd.pdb
    """
    stage = dist_dir / "Zorduzd"
    if stage.exists():
        shutil.rmtree(stage)
    bin_dir = stage / "DCS World" / "bin"
    bin_dir.mkdir(parents=True)

    # DCS.exe
    shutil.copy2(release_dir / DCS_EXE, bin_dir / DCS_EXE)

    # Optional default config
    cfg = Path("assets/zorduzd.cfg")
    if cfg.exists():
        shutil.copy2(cfg, bin_dir / "zorduzd.cfg")

    # Root-level plugin files
    for f in ROOT_FILES:
        shutil.copy2(release_dir / f, stage / f)

    tar_path = dist_dir / f"{name}-win-x86_64.tar.gz"
    with tarfile.open(tar_path, "w:gz") as tf:
        tf.add(stage, arcname="Zorduzd")
    print(f"Created {tar_path}")


def pack_sources(dist_dir: Path, name: str):
    # Get the list of committed files from git
    files = run("git", "ls-files", "--cached").splitlines()

    zip_path = dist_dir / f"{name}-src.zip"
    with zipfile.ZipFile(zip_path, "w", zipfile.ZIP_DEFLATED) as zf:
        for f in files:
            zf.write(f, f"{name}-src/{f}")
    print(f"Created {zip_path}")

    tar_path = dist_dir / f"{name}-src.tar.gz"
    with tarfile.open(tar_path, "w:gz") as tf:
        for f in files:
            tf.add(f, f"{name}-src/{f}")
    print(f"Created {tar_path}")


def main():
    version = get_version()
    name = f"zorduzd-{version}"
    release_dir = Path("target/release")
    dist_dir = Path("target/dist")

    build_release()

    if dist_dir.exists():
        shutil.rmtree(dist_dir)
    dist_dir.mkdir(parents=True)

    pack_binaries(release_dir, dist_dir, name)
    pack_sources(dist_dir, name)

    print(f"\nDone. Artifacts in {dist_dir}/:")
    for p in sorted(dist_dir.iterdir()):
        size = p.stat().st_size
        print(f"  {p.name}  ({size:,} bytes)")


if __name__ == "__main__":
    main()
