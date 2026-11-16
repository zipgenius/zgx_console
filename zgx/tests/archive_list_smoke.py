"""Exercise archive listing without extracting files. Set ZG_TEST_APP to override the build."""
import os
import pathlib
import subprocess
import tempfile
import zipfile

root = pathlib.Path(__file__).resolve().parents[1]
app = pathlib.Path(os.environ.get('ZG_TEST_APP', root / 'bin/Debug/net10.0/zg.dll'))
launch = ['dotnet', str(app)] if app.suffix.lower() == '.dll' else [str(app)]

def listing(path):
    return subprocess.run([*launch, '-list', str(path), '--silent'], capture_output=True, timeout=40)

def member(name, data):
    header = f'{name + "/":<16}{0:<12}{0:<6}{0:<6}{"100644":<8}{len(data):<10}`\n'
    return header.encode('ascii') + data + (b'\n' if len(data) % 2 else b'')

with tempfile.TemporaryDirectory(prefix='zg-list-') as folder:
    work = pathlib.Path(folder)
    deb = work / 'package.deb'
    deb.write_bytes(b'!<arch>\n' + member('debian-binary', b'2.0\n') +
                    member('control.tar.gz', b'odd') + member('data.tar.xz', b'data'))
    before = deb.read_bytes()
    result = listing(deb)
    assert result.returncode == 0, (result.returncode, result.stdout, result.stderr)
    for name in (b'debian-binary', b'control.tar.gz', b'data.tar.xz'):
        assert name in result.stdout, result.stdout
    assert deb.read_bytes() == before
    broken = work / 'broken.deb'
    broken.write_bytes(before[:-2])
    assert listing(broken).returncode != 0
    archive = work / 'archive.zip'
    with zipfile.ZipFile(archive, 'w') as z:
        z.writestr('folder/file.txt', 'content')
    result = listing(archive)
    assert result.returncode == 0 and b'folder/file.txt' in result.stdout, (result.stdout, result.stderr)
