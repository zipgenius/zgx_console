#!/usr/bin/env python3
"""Build local x64 Snap/Flatpak bundles from the existing self-contained publish.

No store publication and no registration of PATH entries on the build host.
"""
import argparse
from datetime import date
import hashlib
import os
from pathlib import Path
import re
import shutil
import subprocess
import tempfile

APP_ID = 'it.zipgenius.Console'
DESCRIPTION = ('ZipGenius X Console lets you create and extract archives, check their '
               'integrity, view detailed information about archives and disk images, '
               'and create self-extracting packages. Built-in ZipBomb protection is always active.')


def run(*args):
    subprocess.run([str(a) for a in args], check=True)


def write(path, text, mode=0o644):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(text, encoding='utf-8')
    path.chmod(mode)


def copy_payload(source, target):
    shutil.copytree(source, target, ignore=shutil.ignore_patterns('*.pdb'))
    for p in target.rglob('*'):
        if not p.is_symlink():
            p.chmod(0o755 if p.is_dir() or p.name == 'zg' else 0o644)


def launcher(binary):
    return '''#!/bin/sh
case "$1" in
  -[Ii][Nn][Ss][Tt][Aa][Ll][Ll]|-[Uu][Nn][Ii][Nn][Ss][Tt][Aa][Ll][Ll])
    echo 'This package is managed by Snap/Flatpak. Use the package manager to install or remove it.' >&2
    exit 7 ;;
esac
exec ''' + binary + ''' "$@"
'''


def snap(payload, output, version, work):
    root = work / 'snap'
    copy_payload(payload, root / 'lib/zg-console')
    # core26 is Ubuntu 26.04. Stage non-base .NET native dependencies from
    # that same distribution; libc and the ELF loader come from the base.
    os_release = Path('/etc/os-release').read_text()
    if 'ID=ubuntu' not in os_release or 'VERSION_ID="26.04"' not in os_release:
        raise RuntimeError('The core26 snap must be assembled on Ubuntu 26.04.')
    native = root / 'usr/lib/x86_64-linux-gnu'
    native.mkdir(parents=True)
    for pattern in ('libicu*.so*', 'libssl.so*', 'libcrypto.so*',
                    'libstdc++.so*', 'libgcc_s.so*', 'libz.so*'):
        libraries = list(Path('/usr/lib/x86_64-linux-gnu').glob(pattern))
        if not libraries:
            raise RuntimeError('Missing native dependency: ' + pattern)
        for lib in libraries:
            shutil.copy2(lib, native / lib.name, follow_symlinks=True)
    write(root / 'bin/zg', launcher('"$SNAP/lib/zg-console/zg"'), 0o755)
    write(root / 'meta/snap.yaml', f'''name: zg-console
version: '{version}'
summary: Create, inspect and test archives and disk images
description: |
  {DESCRIPTION}
base: core26
architectures: [amd64]
grade: stable
confinement: strict
environment:
  LD_LIBRARY_PATH: $SNAP/usr/lib/x86_64-linux-gnu
  DOTNET_ROOT: $SNAP/lib/zg-console
apps:
  zg-console:
    command: bin/zg
    plugs: [home, removable-media]
''')
    run('snap', 'pack', root, output, '--filename', f'zg-console_{version}_amd64.snap')


def flatpak(payload, output, version, runtime, work):
    root = work / 'flatpak'
    # There is no compilation: the Platform also suffices as the build SDK.
    run('flatpak', 'build-init', '--arch=x86_64', root, APP_ID,
        'org.freedesktop.Platform', 'org.freedesktop.Platform', runtime)
    copy_payload(payload, root / 'files/lib/zg-console')
    write(root / 'files/bin/zg', launcher('/app/lib/zg-console/zg'), 0o755)
    metadata = Path(__file__).with_name('it.zipgenius.Console.metainfo.xml').read_text()
    metadata = metadata.replace('@VERSION@', version).replace('@DATE@', date.today().isoformat())
    # Flatpak identity is the application ID, not the DEB/RPM package name.
    metadata = metadata.replace('  <pkgname>zg-console</pkgname>\n', '')
    write(root / f'files/share/metainfo/{APP_ID}.metainfo.xml', metadata)
    run('flatpak', 'build-finish', '--command=zg', '--filesystem=host', root)
    repo = work / 'flatpak-repo'
    run('flatpak', 'build-export', '--arch=x86_64', repo, root, 'stable')
    run('flatpak', 'build-bundle', '--arch=x86_64',
        '--runtime-repo=https://dl.flathub.org/repo/flathub.flatpakrepo',
        repo, output / f'zg-console-{version}-x86_64.flatpak', APP_ID, 'stable')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--payload', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--version', required=True)
    parser.add_argument('--format', choices=('snap', 'flatpak', 'all'), default='all')
    parser.add_argument('--runtime', default='25.08')
    args = parser.parse_args()
    if not re.fullmatch(r'\d+\.\d+\.\d+\.\d+', args.version):
        parser.error('version must match the published assembly version (four numeric fields)')
    if not re.fullmatch(r'\d{2}\.\d{2}', args.runtime):
        parser.error('invalid Freedesktop runtime branch')
    payload, output = args.payload.resolve(), args.output.resolve()
    if not (payload / 'zg').is_file() or not (payload / 'libhostfxr.so').is_file():
        parser.error('payload must be a self-contained Linux publish')
    output.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix='zg-console-sandbox-build-') as temporary:
        work = Path(temporary)
        if args.format in ('snap', 'all'):
            snap(payload, output, args.version, work)
        if args.format in ('flatpak', 'all'):
            flatpak(payload, output, args.version, args.runtime, work)
    packages = sorted(p for p in output.iterdir() if p.name.endswith(('.msi', '.exe', '.deb', '.rpm', '.snap', '.flatpak', '.pkg.tar.zst')))
    checksums = []
    for p in packages:
        with p.open('rb') as f:
            digest = hashlib.file_digest(f, 'sha256').hexdigest()
        checksums.append(f'{digest}  {p.name}\n')
    write(output / 'SHA256SUMS', ''.join(checksums))


if __name__ == '__main__':
    main()
