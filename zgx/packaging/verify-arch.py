#!/usr/bin/env python3
"""Verify in a disposable, dependency-ready Arch root using real pacman.

Run as root; --root must be a dedicated Arch test root, never the host root.
"""
import argparse
import re
from pathlib import Path
import subprocess
import tempfile
import zipfile

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--root', type=Path, required=True)
parser.add_argument('--package', type=Path, required=True)
parser.add_argument('--version', required=True)
args = parser.parse_args()
if not re.fullmatch(r"\d+\.\d+\.\d+\.\d+", args.version):
    parser.error("invalid version")
root, package = args.root.resolve(), args.package.resolve()
if root == Path('/') or not (root / 'etc/os-release').exists():
    parser.error('a dedicated Arch root is required')
if 'ID=arch' not in (root / 'etc/os-release').read_text():
    parser.error('the test root is not Arch Linux')
if (root / 'opt/zipgenius/zgshell').exists():
    parser.error('refusing to replace an existing console installation')
if not package.is_file():
    parser.error('package missing')

with tempfile.TemporaryDirectory(prefix='zg-console-arch-verify-') as temp:
    fixture = Path(temp) / 'sample.zip'
    with zipfile.ZipFile(fixture, 'w') as archive:
        archive.writestr('sample.txt', 'Arch Linux package verification')
    script = r'''set -eu
app=/opt/zipgenius/zgshell/zg
profile=/etc/profile.d/zipgenius-x.sh
if [ -e "$profile" ]; then echo 'Test profile already exists' >&2; exit 1; fi
printf '# Arch package verification\nexport ZG_ARCH_TEST=1\n' > "$profile"
cp "$profile" /tmp/profile-baseline
pacman -Qip /tmp/zg-console.pkg.tar.zst
pacman -U --noconfirm /tmp/zg-console.pkg.tar.zst
test "$(pacman -Q zg-console)" = "zg-console VERSION-1"
test "$(readlink /usr/bin/zg)" = "$app"
test -x "$app"
test "$("$app" | head -n1)" = 'ZipGenius X Console - VERSION'
"$app" -help -info --silent
"$app" -test /tmp/sample.zip --silent
"$app" -info /tmp/sample.zip --verbose --silent
"$app" -extract /tmp/sample.zip /tmp/zg-arch-out --yes --silent
test "$(cat /tmp/zg-arch-out/sample.txt)" = 'Arch Linux package verification'
"$app" -add /tmp/zg-arch-created.zip +/tmp/zg-arch-out/sample.txt --silent
"$app" -test /tmp/zg-arch-created.zip --silent
"$app" -sfx /tmp/sample.zip /tmp/zg-arch-sfx.run --ui=terminal --target=linux-x64 --silent
test -x /tmp/zg-arch-sfx.run
grep -q 'export PATH=' "$profile"
test -f /usr/share/metainfo/it.zipgenius.Console.metainfo.xml
pacman -U --noconfirm /tmp/zg-console.pkg.tar.zst
test "$(grep -c 'export PATH=' "$profile")" = 1
pacman -R --noconfirm zg-console
test ! -e "$app"
test ! -L /usr/bin/zg
test ! -e /usr/share/metainfo/it.zipgenius.Console.metainfo.xml
test "$(sha256sum < "$profile")" = "$(sha256sum < /tmp/profile-baseline)"
rm "$profile"
echo 'PASS: native Arch dependency resolution, install/reinstall/removal, commands, SFX and registration cleanup'
'''.replace('VERSION', args.version)
    command = ['bwrap', '--bind', str(root), '/', '--dev', '/dev', '--proc', '/proc', '--tmpfs', '/tmp',
               '--ro-bind', str(package), '/tmp/zg-console.pkg.tar.zst',
               '--ro-bind', str(fixture), '/tmp/sample.zip',
               '--unshare-pid', '--die-with-parent', '--cap-add', 'CAP_SYS_CHROOT',
               '--setenv', 'PATH', '/usr/bin:/bin', '--setenv', 'HOME', '/root',
               '--setenv', 'LANG', 'C.UTF-8', '/bin/bash', '-c', script]
    subprocess.run(command, check=True)
