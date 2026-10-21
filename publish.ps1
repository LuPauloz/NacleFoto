<#
    Builds the Nacle Foto folder that gets copied to the parents' PC.
    Usage:  powershell -ExecutionPolicy Bypass -File .\publish.ps1
            powershell -ExecutionPolicy Bypass -File .\publish.ps1 -Zip
#>
param([switch]$Zip)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$dist = Join-Path $root 'dist\NacleFoto'
$proj = Join-Path $root 'src\NacleFoto.App\NacleFoto.App.csproj'

# Find the dotnet CLI that actually has an SDK (a runtime-only dotnet fails
# with "No .NET SDKs were found"). No hardcoded user profile: the script has
# to work on any machine.
function Find-Dotnet {
    $candidates = New-Object System.Collections.Generic.List[string]

    $cmd = Get-Command 'dotnet' -ErrorAction SilentlyContinue
    if ($cmd) { $candidates.Add($cmd.Source) }

    $candidates.Add((Join-Path $env:ProgramFiles 'dotnet\dotnet.exe'))
    $candidates.Add((Join-Path ${env:ProgramFiles(x86)} 'dotnet\dotnet.exe'))
    $candidates.Add((Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet\dotnet.exe'))
    $candidates.Add((Join-Path $env:USERPROFILE '.dotnet\dotnet.exe'))

    foreach ($c in $candidates) {
        if (-not $c -or -not (Test-Path $c)) { continue }
        try {
            $sdks = & $c --list-sdks 2>$null
            if ($sdks) { return $c }
        } catch { }
    }

    # nothing with an SDK: fall back to whatever is on the PATH
    if ($cmd) { return $cmd.Source }
    return 'dotnet'
}
$dotnet = Find-Dotnet

Write-Host '==> cleaning' -ForegroundColor DarkGray
Remove-Item $dist -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item (Join-Path $root 'dist\NacleFoto.zip') -Force -ErrorAction SilentlyContinue

Write-Host '==> building (Release)' -ForegroundColor Cyan
& $dotnet publish $proj -c Release -o $dist --nologo
if ($LASTEXITCODE -ne 0) { throw 'build failed' }

Write-Host '==> copying the guides' -ForegroundColor Cyan
Copy-Item (Join-Path $root 'docs\COMO-INSTALAR.txt') (Join-Path $dist 'COMO-INSTALAR.txt') -Force
Copy-Item (Join-Path $root 'docs\INSTALL.txt') (Join-Path $dist 'INSTALL.txt') -Force

if ($Zip) {
    Write-Host '==> creating the zip' -ForegroundColor Cyan
    Compress-Archive -Path (Join-Path $dist '*') -DestinationPath (Join-Path $root 'dist\NacleFoto.zip') -Force
}

$total = (Get-ChildItem $dist -Recurse -File | Measure-Object -Property Length -Sum).Sum
Write-Host ''
Write-Host ('==> done: ' + $dist) -ForegroundColor Green
Write-Host ('    ' + [math]::Round($total / 1MB, 2) + ' MB in ' + (Get-ChildItem $dist -Recurse -File).Count + ' files')
if ($Zip) { Write-Host ('    zip: ' + (Join-Path $root 'dist\NacleFoto.zip')) -ForegroundColor Green }
Write-Host ''
Write-Host 'To open it now:' -ForegroundColor Yellow
Write-Host ('    ' + (Join-Path $dist 'NacleFoto.exe'))
