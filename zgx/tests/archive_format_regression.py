"""Cross-platform regression tests for archive listing and related commands."""
import bz2
import io
import lzma
import os
import pathlib
import subprocess
import tarfile
import tempfile
import zipfile

root = pathlib.Path(__file__).resolve().parents[1]
app = pathlib.Path(os.environ.get('ZG_TEST_APP', root / 'bin/Debug/net10.0/zg.dll'))
launch = ['dotnet', str(app)] if app.suffix.lower() == '.dll' else [str(app)]

def run(*args):
    p = subprocess.run([*launch, *map(str, args), '--silent'], capture_output=True, timeout=90)
    assert p.returncode == 0, (args, p.returncode, p.stdout, p.stderr)
    return p.stdout

def member(name, data):
    return (f'{name + "/":<16}{0:<12}{0:<6}{0:<6}{"100644":<8}{len(data):<10}`\n'.encode() +
            data + (b'\n' if len(data) % 2 else b''))

with tempfile.TemporaryDirectory(prefix='zg-format-regression-') as temp:
    work = pathlib.Path(temp)
    content = b'content of a sample entry\n'
    (work / 'source.txt').write_bytes(content)
    deb = work / 'sample.deb'
    deb.write_bytes(b'!<arch>\n' + member('debian-binary', b'2.0\n') +
                    member('control.tar.gz', b'control') + member('data.tar.xz', b'data'))
    before = deb.read_bytes()
    names = ('debian-binary', 'control.tar.gz', 'data.tar.xz')
    assert all(name.encode() in run('-list', deb) for name in names)
    run('-test', deb)
    run('-info', deb)
    report = work / 'report'
    run('-export', deb, 'TXT', report)
    assert all(name in (report / 'sample.txt').read_text() for name in names)
    dest = work / 'debian'
    run('-extract', deb, dest, 'O0')
    assert (dest / 'debian-binary').read_bytes() == b'2.0\n'
    assert (dest / 'control.tar.gz').read_bytes() == b'control'
    run('-convert', deb, work / 'converted.zip')
    with zipfile.ZipFile(work / 'converted.zip') as z:
        assert all(name in z.namelist() for name in names)
    assert deb.read_bytes() == before
    unsafe = work / 'unsafe.deb'
    unsafe.write_bytes(b'!<arch>\n' + member('../escape', b'data'))
    result = subprocess.run([*launch, '-extract', str(unsafe), str(work / 'unsafe-out'), 'O0', '--silent'], capture_output=True, timeout=30)
    assert result.returncode != 0 and not (work / 'escape').exists()
    damaged = work / 'truncated.deb'
    damaged.write_bytes(before[:-3])
    result = subprocess.run([*launch, '-test', str(damaged), '--silent'], capture_output=True, timeout=30)
    assert result.returncode != 0
    invalid = work / 'invalid.xz'
    invalid.write_bytes(b'not an xz stream')
    result = subprocess.run([*launch, '-list', str(invalid), '--silent'], capture_output=True, timeout=30)
    assert result.returncode != 0
    zip_path = work / 'baseline.zip'
    with zipfile.ZipFile(zip_path, 'w') as z:
        z.writestr('source.txt', content)
    run('-export', zip_path, 'TXT', work / 'zip-report')
    assert 'source.txt' in (work / 'zip-report' / 'baseline.txt').read_text()
    for suffix, packed in (('bz2', bz2.compress(content)), ('xz', lzma.compress(content))):
        archive = work / ('stream.' + suffix)
        archive.write_bytes(packed)
        assert b'stream' in run('-list', archive)
        run('-test', archive)
        run('-info', archive)
        out = work / ('out-' + suffix)
        run('-extract', archive, out, 'O0')
        assert (out / 'stream').read_bytes() == content
        run('-convert', archive, work / ('converted-' + suffix + '.zip'))
        with zipfile.ZipFile(work / ('converted-' + suffix + '.zip')) as z:
            assert z.read('stream') == content
    for suffix, mode in (('tar.gz', 'w:gz'), ('tar.bz2', 'w:bz2'), ('tar.xz', 'w:xz')):
        archive = work / ('archive.' + suffix)
        with tarfile.open(archive, mode) as t:
            entry = tarfile.TarInfo('nested/entry.txt'); entry.size = len(content)
            t.addfile(entry, io.BytesIO(content))
        assert b'nested/entry.txt' in run('-list', archive)
        run('-test', archive)
        out = work / ('out-' + suffix)
        run('-extract', archive, out, 'O0')
        assert (out / 'nested/entry.txt').read_bytes() == content
    for suffix in ('z', 'tar.z'):
        archive = work / ('created.' + suffix)
        run('-add', archive, '+' + str(work / 'source.txt'))
        assert b'created' in run('-list', archive) if suffix == 'z' else b'source.txt' in run('-list', archive)
        run('-test', archive)
        out = work / ('created-out-' + suffix)
        run('-extract', archive, out, 'O0')
        assert next(out.rglob('*')).read_bytes() == content
    if os.name != 'nt':
        gzip_path = work / 'created.gz'
        run('-add', gzip_path, '+' + str(work / 'source.txt'))
        assert b'created' in run('-list', gzip_path)
        run('-test', gzip_path)
        run('-extract', gzip_path, work / 'gzip-output', 'O0')
        assert (work / 'gzip-output' / 'created').read_bytes() == content
    if os.name == 'nt':
        cab = work / 'sample.cab'
        run('-add', cab, '+' + str(work / 'source.txt'))
        assert b'source.txt' in run('-list', cab)
        run('-info', cab)
        run('-export', cab, 'TXT', work / 'cab-report')
        assert 'source.txt' in (work / 'cab-report' / 'sample.txt').read_text()
