"""Compile and render only the explicit, unshown, caller-owned history fixture."""
from pathlib import Path
import hashlib
import json
import os
import shutil
import subprocess
import sys
import time

tests = Path(__file__).resolve().parent
repository = tests.parent.parent
source = tests.parent / 'src'
private_root = (repository.parent / 'private-validation' / 'history-layout').resolve()
baseline = Path(sys.argv[1]).resolve() if len(sys.argv) > 1 else repository / 'audit' / 'baseline.json'
output = Path(sys.argv[2]).resolve() if len(sys.argv) > 2 else private_root / 'final'
if output == private_root or private_root not in output.parents or repository.resolve() in output.parents:
    raise RuntimeError('Use a fresh child of sibling private-validation/history-layout')
if os.name != 'nt':
    raise RuntimeError('Windows fixture required')
if output.exists():
    prior_path = output / 'execution.json'
    if not prior_path.is_file():
        raise RuntimeError('Preserve unknown existing artifacts')
    prior = json.loads(prior_path.read_text(encoding='utf-8-sig'))
    if type(prior.get('build_exit')) is not int or prior['build_exit'] == 0 or 'run_exit' in prior:
        raise RuntimeError('Preserve previous fixture execution')
    backup = output / 'compile-default-denied'
    if backup.exists():
        raise RuntimeError('Compile refusal was already preserved')
    backup.mkdir()
    for leaf in ['execution.json', 'compile-output.txt']:
        if (output / leaf).is_file():
            shutil.copy2(output / leaf, backup / leaf)

def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def leaves(mapping):
    result = {}
    for path, value in mapping.items():
        name = path.replace('\\', '/').rsplit('/', 1)[-1]
        if name in result:
            raise RuntimeError('Duplicate baseline leaf')
        result[name] = value.lower()
    return result

fixture = tests / 'HistoryLayout110Probe.cs'
files = sorted(source.glob('*.cs'))
assets = [source / 'assets' / ('quota-tray-' + mode + '.ico') for mode in ['dark', 'light']]
inputs = files + assets + [fixture, Path(__file__)]
hashes = {str(path): digest(path) for path in inputs}
source_hashes = {path.name: digest(path) for path in files}
asset_hashes = {path.name: digest(path) for path in assets}
frozen = json.loads(baseline.read_text(encoding='utf-8-sig'))
if len(files) != 34 or source_hashes != leaves(frozen['sourceHashes']) or asset_hashes != leaves(frozen['assetHashes']):
    raise RuntimeError('Frozen production baseline mismatch; nothing executed')
compiler = Path(os.environ.get('WINDIR') or os.environ['SystemRoot']) / 'Microsoft.NET' / 'Framework64' / 'v4.0.30319' / 'csc.exe'
output.mkdir(parents=True, exist_ok=True)
exe = output / 'HistoryLayout110Probe.exe'
args = [str(compiler), '/nologo', '/codepage:65001', '/target:exe', '/platform:x64', '/main:HistoryLayout110Probe', '/out:' + str(exe)]
args += ['/reference:' + leaf for leaf in ['System.dll', 'System.Core.dll', 'System.Drawing.dll', 'System.Windows.Forms.dll', 'System.Web.Extensions.dll', 'System.Net.Http.dll']]
args += ['/reference:' + str(compiler.parent / 'WPF' / leaf) for leaf in ['UIAutomationClient.dll', 'UIAutomationTypes.dll', 'WindowsBase.dll']]
args += ['/resource:' + str(source / 'assets' / ('quota-tray-' + mode + '.ico')) + ',QuotaBar.Tray.' + mode.title() + '.ico' for mode in ['dark', 'light']]
args += [str(path) for path in files] + [str(fixture)]
names = [theme + ' ' + check for theme in ['light', 'dark'] for check in [
    'longest history is retained', 'preview is unshown without hook or read', 'history slot contains measured text',
    'history and footer fit the frame', 'card glyphs match the independent tall canvas', 'full glyph plane has no bottom or right clipping']]
