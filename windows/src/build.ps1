param([string]$OutputDirectory = '')
$ErrorActionPreference = 'Stop'
$miniFramework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$miniCompiler = Join-Path $miniFramework 'csc.exe'
$miniProject = Split-Path -Parent $MyInvocation.MyCommand.Path
if (-not (Test-Path -LiteralPath $miniCompiler -PathType Leaf)) { throw 'The x64 .NET Framework compiler was not found. Install the full .NET Framework developer/runtime components.' }
if (-not $OutputDirectory) { $OutputDirectory = Join-Path (Split-Path -Parent $miniProject) 'build' }
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$miniOutput = Join-Path $OutputDirectory 'CodexUsageMini.exe'
$miniSources = Get-ChildItem -LiteralPath $miniProject -Filter '*.cs' | ForEach-Object FullName
& $miniCompiler /nologo /codepage:65001 /optimize+ /target:winexe /platform:x64 /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Web.Extensions.dll "/reference:$miniFramework\WPF\UIAutomationClient.dll" "/reference:$miniFramework\WPF\UIAutomationTypes.dll" "/reference:$miniFramework\WPF\WindowsBase.dll" "/resource:$miniProject\assets\quota-tray-dark.ico,QuotaBar.Tray.Dark.ico" "/resource:$miniProject\assets\quota-tray-light.ico,QuotaBar.Tray.Light.ico" "/win32icon:$miniProject\assets\quota-app.ico" "/out:$miniOutput" $miniSources
if ($LASTEXITCODE -ne 0) { throw "Build failed: $LASTEXITCODE" }
Copy-Item -LiteralPath (Join-Path $miniProject 'App.config') -Destination ($miniOutput + '.config') -Force
Write-Output $miniOutput
