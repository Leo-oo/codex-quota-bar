"""Build and run the explicit, non-activating own-window card fixture only.

Production Program.Main, network clients and real account/runtime files are not
used. Generated binaries, cache, HWND diagnostics and raw results remain in the
sibling private-validation/tests tree; audit receives only an allowlisted summary.
"""
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
private_tests = (repository.parent / 'private-validation' / 'tests').resolve()
baseline = Path(sys.argv[1]).resolve() if len(sys.argv) > 1 else repository / 'audit' / 'baseline.json'
output = Path(sys.argv[2]).resolve() if len(sys.argv) > 2 else private_tests / 'card-freeze'
output = output.resolve()
if output == private_tests or private_tests not in output.parents or repository.resolve() in output.parents or repository.resolve() in private_tests.parents:
    raise RuntimeError('Use a fresh suite folder under sibling private-validation/tests, outside the repository')
if os.name != 'nt':
    raise RuntimeError('This native fixture requires Windows')
process_file = output / 'execution.json'
if output.exists():
    if not process_file.is_file():
        raise RuntimeError('Fresh artifact folder required; unknown existing evidence is preserved')
    previous = json.loads(process_file.read_text(encoding='utf-8-sig'))
    if type(previous.get('build_exit')) is not int or previous['build_exit'] == 0 or 'run_exit' in previous or (output / 'result.json').exists() or (output / 'own-fixture-runtime').exists():
        raise RuntimeError('Existing fixture runtime or execution evidence is preserved')
    backup = output / 'compile-default-denied'
    if backup.exists():
        raise RuntimeError('Previous compile refusal is already preserved; execution stopped')
    backup.mkdir()
    for name in ['execution.json', 'compile-output.txt']:
        if (output / name).is_file():
            shutil.copy2(output / name, backup / name)

def digest(file):
    return hashlib.sha256(file.read_bytes()).hexdigest()

def names_map(values):
    mapped = {}
    for name, value in values.items():
        leaf = str(name).replace('\\', '/').rsplit('/', 1)[-1]
        if leaf in mapped:
            raise RuntimeError('Duplicate baseline input name')
        mapped[leaf] = str(value).lower()
    return mapped

fixture = tests / 'CardFreeze118Probe.cs'
files = sorted(source.glob('*.cs'))
assets = [source / 'assets' / ('quota-tray-' + mode + '.ico') for mode in ['dark', 'light']]
inputs = files + assets + [fixture, Path(__file__)]
hashes = {str(file): digest(file) for file in inputs}
source_hashes = {file.name: hashes[str(file)] for file in files}
asset_hashes = {file.name: hashes[str(file)] for file in assets}
build_map = json.loads(baseline.read_text(encoding='utf-8-sig'))
if len(files) != 34 or source_hashes != names_map(build_map['sourceHashes']):
    raise RuntimeError('Production sources differ from the frozen 34-source baseline; no GUI started')
if len(assets) != 2 or asset_hashes != names_map(build_map['assetHashes']):
    raise RuntimeError('Resources differ from the frozen 2-asset baseline; no GUI started')
windows_root = os.environ.get('WINDIR') or os.environ.get('SystemRoot')
if not windows_root:
    raise RuntimeError('Windows compiler requires WINDIR or SystemRoot')
compiler = Path(windows_root) / 'Microsoft.NET' / 'Framework64' / 'v4.0.30319' / 'csc.exe'
output.mkdir(parents=True, exist_ok=True)
exe = output / 'CardFreeze118Probe.exe'
args = [str(compiler), '/nologo', '/codepage:65001', '/target:winexe', '/platform:x64',
        '/main:CardFreeze118Probe', '/out:' + str(exe)]
args += ['/reference:' + name for name in ['System.dll', 'System.Core.dll', 'System.Drawing.dll',
         'System.Windows.Forms.dll', 'System.Web.Extensions.dll', 'System.Net.Http.dll']]
args += ['/reference:' + str(compiler.parent / 'WPF' / name) for name in
         ['UIAutomationClient.dll', 'UIAutomationTypes.dll', 'WindowsBase.dll']]
args += ['/resource:' + str(source / 'assets' / ('quota-tray-' + mode + '.ico')) +
         ',QuotaBar.Tray.' + mode.title() + '.ico' for mode in ['dark', 'light']]
args += [str(file) for file in files] + [str(fixture)]
report = {'source_sha256': hashes, 'source_count': len(files), 'compiler_arguments': args,
          'baseline_matches': True, 'baseline_sha256': digest(baseline),
          'fixture_source_sha256': hashes[str(fixture)], 'runner_sha256': hashes[str(Path(__file__))],
          'normalProgramMainStarted': False, 'networkAccessed': False, 'accountFilesRead': False,
          'actualRuntimeCacheRead': False, 'userProcessTouched': False, 'physicalGlobalInputSent': False,
          'environmentInheritedWithoutModification': True}
