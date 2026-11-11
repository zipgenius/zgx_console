#!/usr/bin/env python3
"""Package an existing linux-x64 self-contained publish; run on Linux."""
import argparse
from datetime import date
import os
from pathlib import Path
import re
import shutil
import subprocess
import tempfile


def run(*args):
    subprocess.run(args, check=True)


def write(path, content, mode=0o644):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(content, encoding="utf-8")
    path.chmod(mode)


parser = argparse.ArgumentParser()
parser.add_argument("--payload", type=Path, required=True)
parser.add_argument("--output", type=Path, required=True)
parser.add_argument("--version", default="10.0.0")
parser.add_argument("--release", default="1")
args = parser.parse_args()
if not re.fullmatch(r"\d+\.\d+\.\d+(\.\d+)?", args.version):
    parser.error("version must have three or four numeric components")
if not re.fullmatch(r"[1-9]\d*", args.release):
    parser.error("release must be a positive integer")
if not (args.payload / "zg").is_file():
    parser.error("payload must contain linux-x64 zg")
args.output.mkdir(parents=True, exist_ok=True)
output = args.output.resolve()
with tempfile.TemporaryDirectory(prefix="zgshell-packaging-") as temporary:
    work = Path(temporary)
    root = work / "root"
    app = root / "opt/zipgenius/zgshell"
    shutil.copytree(args.payload, app, ignore=shutil.ignore_patterns("*.pdb"))
    for entry in root.rglob("*"):
        entry.chmod(0o755 if entry.is_dir() or entry.name == "zg" else 0o644)
    (root / "usr/bin").mkdir(parents=True)
    (root / "usr/bin/zg").symlink_to("/opt/zipgenius/zgshell/zg")
    metadata = Path(__file__).with_name("it.zipgenius.Console.metainfo.xml").read_text(encoding="utf-8")
    metadata = metadata.replace("@VERSION@", args.version).replace("@DATE@", date.today().isoformat())
    write(root / "usr/share/metainfo/it.zipgenius.Console.metainfo.xml", metadata)
    # Run registration after unpack/configure, and removal while the binary exists.
    write(root / "DEBIAN/postinst", """#!/bin/sh
set -eu
case "$1" in
  configure) /opt/zipgenius/zgshell/zg -install --system-wide --silent ;;
esac
""", 0o755)
    write(root / "DEBIAN/prerm", """#!/bin/sh
set -eu
case "$1" in
  remove|deconfigure) /opt/zipgenius/zgshell/zg -uninstall --system-wide --silent ;;
esac
""", 0o755)
    write(root / "DEBIAN/postrm", """#!/bin/sh
set -eu
case "$1" in
  abort-upgrade|failed-upgrade)
    if [ -x /opt/zipgenius/zgshell/zg ]; then
      /opt/zipgenius/zgshell/zg -install --system-wide --silent
    fi ;;
esac
""", 0o755)
    size = sum(p.stat().st_size for p in app.rglob("*") if p.is_file()) // 1024
    write(root / "DEBIAN/control", f"""Package: zg-console
Version: {args.version}-{args.release}
Provides: zgshell
Conflicts: zgshell
Replaces: zgshell
Section: utils
Priority: optional
Architecture: amd64
Maintainer: The ZipGenius Team <support@zipgenius.com>
Installed-Size: {size}
Depends: libc6 (>= 2.34), libgcc-s1, libstdc++6, libicu78 | libicu76 | libicu74 | libicu72, libssl3t64 | libssl3, zlib1g, libarchive13t64 | libarchive13
Description: ZipGenius X Console
 ZipGenius X Console lets you create and extract archives, check their integrity, view detailed information about archives and disk images, and create self-extracting packages. Built-in ZipBomb protection is always active.
""")
    run("dpkg-deb", "--root-owner-group", "--build", str(root),
        str(output / f"zg-console_{args.version}-{args.release}_amd64.deb"))
    shutil.rmtree(root / "DEBIAN")
    top = work / "rpm"
    for directory in ("BUILD", "BUILDROOT", "RPMS", "SOURCES", "SPECS", "SRPMS"):
        (top / directory).mkdir(parents=True)
    spec = top / "SPECS/zg-console.spec"
    write(spec, f"""Name: zg-console
Version: {args.version}
Release: {args.release}
Summary: ZipGenius X Console
Vendor: The ZipGenius Team
Packager: The ZipGenius Team
License: GPL-3.0-only
URL: https://www.zipgenius.it/
BuildArch: x86_64
AutoReqProv: no
Provides: zgshell
Obsoletes: zgshell
Requires: glibc >= 2.34, libgcc, libstdc++, libicu, openssl-libs, zlib, libarchive
%global debug_package %{{nil}}
%global __os_install_post %{{nil}}

%description
ZipGenius X Console lets you create and extract archives, check their integrity, view detailed information about archives and disk images, and create self-extracting packages. Built-in ZipBomb protection is always active.

%install
mkdir -p %{{buildroot}}
cp -a "{root}/." %{{buildroot}}/

%post
/opt/zipgenius/zgshell/zg -install --system-wide --silent || exit $?

%posttrans
/opt/zipgenius/zgshell/zg -install --system-wide --silent || exit $?

%preun
if [ "$1" -eq 0 ]; then
  /opt/zipgenius/zgshell/zg -uninstall --system-wide --silent || exit $?
fi

%files
%dir /opt/zipgenius
/opt/zipgenius/zgshell
/usr/bin/zg
/usr/share/metainfo/it.zipgenius.Console.metainfo.xml
""")
    run("rpmbuild", "--define", f"_topdir {top}", "--target", "x86_64", "-bb", str(spec))
    packages = list((top / "RPMS").rglob("*.rpm"))
    if len(packages) != 1:
        raise RuntimeError(f"Expected one RPM, got {packages}")
    shutil.copy2(packages[0], output / packages[0].name)
