"""Linux WIM creation/update regression. Requires libwim and wimlib-imagex."""
import os
from pathlib import Path
import subprocess
import tempfile
import zipfile

root = Path(__file__).resolve().parents[1]
app = Path(os.environ.get('ZG_TEST_APP', root / 'bin/Debug/net10.0/zg.dll')).resolve()
imagex = os.environ.get('ZG_WIMLIB_IMAGEX', 'wimlib-imagex')
launch = ['dotnet', str(app)] if app.suffix == '.dll' else [str(app)]

def run(*args, ok=True):
    result = subprocess.run([*launch, *map(str, args), '--silent'], capture_output=True, timeout=120)
    assert (result.returncode == 0) == ok, (args, result.returncode, result.stdout, result.stderr)
    return result

def inspect(wim, dest):
    p = subprocess.run([imagex, 'apply', str(wim), '1', str(dest)], capture_output=True, timeout=120)
    assert p.returncode == 0, (p.returncode, p.stdout, p.stderr)

with tempfile.TemporaryDirectory(prefix='zg-wim-test-') as temp:
    work = Path(temp)
    source = work / 'source'
    (source / 'nested').mkdir(parents=True)
    (source / 'nested' / 'caffè.txt').write_text('original', encoding='utf-8')
    (source / 'other.txt').write_text('second', encoding='utf-8')
    wim = work / 'archive.wim'
    run('-add', wim, '+' + str(source), 'R1')
    result = run('-list', wim)
    assert 'caffè.txt'.encode() in result.stdout, result.stdout
    run('-info', wim)
    run('-test', wim)
    damaged = work / 'damaged.wim'
    damaged.write_bytes(wim.read_bytes()[:64])
    run('-test', damaged, ok=False)
    run('-extract', wim, work / 'zg-extracted', 'O0')
    assert (work / 'zg-extracted/source/nested/caffè.txt').read_text(encoding='utf-8') == 'original'
    run('-extract', wim, work / 'filtered', 'O0', '+source/other.txt')
    assert (work / 'filtered/source/other.txt').read_text(encoding='utf-8') == 'second'
    assert not (work / 'filtered/source/nested/caffè.txt').exists()
    run('-export', wim, 'TXT', work / 'reports')
    assert 'caffè.txt' in (work / 'reports' / 'archive.txt').read_text(encoding='utf-8')
    inspect(wim, work / 'initial')
    assert (work / 'initial/source/nested/caffè.txt').read_text(encoding='utf-8') == 'original'
    # AA retains an existing path while adding new files.
    (source / 'nested/caffè.txt').write_text('changed', encoding='utf-8')
    (source / 'new.txt').write_text('new', encoding='utf-8')
    run('-add', wim, '+' + str(source), 'R1', 'AA')
    inspect(wim, work / 'after-add')
    assert (work / 'after-add/source/nested/caffè.txt').read_text(encoding='utf-8') == 'original'
    assert (work / 'after-add/source/new.txt').read_text(encoding='utf-8') == 'new'
    run('-add', wim, '+' + str(source), 'R1', 'AU')
    inspect(wim, work / 'after-update')
    assert (work / 'after-update/source/nested/caffè.txt').read_text(encoding='utf-8') == 'changed'
    before = wim.read_bytes()
    run('-add', wim, '+' + str(source), 'R1', 'Psecret', ok=False)
    assert wim.read_bytes() == before
    run('-add', wim, '+' + str(source), 'R1', 'V720', ok=False)
    assert wim.read_bytes() == before
    # Reject reparse points before extraction, even if a filter excludes them.
    links = work / 'links'
    links.mkdir()
    (links / 'link').symlink_to('/tmp')
    link_wim = work / 'link.wim'
    result = subprocess.run([imagex, 'capture', str(links), str(link_wim), 'Links'], capture_output=True, timeout=120)
    assert result.returncode == 0, (result.stdout, result.stderr)
    run('-extract', link_wim, work / 'unsafe-output', 'O0', ok=False)
    assert not list((work / 'unsafe-output').iterdir())
    # A multi-image WIM is left unchanged rather than updating an ambiguous image.
    multi = work / 'multi.wim'
    multi.write_bytes(before)
    result = subprocess.run([imagex, 'append', str(source), str(multi), 'Second'], capture_output=True, timeout=120)
    assert result.returncode == 0, (result.stdout, result.stderr)
    multi_before = multi.read_bytes()
    run('-add', multi, '+' + str(source), 'R1', 'AU', ok=False)
    assert multi.read_bytes() == multi_before
    with zipfile.ZipFile(work / 'input.zip', 'w') as z:
        z.writestr('test.txt', 'converted')
    run('-convert', work / 'input.zip', work / 'converted.wim')
    inspect(work / 'converted.wim', work / 'converted')
    assert (work / 'converted/test.txt').read_text() == 'converted'
    run('-convert', work / 'converted.wim', work / 'roundtrip.zip')
    with zipfile.ZipFile(work / 'roundtrip.zip') as z:
        assert z.read('test.txt') == b'converted'
    assert not list(work.glob('.zg-wim-*.tmp'))
print('PASS: WIM creation, metadata, independent extraction, update, rejection and conversion')