started = time.monotonic()
try:
    built = subprocess.run(args, cwd=tests, capture_output=True, timeout=45)
    text = built.stdout.decode('utf-8', 'replace') + built.stderr.decode('utf-8', 'replace')
    (output / 'compile-output.txt').write_text(text, encoding='utf-8')
    report.update(build_exit=built.returncode, build_stdout=built.stdout.decode('utf-8', 'replace'),
                  build_stderr=built.stderr.decode('utf-8', 'replace'))
    report['inputs_unchanged_after_build'] = hashes == {str(file): digest(file) for file in inputs}
    if built.returncode != 0 or not report['inputs_unchanged_after_build'] or not exe.is_file():
        raise RuntimeError('Fresh fixture build failed; no GUI started')
    report['fixture_exe_sha256'] = digest(exe)
    si = subprocess.STARTUPINFO()
    si.dwFlags |= subprocess.STARTF_USESHOWWINDOW
    si.wShowWindow = 0
    run = subprocess.run([str(exe), str(output)], cwd=tests, capture_output=True, timeout=25, startupinfo=si)
    report.update(run_exit=run.returncode, run_stdout=run.stdout.decode('utf-8', 'replace'),
                  run_stderr=run.stderr.decode('utf-8', 'replace'), probeExited=True)
    report['inputs_unchanged_during_run'] = hashes == {str(file): digest(file) for file in inputs}
    result_file = output / 'result.json'
    if not result_file.is_file():
        raise RuntimeError('Fresh result missing; early process exit cannot pass')
    result = json.loads(result_file.read_text(encoding='utf-8-sig'))
    report['fixture_result'] = result
    required = ['empty-open', 'empty-arrival', 'reopen-v1', 'same-id-update', 'reopen-v2',
                'checked-only', 'status-clock-crossing', 'reopen-clock-only']
    report['required_cases_exactly_completed'] = result.get('cases') == required
    report['accepted'] = run.returncode == 0 and result.get('completed') is True and result.get('fail') == 0 and \
        result.get('pass') == 46 and report['required_cases_exactly_completed'] and report['inputs_unchanged_during_run']
except Exception as error:
    report.update(accepted=False, exception_type=type(error).__name__, exception=str(error))
finally:
    report['elapsed_seconds'] = time.monotonic() - started
    report['final_inputs_unchanged'] = hashes == {str(file): digest(file) for file in inputs}
    report['accepted'] = bool(report.get('accepted') and report['final_inputs_unchanged'])
    (output / 'execution.json').write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')

# Only fixed keys/scalars/hashes are published; no private result object, compiler
# argument, cache path, rectangle, foreground handle or HWND enters this document.
published = repository / 'audit' / 'tests-validation.json'
previous_summary = json.loads(published.read_text(encoding='utf-8-sig')) if published.is_file() else {}
summary = {'schema': 1, 'baselineSha256': digest(baseline), 'sourceCount': len(files),
           'sourceHashes': source_hashes, 'assetHashes': asset_hashes, 'suites': {}}
number_fields = ['pass', 'fail', 'compileExit', 'runExit']
boolean_fields = ['accepted', 'sourceMatchesBaseline', 'assetsMatchBaseline', 'sourceUnchanged', 'resourcesUnchanged', 'fixtureUnchanged',
                  'runnerUnchanged', 'inputsUnchanged', 'probeExited', 'requiredCasesExactlyCompleted', 'normalProgramMainStarted',
                  'networkAccessed', 'accountFilesRead', 'actualRuntimeCacheRead', 'userProcessTouched', 'physicalGlobalInputSent',
                  'nativeAcknowledgementTested', 'realVisibleNotificationTested', 'nativeVisiblePaintAcknowledgementRequired']
hash_fields = ['fixtureSha256', 'runnerSha256', 'fixtureExeSha256']
for name in ['newsUnread', 'cardFreeze']:
    old = previous_summary.get('suites', {}).get(name) if previous_summary.get('baselineSha256') == summary['baselineSha256'] else None
    if not isinstance(old, dict):
        continue
    clean = {'scope': 'synthetic own-file and unshown bitmap/layout fixture' if name == 'newsUnread' else
             'non-activating own-window native paint and acknowledgement fixture', 'expectedAssertions': 79 if name == 'newsUnread' else 46}
    for key in number_fields:
        if key in old and (old[key] is None or type(old[key]) is int):
            clean[key] = old[key]
    for key in boolean_fields:
        if type(old.get(key)) is bool:
            clean[key] = old[key]
    for key in hash_fields:
        value = old.get(key)
        if key in old and (value is None or isinstance(value, str) and len(value) == 64 and all(char in '0123456789abcdefABCDEF' for char in value)):
            clean[key] = value
    summary['suites'][name] = clean
result = report.get('fixture_result', {})
suite = {'scope': 'non-activating own-window native paint and acknowledgement fixture', 'expectedAssertions': 46,
         'pass': result.get('pass'), 'fail': result.get('fail'), 'compileExit': report.get('build_exit'),
         'runExit': report.get('run_exit'), 'accepted': report['accepted'], 'sourceMatchesBaseline': True,
         'assetsMatchBaseline': True, 'inputsUnchanged': report['final_inputs_unchanged'],
         'fixtureSha256': report['fixture_source_sha256'], 'runnerSha256': report['runner_sha256'],
         'fixtureExeSha256': report.get('fixture_exe_sha256'), 'probeExited': report.get('probeExited') is True,
         'requiredCasesExactlyCompleted': report.get('required_cases_exactly_completed') is True,
         'normalProgramMainStarted': False, 'networkAccessed': False, 'accountFilesRead': False,
         'actualRuntimeCacheRead': False, 'userProcessTouched': False, 'physicalGlobalInputSent': False,
         'nativeVisiblePaintAcknowledgementRequired': True}
summary.setdefault('suites', {})['cardFreeze'] = suite
published.write_text(json.dumps(summary, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
print(json.dumps(suite, ensure_ascii=True), flush=True)
sys.exit(0 if report['accepted'] else 1)

