#!/usr/bin/env python3
"""Exercise a locally installed Snap or user Flatpak without changing registrations.

Install/uninstall with the package manager separately. Pass --kind snap|flatpak.
"""
import argparse
import hashlib
from pathlib import Path
import subprocess
import struct
import tempfile
import zipfile

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--kind', choices=('snap', 'flatpak'), required=True)
parser.add_argument('--version', required=True)
args = parser.parse_args()
command = ['snap', 'run', 'zg-console'] if args.kind == 'snap' else ['flatpak', 'run', 'it.zipgenius.Console']


def run(code, *options):
    result = subprocess.run([*command, *map(str, options)], capture_output=True, timeout=90)
    assert result.returncode == code, (options, result.returncode, result.stdout, result.stderr)
    return result.stdout.decode('utf-8', errors='replace')


# Snap home interface permits visible personal paths, not the host's /tmp tree.
with tempfile.TemporaryDirectory(prefix='zg-console-package-test-', dir=Path.home()) as temporary:
    root = Path(temporary)
    sample = root / 'sample.zip'
    with zipfile.ZipFile(sample, 'w') as archive:
        archive.writestr('sample.txt', 'archive package verification')
    digest = hashlib.sha256(sample.read_bytes()).digest()
    assert run(0).splitlines()[0] == f'ZipGenius X Console - {args.version}'
    assert 'zg -info' in run(0, '-help', '-info', '--silent')
    assert 'sample.txt' in run(0, '-list', sample, '--silent')
    run(0, '-test', sample, '--silent')
    assert 'Archive' in run(0, '-info', sample, '--verbose', '--silent')
    run(0, '-extract', sample, root / 'out', '--yes', '--silent')
    assert (root / 'out/sample.txt').read_text() == 'archive package verification'
    run(0, '-add', root / 'created.zip', '+' + str(root / 'out/sample.txt'), '--silent')
    run(0, '-test', root / 'created.zip', '--silent')
    run(0, '-sfx', '--list-targets', '--silent')
    run(0, '-sfx', sample, root / 'sample.run', '--ui=terminal', '--target=linux-x64', '--silent')
    assert (root / 'sample.run').read_bytes()[:4] == b'\x7fELF'
    run(7, '-install', '--silent')
    run(7, '-uninstall', '--silent')
    run(7, '-INSTALL', '--silent')
    run(7, '-UnInstall', '--silent')
    suspicious = root / 'suspicious.zip'
    with zipfile.ZipFile(suspicious, 'w') as archive:
        for i in range(4):
            archive.writestr(str(i) + '.zip', b'x')
    data = bytearray(suspicious.read_bytes())
    offset = data.index(b'PK\x01\x02')
    for i in range(4):
        struct.pack_into('<III', data, offset + 16, 0, 2524, 34902)
        name, extra, comment = struct.unpack_from('<HHH', data, offset + 28)
        offset += 46 + name + extra + comment
    suspicious.write_bytes(data)
    run(42, '-extract', suspicious, root / 'blocked', '--yes', '--silent')
    assert not (root / 'blocked').exists()
    assert hashlib.sha256(sample.read_bytes()).digest() == digest
print(f'PASS: {args.kind} startup, embedded help, add/list/test/info/extract, SFX, ZIP guard and registration policy')
