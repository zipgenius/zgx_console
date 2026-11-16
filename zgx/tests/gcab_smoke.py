"""Linux CAB regression tests; requires libgcab and the independent gcab CLI."""
import os
from pathlib import Path
import subprocess
import tempfile
import zipfile

root = Path(__file__).resolve().parents[1]
app = Path(os.environ.get('ZG_TEST_APP', root / 'bin/Debug/net10.0/zg.dll')).resolve()
gcab = os.environ.get('ZG_GCAB', 'gcab')
launch = ['dotnet', str(app)] if app.suffix == '.dll' else [str(app)]

def run(*args, ok=True):
    p = subprocess.run([*launch, *map(str, args), '--silent'], capture_output=True, timeout=120)
    assert (p.returncode == 0) == ok, (args, p.returncode, p.stdout, p.stderr)
    return p

def inspect(cab, target):
    target.mkdir()
    p = subprocess.run([gcab, '-x', str(cab)], cwd=target, capture_output=True, timeout=120)
    assert p.returncode == 0, (p.returncode, p.stdout, p.stderr)

with tempfile.TemporaryDirectory(prefix='zg-gcab-test-') as temp:
    work = Path(temp)
    source = work / 'source'
    (source / 'nested').mkdir(parents=True)
    (source / 'nested/caffè.txt').write_text('original', encoding='utf-8')
    (source / 'other.txt').write_text('second', encoding='utf-8')
    for level in ('C0', 'C5'):
        cab = work / (level + '.cab')
        run('-add', cab, '+' + str(source), 'R1', level)
        assert 'caffè.txt'.encode() in run('-list', cab).stdout
        run('-info', cab)
        run('-test', cab)
        run('-export', cab, 'TXT', work / ('report-' + level))
        assert 'caffè.txt' in (work / ('report-' + level) / (level + '.txt')).read_text(encoding='utf-8')
        inspect(cab, work / ('independent-' + level))
        assert (work / ('independent-' + level) / 'source/nested/caffè.txt').read_text(encoding='utf-8') == 'original'
        run('-extract', cab, work / ('extract-' + level), 'O0')
        assert (work / ('extract-' + level) / 'source/nested/caffè.txt').read_text(encoding='utf-8') == 'original'
        run('-extract', cab, work / ('filter-' + level), 'O0', '+source/other.txt')
        assert (work / ('filter-' + level) / 'source/other.txt').read_text() == 'second'
        assert not (work / ('filter-' + level) / 'source/nested/caffè.txt').exists()
        run('-extract', cab, work / ('flat-' + level), 'O0', 'F0')
        assert (work / ('flat-' + level) / 'caffè.txt').read_text(encoding='utf-8') == 'original'
    cab = work / 'C5.cab'
    (source / 'nested/caffè.txt').write_text('changed', encoding='utf-8')
    (source / 'new.txt').write_text('new', encoding='utf-8')
    run('-add', cab, '+' + str(source), 'R1', 'AA')
    inspect(cab, work / 'after-add')
    assert (work / 'after-add/source/nested/caffè.txt').read_text(encoding='utf-8') == 'original'
    assert (work / 'after-add/source/new.txt').read_text() == 'new'
    run('-add', cab, '+' + str(source), 'R1', 'AU')
    inspect(cab, work / 'after-update')
    assert (work / 'after-update/source/nested/caffè.txt').read_text(encoding='utf-8') == 'changed'
    before = cab.read_bytes()
    run('-add', cab, '+' + str(source), 'R1', 'Psecret', ok=False)
    run('-add', cab, '+' + str(source), 'R1', 'V720', ok=False)
    assert cab.read_bytes() == before
    damaged = work / 'damaged.cab'
    damaged.write_bytes(before[:40])
    run('-test', damaged, ok=False)
    damaged_before = damaged.read_bytes()
    run('-add', damaged, '+' + str(source), 'R1', 'AU', ok=False)
    assert damaged.read_bytes() == damaged_before
    split = work / 'split.cab'
    split_data = bytearray(before)
    split_data[30] |= 1
    split.write_bytes(split_data)
    run('-list', split, ok=False)
    run('-extract', split, work / 'split-output', 'O0', ok=False)
    small = work / 'plain.cab'
    (work / 'a.txt').write_text('safe')
    run('-add', small, '+' + str(work / 'a.txt'), 'C0')
    attack = small.read_bytes()
    assert attack.count(b'a.txt\0') == 1
    corrupted = work / 'bad-data.cab'
    mutated = bytearray(attack)
    mutated[-1] ^= 1
    corrupted.write_bytes(mutated)
    run('-test', corrupted, ok=False)
    unsafe = work / 'unsafe.cab'
    unsafe.write_bytes(attack.replace(b'a.txt\0', b'../xy\0', 1))
    run('-extract', unsafe, work / 'unsafe-output', 'O0', ok=False)
    assert not (work / 'xy').exists()
    with zipfile.ZipFile(work / 'input.zip', 'w') as z:
        z.writestr('test.txt', 'converted')
    run('-convert', work / 'input.zip', work / 'converted.cab')
    inspect(work / 'converted.cab', work / 'converted')
    assert (work / 'converted/test.txt').read_text() == 'converted'
    run('-convert', work / 'converted.cab', work / 'roundtrip.zip')
    with zipfile.ZipFile(work / 'roundtrip.zip') as z:
        assert z.read('test.txt') == b'converted'
    assert not list(work.glob('.zg-cab-*.tmp'))
print('PASS: CAB creation, independent extraction, read, test, update, rejection and conversion')
