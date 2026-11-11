#!/usr/bin/env python3
"""Publish and archive an independent, self-contained console distribution."""
import argparse
import hashlib
import os
from pathlib import Path
import shutil
import subprocess
import tarfile
import zipfile

root = Path(__file__).resolve().parents[1]
p = argparse.ArgumentParser(description=__doc__)
p.add_argument('--rid', required=True, choices=['win-x64', 'win-arm64', 'linux-x64', 'linux-arm64', 'osx-x64', 'osx-arm64'])
p.add_argument('--build-number', type=int, required=True)
a = p.parse_args()
if not 1 <= a.build_number <= 65534:
    p.error('build-number must be 1..65534')
version = f'10.0.1.{a.build_number}'
payload = root / 'artifacts' / 'publish' / a.rid
dist = root / 'artifacts' / 'dist'
if payload.exists():
    shutil.rmtree(payload)
payload.mkdir(parents=True)
dist.mkdir(parents=True, exist_ok=True)
subprocess.run(['dotnet', 'publish', str(root / 'zgshell/zg.csproj'), '-c', 'Release', '-r', a.rid,
                '--self-contained', 'true', '-o', str(payload), f'-p:BuildNumber={a.build_number}'], check=True)
for name in ['LICENSE', 'THIRD_PARTY_NOTICES.md', 'README.md']:
    shutil.copy2(root / name, payload / name)
licenses = payload / 'licenses'
licenses.mkdir(exist_ok=True)
shutil.copy2(root / 'native/sfx_stub/third_party/License.txt', licenses / '7zip.txt')
# Preserve license metadata/files for the exact direct and transitive packages restored.
import json
assets = json.loads((root / 'zgshell/obj/project.assets.json').read_text(encoding='utf-8-sig'))
for library, data in assets['libraries'].items():
    if data['type'] != 'package':
        continue
    for folder in assets['packageFolders']:
        package = Path(folder) / data['path']
        if not package.is_dir():
            continue
        target = licenses / library.replace('/', '-')
        target.mkdir(exist_ok=True)
        for f in package.rglob('*'):
            if f.is_file() and (f.suffix == '.nuspec' or any(x in f.name.lower() for x in ('license', 'copying', 'notice'))):
                destination = target / f.relative_to(package)
                destination.parent.mkdir(parents=True, exist_ok=True)
                shutil.copy2(f, destination)
        break
for f in payload.rglob('*.pdb'):
    f.unlink()
name = f'zg-console-{version}-{a.rid}'
if a.rid.startswith('win-'):
    archive = dist / (name + '.zip')
    with zipfile.ZipFile(archive, 'w', zipfile.ZIP_DEFLATED) as z:
        for f in sorted(payload.rglob('*')):
            if f.is_file():
                z.write(f, Path(name) / f.relative_to(payload))
else:
    (payload / 'zg').chmod(0o755)
    archive = dist / (name + '.tar.gz')
    with tarfile.open(archive, 'w:gz') as t:
        t.add(payload, arcname=name)
print(archive)
