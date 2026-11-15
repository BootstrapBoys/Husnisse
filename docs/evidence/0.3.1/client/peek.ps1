$p = Join-Path $env:USERPROFILE 'AppData\LocalLow\IronGate\Valheim\Player.log'
Write-Output ('player.log exists: ' + (Test-Path $p))
if (Test-Path $p) {
  Write-Output ('size ' + (Get-Item $p).Length + ' mtime ' + (Get-Item $p).LastWriteTime)
  Get-Content -LiteralPath $p -Tail 40
}
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
$b = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
$bmp = New-Object System.Drawing.Bitmap $b.Width, $b.Height
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen($b.Location, [System.Drawing.Point]::Empty, $b.Size)
$out = Join-Path $env:USERPROFILE 'stage\screen.png'
$bmp.Save($out, [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose(); $bmp.Dispose()
Write-Output ('shot ' + $out + ' ' + (Get-Item $out).Length)
