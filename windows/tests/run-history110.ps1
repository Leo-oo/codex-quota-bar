param([string]$OutputDirectory)
$ErrorActionPreference='Stop'
# Portable wrapper. The Python runner enforces frozen hashes, fresh private
# output and an explicit synthetic fixture Main. Python 3 must be installed.
$runner=Join-Path $PSScriptRoot 'run_history110.py'
if($OutputDirectory) {
    $repository=Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
    & python $runner (Join-Path $repository 'audit\baseline.json') $OutputDirectory
} else {
    & python $runner
}
if($LASTEXITCODE -ne 0) { throw 'History regression failed; evidence preserved outside repository.' }
