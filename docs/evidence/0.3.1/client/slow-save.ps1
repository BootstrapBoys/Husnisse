$p = 'V:\Gate031\start-test.cmd'
$t = Get-Content -LiteralPath $p -Raw
$t = $t -replace '-saveinterval 300', '-saveinterval 3600'
Set-Content -LiteralPath $p -Value $t -Encoding ASCII
Get-Content -LiteralPath $p
