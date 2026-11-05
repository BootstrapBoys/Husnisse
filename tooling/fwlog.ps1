$ErrorActionPreference = 'SilentlyContinue'
$log = 'Microsoft-Windows-Windows Firewall With Advanced Security/Firewall'
$ev = Get-WinEvent -LogName $log -MaxEvents 120
if (-not $ev) { 'no events'; exit }
$ev | Where-Object { $_.Id -in 2004, 2005, 2006, 2009, 2010, 2011 } | Select-Object -First 12 | ForEach-Object {
    $first = ($_.Message -split "`r?`n")[0]
    "$($_.TimeCreated.ToString('MM-dd HH:mm')) id=$($_.Id) $first"
}
