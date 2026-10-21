# Normalizes the line endings of the source files to CRLF (Windows convention).
# The agents that translated the comments wrote LF, which would show the whole
# file as changed in the diff.
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$files = Get-ChildItem -Path $root -Recurse -Include *.cs,*.xaml,*.csproj,*.manifest,*.ps1,*.md,*.txt -File |
    Where-Object { $_.FullName -notmatch "\\(bin|obj|dist|\.git|node_modules)\\" }

$fixados = 0
foreach ($f in $files) {
    $bytes = [System.IO.File]::ReadAllBytes($f.FullName)
    $temCr = $false
    $temLf = $false
    for ($i = 0; $i -lt $bytes.Length; $i++) {
        if ($bytes[$i] -eq 10) {
            if ($i -gt 0 -and $bytes[$i - 1] -eq 13) { $temCr = $true } else { $temLf = $true }
        }
    }
    if ($temLf -and -not $temCr) {
        $texto = [System.Text.Encoding]::UTF8.GetString($bytes)
        $texto = $texto -replace "`r`n", "`n"
        $texto = $texto -replace "`n", "`r`n"
        [System.IO.File]::WriteAllText($f.FullName, $texto, (New-Object System.Text.UTF8Encoding($false)))
        $fixados++
    }
}
Write-Host ("==> " + $fixados + " arquivo(s) convertido(s) para CRLF")
