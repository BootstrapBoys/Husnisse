# Reset the probe command file so a fresh client does not replay the last batch (e.g. "quit").
$cmd = Join-Path $env:USERPROFILE "husnisse-probe.cmd"
Set-Content -Path $cmd -Value "# idle" -Encoding ASCII
Write-Output ("cmd file reset: " + (Get-Content $cmd -Raw).Trim())
