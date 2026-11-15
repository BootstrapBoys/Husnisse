$log = "V:\ValheimTest\BepInEx\LogOutput.log"
Write-Output "== matches =="
Select-String -Path $log -Pattern "RPC registered|rule RPC|Husnisse_Rule" -SimpleMatch:$false |
    ForEach-Object { Write-Output ($_.LineNumber.ToString() + ": " + $_.Line) }
Write-Output ("== lines " + (Get-Content $log).Count)
