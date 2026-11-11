"""Integrity command integration tests. No dangerous payload is decompressed.
Set ZG_TEST_APP to test a different build. Supports Windows and Unix hosts.
"""
import gzip
import base64
import hashlib
import io
import os
import pathlib
import struct
import subprocess
import tarfile
import tempfile
import zipfile

ROOT = pathlib.Path(__file__).resolve().parents[1]
APP = pathlib.Path(os.environ.get('ZG_TEST_APP', str(ROOT/'bin/Debug/net10.0/zg.dll'))).resolve()


def run(code, *args):
    launch = [str(APP)] if APP.suffix.lower() == '.exe' else ['dotnet', str(APP)]
    result = subprocess.run([*launch, *map(str, args), '--silent'], cwd=work,
                            capture_output=True, timeout=40)
    assert result.returncode == code, (args, result.returncode, result.stdout, result.stderr)
    return result


with tempfile.TemporaryDirectory(prefix='zg-test-command-') as folder:
    work = pathlib.Path(folder)
    archive = work/'valid.zip'
    with zipfile.ZipFile(archive, 'w', compression=zipfile.ZIP_STORED) as z:
        z.writestr('folder/file.txt', 'verified content')
        z.writestr('empty/', '')
    original = archive.read_bytes()
    run(0, '-test', archive)
    run(0, '-t', archive)
    corrupt = work/'bad-crc.zip'
    damaged = bytearray(original)
    name_length, extra_length = struct.unpack_from('<HH', damaged, 26)
    damaged[30+name_length+extra_length] ^= 1
    corrupt.write_bytes(damaged)
    run(6, '-test', corrupt)
    bad_header = work/'bad-header.zip'
    damaged = bytearray(original)
    damaged[30] ^= 1
    bad_header.write_bytes(damaged)
    run(6, '-test', bad_header)
    truncated = work/'truncated.zip'
    truncated.write_bytes(original[:-15])
    run(43, '-test', truncated)
    empty = work/'empty.zip'
    with zipfile.ZipFile(empty, 'w'): pass
    run(0, '-test', empty)
    run(3, '-test', work/'missing.zip')
    run(99, '-test')
    run(99, '-test', archive, '--invalid-option')
    plain = work/'plain.txt'
    plain.write_text('ordinary file')
    run(7, '-test', plain)
    sqx = work/'unsupported.sqx'
    sqx.write_bytes(b'placeholder')
    run(7, '-test', sqx)

    compressed = work/'valid.gz'
    compressed.write_bytes(gzip.compress(b'gzip content'*100))
    run(0, '-test', compressed)
    bad_gzip = work/'bad.gz'
    damaged = bytearray(compressed.read_bytes())
    damaged[-8] ^= 1
    bad_gzip.write_bytes(damaged)
    run(6, '-test', bad_gzip)
    tar = work/'valid.tar'
    with tarfile.open(tar, 'w') as t:
        info = tarfile.TarInfo('file.txt'); info.size = 12
        t.addfile(info, io.BytesIO(b'tar contents'))
    run(0, '-test', tar)
    bad_tar_header = work/'bad-header.tar'
    damaged = bytearray(tar.read_bytes()); damaged[0] ^= 1
    bad_tar_header.write_bytes(damaged)
    run(6, '-test', bad_tar_header)
    short_tar = work/'short.tar'
    short_tar.write_bytes(tar.read_bytes()[:520])
    run(6, '-test', short_tar)
    tgz = work/'valid.tar.gz'
    tgz.write_bytes(gzip.compress(tar.read_bytes()))
    run(0, '-test', tgz)

    # Use the production writer for encrypted ZIP and 7Z fixtures.
    protected = work/'protected.zip'
    run(0, '-add', protected, '+'+str(plain), 'Psecret', 'Q3')
    run(8, '-test', protected)
    run(8, '-test', protected, 'Pwrong')
    run(0, '-test', protected, 'Psecret')
    run(0, '-test', protected, '--password=secret')
    for algorithm in ('Q1', 'Q2'):
        compatible = work/('protected-'+algorithm+'.zip')
        run(0, '-add', compatible, '+'+str(plain), 'Psecret', algorithm)
        run(0, '-test', compatible, 'Psecret')
        run(8, '-test', compatible, 'Pwrong')
    # Small real 7Z fixtures, also usable on Unix without an external 7-Zip tool.
    solid = work/'solid-fixture.7z'
    solid.write_bytes(base64.b64decode('N3q8ryccAAQ8tRFTdAAAAAAAAAAgAAAAAAAAAAnTuFUBABFzb2xpZCBvbmVzb2xpZCB0d28AAACBMweuD89dLwwHyEN/QbH6/eXHfeltPPYguzL7BEvdvF2m7CKnOVg15PK1cZs+0xVUSI0kG09wg/260MPFFGyhYaaftusGxd0yb0oFhmGwY1xpIS5IweqHeDL1ZRcGFgEJXgAHCwEAASMDAQEFXQAQAAAMdgoBekXMmwAA'))
    run(0, '-test', solid)
    bad_solid = work/'bad-solid.7z'
    damaged = bytearray(solid.read_bytes()); damaged[40] ^= 1
    bad_solid.write_bytes(damaged)
    run(6, '-test', bad_solid)
    locked_fixture = work/'locked-fixture.7z'
    locked_fixture.write_bytes(base64.b64decode('N3q8ryccAAQ2tKG/oAAAAAAAAAA9AAAAAAAAAJoXCKd8baa5zPKdwLRtcIJ+/y+3VFNpn6jmglrLuvayAqGKxw0zplYXW10FGPAk3y8hMfhlSsbPCge6GcvBudh69i4DBI0UpfcCiDFGofP5Jz8eu/k1iij5hdEqiMFtf0R2KD8Xi6+6bBq67b/JfKTbavHvAmo3QSP+WvpGOC/8aLk93i8Z5P7DmikHmwADHivVEM09l18kJfMX6zYxaB+SuEc+FwYgAQmAgAAHCwEAAiQG8QcBElMPAtE+WPXBdmCH1g30FQ/PviMDAQEFXQAQAAABAAx6gJYKAS2VdYMAAA=='))
    run(8, '-test', locked_fixture)
    run(8, '-test', locked_fixture, 'Pwrong')
    run(0, '-test', locked_fixture, 'Psecret')
    if os.name == 'nt':
        seven = work/'valid.7z'
        run(0, '-add', seven, '+'+str(plain))
        run(0, '-test', seven)
        cab = work/'valid.cab'
        run(0, '-add', cab, '+'+str(plain))
        run(0, '-test', cab)
        engine = ROOT/'Libs/64bit/7z.exe'
        locked = work/'locked.7z'
        result = subprocess.run([str(engine), 'a', str(locked), str(plain), '-psecret', '-mhe=on'], capture_output=True, timeout=30)
        assert result.returncode == 0, result.stdout
        run(8, '-test', locked)
        run(8, '-test', locked, 'Pwrong')
        run(0, '-test', locked, 'Psecret')

    # Warnings allow checking, but never turn unreadable data into a successful test.
    warning = work/'warning.zip'
    with zipfile.ZipFile(warning, 'w') as z:
        for i in range(4): z.writestr(str(i), bytes([65+i]))
    data = bytearray(warning.read_bytes()); offset = data.index(b'PK\x01\x02')
    for _ in range(4):
        struct.pack_into('<I', data, offset+20, 2*1024**2)
        struct.pack_into('<I', data, offset+24, 1024**3)
        a,b,c = struct.unpack_from('<HHH',data,offset+28); offset+=46+a+b+c
    warning.write_bytes(data)
    run(6, '-test', warning)

    bomb = pathlib.Path.home()/'Desktop/42.zip'
    if bomb.exists(): run(42, '-test', bomb, '--yes')
    assert archive.read_bytes() == original
    before = {p.name: hashlib.sha256(p.read_bytes()).digest() for p in work.iterdir() if p.is_file()}
    run(0, '-test', archive)
    assert before == {p.name: hashlib.sha256(p.read_bytes()).digest() for p in work.iterdir() if p.is_file()}
    assert not any(p.is_dir() for p in work.iterdir()), 'Test unexpectedly extracted files'

print('PASS -test: ZIP headers/CRC, empty archives, gzip/TAR, password handling, unsupported inputs, safety and source preservation')
