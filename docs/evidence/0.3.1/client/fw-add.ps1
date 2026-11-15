$name = 'Valheim canary rig UDP 2656'
$existing = Get-NetFirewallRule -DisplayName $name -ErrorAction SilentlyContinue
if ($existing) {
  Write-Output 'rule already present'
  ($existing | Get-NetFirewallPortFilter).LocalPort
} else {
  New-NetFirewallRule -DisplayName $name -Direction Inbound -Action Allow -Protocol UDP `
    -LocalPort 2656-2657 -Profile Any -Description 'Client joins the canary tree V:\ValheimTest2 (0.3.1 client gate, 2026-11-15)' | Out-Null
  Write-Output 'rule added'
  ((Get-NetFirewallRule -DisplayName $name) | Get-NetFirewallPortFilter).LocalPort
}
