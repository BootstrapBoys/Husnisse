$log = "C:\Program Files (x86)\Steam\steamapps\common\Valheim\BepInEx\LogOutput.log"
$lines = Get-Content $log
$open = ($lines | Select-String -Pattern 'gc open gui=').Count
$none = ($lines | Select-String -Pattern 'gc open gui=none').Count
$stand = ($lines | Select-String -Pattern 'gc stand piece_chest').Count
$click = ($lines | Select-String -Pattern 'gc click HusnisseCat').Count
Write-Output ("open=$open none=$none stand=$stand catclicks=$click")
