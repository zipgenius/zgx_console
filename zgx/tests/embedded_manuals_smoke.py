"""Exercise every help route without external manuals, including culture fallback."""
import os
from pathlib import Path
import re
import shutil
import subprocess
import tempfile

root = Path(__file__).resolve().parents[1]
app = Path(os.environ['ZG_MANUAL_APP']).resolve()
sandbox = tempfile.TemporaryDirectory(prefix='zg-embedded-manuals-')
assert Path(sandbox.name).resolve().parent == Path(tempfile.gettempdir()).resolve()
isolated = Path(sandbox.name) / 'bundle'
shutil.copytree(app.parent, isolated, ignore=shutil.ignore_patterns('man', '*.pdb'))
app = isolated / app.name
command = ['dotnet', str(app)] if app.suffix == '.dll' else [str(app)]
pages = ('add', 'extract', 'backup', 'convert', 'list', 'export', 'sfx', 'info',
         'test', 'exitcodes', 'dumpdisc', 'hexview', 'hash', 'optim')


def render(text):
    lines = []
    for line in text.splitlines():
        if line == '>>':
            continue
        if line and line[0] in '^*[]%':
            line = line.lstrip(line[0])
        lines.append(line)
    return '\n'.join(lines).rstrip('\n')


def run(args, culture=None):
    environment = dict(os.environ)
    if culture:
        environment.update(LANG=culture, LC_ALL=culture)
    result = subprocess.run([*command, *args], capture_output=True, env=environment, timeout=30)
    assert result.returncode == 0, (args, culture, result.returncode, result.stderr)
    text = result.stdout.decode('utf-8-sig').replace('\r\n', '\n')
    text = re.sub(r'\x1b\[[0-9;]*m', '', text)
    assert not any(line.startswith('>>') for line in text.splitlines()), (args, text)
    return text.rstrip('\n')


assert not (app.parent / 'man').exists(), 'Use a fresh publish without external manual files'
# Deliberately place an external add.txt next to the app: it must never override embedded help.
trap = app.parent / 'man'
trap.mkdir()
fake = trap / 'add.txt'
fake.write_text('EXTERNAL_MANUAL_MUST_NOT_BE_LOADED', encoding='utf-8')
try:
    if os.name == 'nt':
        language = 'it-IT' if run(['-help', '-hexview', '--silent']).startswith('Sintassi:') else 'en'
        cultures = [(None, language)]
    else:
        cultures = [('en_US.UTF-8', 'en'), ('it_IT.UTF-8', 'it-IT'),
                    ('it_CH.UTF-8', 'it-IT'), ('it', 'it-IT'), ('fr_FR.UTF-8', 'en')]
    for culture, language in cultures:
        directory = root / 'man' / (language if language != 'en' else '')
        for page in pages:
            filename = 'exitcode' if page == 'exitcodes' else page
            expected = render((directory / f'{filename}.txt').read_text(encoding='utf-8-sig'))
            actual = run(['-help', '-' + page, '--silent'], culture)
            assert actual == expected, (page, culture, actual, expected)
        startup = render((directory / 'startup.txt').read_text(encoding='utf-8-sig'))
        assert run(['-help', '--silent'], culture) == startup
        initial = run([], culture).split('\n', 1)
        assert re.fullmatch(r'ZipGenius X Console - \d+\.\d+\.\d+\.\d+', initial[0])
        assert initial[1] == startup
        print(f'PASS: 14 help pages and startup from embedded resources; culture={culture or language}')
finally:
    fake.unlink()
    trap.rmdir()
    sandbox.cleanup()
print('PASS: external manuals ignored; no file dependency; language and English fallback verified')
