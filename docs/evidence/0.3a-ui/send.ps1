# Send a command batch to the client probe: stage\cmd.txt -> %USERPROFILE%\husnisse-probe.cmd.
$ErrorActionPreference = "Stop"
$src = "C:\Users\Hermes\stage\cmd.txt"
$dst = Join-Path $env:USERPROFILE "husnisse-probe.cmd"
$text = Get-Content -LiteralPath $src -Raw
Set-Content -Path $dst -Value $text -Encoding ASCII
Write-Output ("sent: " + ($text -replace "`r?`n", " | "))
