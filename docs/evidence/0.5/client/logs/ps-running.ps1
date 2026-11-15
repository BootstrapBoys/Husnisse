$p = Get-Process valheim -ErrorAction SilentlyContinue
if ($p) { Write-Output ('running ' + $p.Id) } else { Write-Output 'idle' }
