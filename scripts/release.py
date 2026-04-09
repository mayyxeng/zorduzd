"""Build release archives for zorduzd.

Produces:
  target/dist/zorduzd-{version}-win-x86_64.zip   (binaries)
  target/dist/zorduzd-{version}-src.zip           (sources)
  target/dist/zorduzd-{version}-src.tar.gz        (sources)
"""

import json
import shutil
import subprocess
import sys
import tarfile
import zipfile
from pathlib import Path

BINARIES = ["DCS.exe", "zorduzd.dll", "zorduzd.pdb", "zorduzd.deps.json"]


def run(*args: str) -> str:
    result = subprocess.run(args, capture_output=True, text=True)
    if result.returncode != 0:
        sys.exit(f"{args[0]} failed:\n{result.stderr}")
    return result.stdout


def get_version() -> str:
    meta = json.loads(run("cargo", "metadata", "--no-deps", "--format-version=1"))
    return meta["packages"][0]["version"]


def build_release():
    print("Building release...")
    subprocess.run(["cargo", "build", "--release"], check=True)


def pack_binaries(release_dir: Path, dist_dir: Path, name: str):
    zip_path = dist_dir / f"{name}-win-x86_64.zip"
    with zipfile.ZipFile(zip_path, "w", zipfile.ZIP_DEFLATED) as zf:
        for f in BINARIES:
            src = release_dir / f
            zf.write(src, f"{f}")
        cfg = Path("assets/zorduzd.cfg")
        if cfg.exists():
            zf.write(cfg, f"zorduzd.cfg")
    print(f"Created {zip_path}")


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
