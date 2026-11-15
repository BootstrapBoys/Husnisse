Get-NetFirewallRule -Enabled True -Direction Inbound -Action Allow -ErrorAction SilentlyContinue |
  Where-Object { $_.DisplayName -match 'Valheim|valheim|2656|2556|2756' } |
  ForEach-Object {
    $ports = ($_ | Get-NetFirewallPortFilter).LocalPort
    Write-Output ($_.DisplayName + ' | ' + $_.Profile + ' | ' + $ports)
  }