report = {'sourceSha256': hashes, 'compilerArguments': args, 'baselineSha256': digest(baseline), 'accepted': False}
started = time.monotonic()
try:
    built = subprocess.run(args, cwd=tests, capture_output=True, timeout=45)
    compile_text = built.stdout.decode('utf-8', 'replace') + built.stderr.decode('utf-8', 'replace')
    (output / 'compile-output.txt').write_text(compile_text, encoding='utf-8')
    report.update(build_exit=built.returncode)
    if built.returncode != 0 or hashes != {str(path): digest(path) for path in inputs}:
        raise RuntimeError('Fixture compile failed or inputs changed; nothing run')
    si = subprocess.STARTUPINFO()
    si.dwFlags |= subprocess.STARTF_USESHOWWINDOW
    si.wShowWindow = 0
    run = subprocess.run([str(exe), str(output)], cwd=tests, capture_output=True, timeout=30, startupinfo=si)
    report.update(run_exit=run.returncode, run_stdout=run.stdout.decode('utf-8', 'replace'), run_stderr=run.stderr.decode('utf-8', 'replace'))
    actual_names = [line[5:] for line in report['run_stdout'].splitlines() if line.startswith('PASS ')]
    report['actualPassNames'] = actual_names
    report['expectedPassNames'] = names
    metrics_path = output / 'layout-metrics.json'
    metrics = json.loads(metrics_path.read_text(encoding='utf-8-sig')) if metrics_path.is_file() else {}
    report['metrics'] = metrics
    report['accepted'] = run.returncode == 0 and actual_names == names and '12 history layout checks passed' in report['run_stdout'].splitlines() and metrics.get('pass') == 12
except Exception as error:
    report.update(exceptionType=type(error).__name__, exception=str(error), accepted=False)
finally:
    report['inputsUnchanged'] = hashes == {str(path): digest(path) for path in inputs}
    report['accepted'] = report['accepted'] and report['inputsUnchanged']
    report['elapsedSeconds'] = time.monotonic() - started
    (output / 'execution.json').write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')

metrics = report.get('metrics', {})
images = [{'name': path.name, 'sha256': digest(path)} for path in sorted(output.glob('history-empty-*-125.png'))]
summary = {'schema': 1, 'scope': 'actual unshown control bitmap rendering, not a live screenshot', 'expectedAssertions': 12,
           'pass': metrics.get('pass', len(report.get('actualPassNames', []))), 'fail': 0 if report['accepted'] else 1,
           'accepted': report['accepted'], 'compileExit': report.get('build_exit'), 'runExit': report.get('run_exit'),
           'baselineSha256': digest(baseline), 'sourceCount': len(files), 'sourceHashes': source_hashes, 'assetHashes': asset_hashes,
           'inputsUnchanged': report['inputsUnchanged'], 'fixtureSha256': digest(fixture), 'runnerSha256': digest(Path(__file__)),
           'fixtureExeSha256': digest(exe) if exe.is_file() else None, 'uiScaleParameter': 1.25,
           'actualHdcDpi': [case['actualHdcDpi'] for case in metrics.get('cases', [])], 'systemDpiChanged': False,
           'cases': metrics.get('cases', []), 'images': images, 'ownWindowsShown': False, 'normalProgramMainStarted': False,
           'networkAccessed': False, 'accountFilesRead': False, 'actualRuntimeCacheRead': False, 'userProcessTouched': False,
           'physicalGlobalInputSent': False, 'nativeVisiblePaintAcknowledgementTested': False}
(repository / 'audit' / 'tests-history-layout110-validation.json').write_text(json.dumps(summary, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
print(json.dumps({key: value for key, value in summary.items() if key not in ['sourceHashes', 'assetHashes', 'cases']}, ensure_ascii=True), flush=True)
sys.exit(0 if report['accepted'] else 1)
