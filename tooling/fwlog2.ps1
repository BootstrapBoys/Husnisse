$ErrorActionPreference = 'SilentlyContinue'
$log = 'Microsoft-Windows-Windows Firewall With Advanced Security/Firewall'
$ev = Get-WinEvent -LogName $log -MaxEvents 200
$ev | Where-Object { $_.Id -in 2004, 2005, 2006 } | Select-Object -First 10 | ForEach-Object {
    "==== $($_.TimeCreated.ToString('yyyy-MM-dd HH:mm:ss')) id=$($_.Id) ===="
    $_.Message
    ''
}
