# Builds dist\Tolmacz-Setup.exe. Run on Windows with the .NET 10 SDK, the MAUI
# workload (dotnet workload install maui-windows) and Inno Setup 7.
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'src\Tolmacz.App\Tolmacz.App.csproj'
$publish = Join-Path $root 'dist\windows\Tolmacz'

if (Test-Path $publish) { Remove-Item $publish -Recurse -Force }

dotnet publish $project -f net10.0-windows10.0.19041.0 -c Release -r win-x64 `
    --self-contained true -p:WindowsPackageType=None -o $publish
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed.' }

$compiler = @(
    "$env:ProgramFiles\Inno Setup 7\ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 7\ISCC.exe",
    "$env:LOCALAPPDATA\Programs\Inno Setup 7\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $compiler) { throw 'Inno Setup was not found. Install version 7 from https://jrsoftware.org/isdl.php' }

& $compiler (Join-Path $root 'installer\Tolmacz.iss')
if ($LASTEXITCODE -ne 0) { throw 'Inno Setup failed.' }
