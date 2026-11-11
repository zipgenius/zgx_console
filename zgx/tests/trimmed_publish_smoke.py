"""Exercise features that require special handling in a trimmed publish.

Set ZG_TEST_APP to a published executable, and optionally ZG_BASELINE_APP to
an untrimmed executable for a comparison of verbose archive metadata. Set
ZG_TEST_DISK to a disk image to compare its verbose metadata as well.
"""
import os
import pathlib
import platform
import subprocess
import sys
import tempfile
import zipfile

app = pathlib.Path(os.environ['ZG_TEST_APP']).resolve()
baseline = os.environ.get('ZG_BASELINE_APP')


def run(executable, *args):
    result = subprocess.run([str(executable), *map(str, args), '--silent'],
                            capture_output=True, timeout=120)
    assert result.returncode == 0, (args, result.returncode, result.stdout, result.stderr)
    return result.stdout.decode('utf-8', 'replace')


with tempfile.TemporaryDirectory(prefix='zg-trimming-smoke-') as folder:
    work = pathlib.Path(folder)
    archive = work/'sample.zip'
    with zipfile.ZipFile(archive, 'w') as z:
        z.writestr('folder/example.txt', b'trimmed SFX test')

    info = run(app, '-info', archive, '--verbose')
    for field in ('TotalSize', 'CompressionType', 'LastAccessedTime'):
        assert field in info, (field, info)
    if baseline:
        reference = run(pathlib.Path(baseline).resolve(), '-info', archive, '--verbose')
        assert info == reference, 'Verbose archive metadata differs from the untrimmed build'
        if os.environ.get('ZG_TEST_DISK'):
            disk = pathlib.Path(os.environ['ZG_TEST_DISK']).resolve()
            assert run(app, '-info', disk, '--verbose') == run(
                pathlib.Path(baseline).resolve(), '-info', disk, '--verbose'
            ), 'Verbose disk metadata differs from the untrimmed build'

    if os.name == 'nt' or sys.platform.startswith('linux'):
        windows = os.name == 'nt'
        executable = work/('sample.exe' if windows else 'sample.run')
        arch = 'arm64' if platform.machine().lower() in ('arm64', 'aarch64') else 'x64'
        target = ('win-' if windows else 'linux-')+arch
        run(app, '-sfx', archive, executable, '--ui=terminal', '--target='+target)
        if not windows:
            executable.chmod(0o755)
        destination = work/'extracted'
        result = subprocess.run([str(executable), '--yes', '--destination='+str(destination), '--no-open'],
                                capture_output=True, timeout=120)
        assert result.returncode == 0, (result.returncode, result.stdout, result.stderr)
        assert (destination/'folder'/'example.txt').read_bytes() == b'trimmed SFX test'

print('PASS: verbose archive metadata and terminal SFX')
