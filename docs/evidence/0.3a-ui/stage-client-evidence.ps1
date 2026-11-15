# Copy the client-side evidence for the 0.3a UI gate into the stage folder as one zip.
$g = "C:\Program Files (x86)\Steam\steamapps\common\Valheim"
$out = "C:\Users\Hermes\stage\client-evidence"
New-Item -ItemType Directory -Force -Path $out | Out-Null

Copy-Item -LiteralPath (Join-Path $g "BepInEx\LogOutput.log") -Destination (Join-Path $out "client-bepinex.log") -Force
$caps = Join-Path ([Environment]::GetFolderPath("Desktop")) "husnisse-captures"
if (Test-Path $caps) {
    Get-ChildItem $caps -Filter *.png | ForEach-Object {
        Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $out $_.Name) -Force
    }
}
$cmd = Join-Path $env:USERPROFILE "husnisse-probe.cmd"
if (Test-Path $cmd) { Copy-Item -LiteralPath $cmd -Destination (Join-Path $out "last-probe-command.txt") -Force }

$zip = "C:\Users\Hermes\stage\client-evidence.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path (Join-Path $out "*") -DestinationPath $zip -Force
Write-Output ("zip " + $zip + " " + (Get-Item $zip).Length)
Get-ChildItem $out | Select-Object Name, Length | Format-Table -AutoSize
