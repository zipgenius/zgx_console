#!/usr/bin/env python3
"""Repackage the existing self-contained Linux x64 publish with makepkg."""
import argparse
from datetime import date
import hashlib
import os
from pathlib import Path
import re
import shutil
import shlex
import subprocess
import tempfile

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--payload', type=Path, required=True)
parser.add_argument('--output', type=Path, required=True)
parser.add_argument('--version', required=True)
parser.add_argument('--release', default='1')
args = parser.parse_args()
if os.geteuid() == 0:
    parser.error('run makepkg as a regular user')
if not re.fullmatch(r'\d+\.\d+\.\d+\.\d+', args.version) or not re.fullmatch(r'[1-9]\d*', args.release):
    parser.error('invalid version or release')
source, output = args.payload.resolve(), args.output.resolve()
if not (source / 'zg').is_file() or not (source / 'libhostfxr.so').is_file():
    parser.error('a self-contained Linux publish is required')
output.mkdir(parents=True, exist_ok=True)
base = Path(__file__).resolve().parent
with tempfile.TemporaryDirectory(prefix='zg-console-arch-build-') as folder:
    work = Path(folder)
    payload = work / 'payload'
    shutil.copytree(source, payload, ignore=shutil.ignore_patterns('*.pdb'))
    for p in payload.rglob('*'):
        if not p.is_symlink():
            p.chmod(0o755 if p.is_dir() or p.name == 'zg' else 0o644)
    metadata = (base / 'it.zipgenius.Console.metainfo.xml').read_text()
    metadata = metadata.replace('@VERSION@', args.version).replace('@DATE@', date.today().isoformat())
    (work / 'metainfo.xml').write_text(metadata)
    for name in ('PKGBUILD', 'zg-console.install'):
        # Linux shell sources must have LF line endings even from Windows checkouts.
        (work / name).write_text((base / 'arch' / name).read_text(), newline='\n')
    environment = dict(os.environ, ZG_CONSOLE_PAYLOAD=str(payload),
                       ZG_CONSOLE_METAINFO=str(work / 'metainfo.xml'),
                       ZG_CONSOLE_VERSION=args.version, ZG_CONSOLE_RELEASE=args.release,
                       PACKAGER='The ZipGenius Team', PKGEXT='.pkg.tar.zst')
    if not Path('/var/lib/pacman/local').is_dir():
        # A non-Arch builder has no installed ALPM packages. Give makepkg's
        # read-only BUILDINFO query an empty private database, never the apt host.
        database = work / 'pacman-db'
        (database / 'local').mkdir(parents=True)
        query = work / 'pacman-query'
        query.write_text('#!/bin/sh\nexec ' + shlex.quote(shutil.which('pacman')) +
                         ' --dbpath ' + shlex.quote(str(database)) + ' "$@"\n')
        query.chmod(0o755)
        environment['PACMAN'] = str(query)
    # Dependency resolution is deliberately left to pacman on the destination.
    # This build step only copies already-published binaries and may run on Ubuntu.
    subprocess.run(['makepkg', '--nodeps', '--nocheck', '--noconfirm'], cwd=work, env=environment, check=True)
    packages = list(work.glob('*.pkg.tar.zst'))
    if len(packages) != 1:
        raise RuntimeError(f'Expected one Arch package, found {packages}')
    shutil.copy2(packages[0], output / packages[0].name)
    print('Arch package:', output / packages[0].name)
packages = sorted(p for p in output.iterdir() if p.name.endswith(('.deb', '.rpm', '.msi', '.exe', '.snap', '.flatpak', '.pkg.tar.zst')))
with (output / 'SHA256SUMS').open('w') as checksums:
    for p in packages:
        with p.open('rb') as f:
            checksums.write(f'{hashlib.file_digest(f, "sha256").hexdigest()}  {p.name}\n')
