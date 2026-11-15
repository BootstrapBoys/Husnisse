# Put back the folders that were already parked before this session (a session marker and the
# two Quartermaster copies), so plugins holds exactly the tree's 19 mod folders again.
$plugins = "V:\ValheimTest\BepInEx\plugins"
$disabled = "V:\ValheimTest\BepInEx\plugins.disabled"
foreach ($name in @("Searica-Quartermaster", "Vassteel-Quartermaster", "session-0.2b", "session-0.2.2-voice")) {
    $src = Join-Path $plugins $name
    if (Test-Path $src) {
        $dst = Join-Path $disabled $name
        if (Test-Path $dst) { Remove-Item -Recurse -Force $dst }
        Move-Item -LiteralPath $src -Destination $dst
        Write-Host ("  parked " + $name)
    }
}
Write-Host ("plugins=" + (Get-ChildItem $plugins -Directory).Count + " disabled=" + (Get-ChildItem $disabled -Directory).Count)
Get-ChildItem $disabled -Directory | Select-Object -ExpandProperty Name
