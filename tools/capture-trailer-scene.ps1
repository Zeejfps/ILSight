# Plays one trailer scene in the real app and saves its frames, with the times they were drawn at.
# Only on the trailer-capture branch. The window is 1920x1080 with the frame's edges left around it.
param(
    [Parameter(Mandatory)][string]$Scene,
    [Parameter(Mandatory)][string[]]$Assemblies,
    [string]$OutRoot = "$env:TEMP\ilsight-trailer"
)

$ErrorActionPreference = 'Stop'

Add-Type @"
using System; using System.Runtime.InteropServices;
public static class TrailerWindow {
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr a, int x, int y, int cx, int cy, uint f);
}
"@
[TrailerWindow]::SetProcessDPIAware() | Out-Null

$NoMoveNoZOrderNoActivate = 0x0016
$exe = Join-Path $PSScriptRoot '..\ILSight\bin\Debug\net10.0\ILSight.exe'
$out = Join-Path $OutRoot $Scene
if (Test-Path $out) { throw "$out already exists; capture into a new folder." }
New-Item -ItemType Directory -Force $out | Out-Null

# The app keeps user themes beside its settings, so they get a folder of their own.
$settingsFolder = Join-Path $OutRoot "settings-$Scene"
New-Item -ItemType Directory -Force $settingsFolder | Out-Null
$settings = Join-Path $settingsFolder 'settings.json'
$display = '{"FontSize":16,"ShowLineNumbers":true,"HighlightCurrentLine":false,"HighlightMatchingBrackets":true,"ShowOmnibar":true,"UiScale":1.1,"Font":null,"TreeTokens":"Hidden","ShowEmptyMetadataTables":false,"NestedNamespaces":false,"SystemTitleBar":true,"PreviewTabs":true}'
[IO.File]::WriteAllText($settings, "{`"Theme`":`"one-dark-pro`",`"IconTheme`":`"lucide`",`"CheckForUpdates`":false,`"Display`":$display}")

$env:ILSIGHT_TRAILER_SCENE = $Scene
$env:ILSIGHT_TRAILER_OUT = $out
$arguments = @('--newinstance', "--settings=`"$settings`"") + ($Assemblies | ForEach-Object { "`"$_`"" })
$app = Start-Process $exe -ArgumentList $arguments -PassThru
Remove-Item Env:ILSIGHT_TRAILER_SCENE, Env:ILSIGHT_TRAILER_OUT

for ($i = 0; $i -lt 60 -and $app.MainWindowHandle -eq 0; $i++) { Start-Sleep -Milliseconds 250; $app.Refresh() }
[TrailerWindow]::SetWindowPos($app.MainWindowHandle, [IntPtr]::Zero, 0, 0, 1938, 1128, $NoMoveNoZOrderNoActivate) | Out-Null
Start-Sleep -Milliseconds 500
[IO.File]::WriteAllText((Join-Path $out 'go'), '')

if (-not $app.WaitForExit(90000)) { $app.Kill(); throw "$Scene did not finish in time." }
$times = Get-Content (Join-Path $out 'times.txt')
$last = [double]($times[-1] -split ' ')[1]
"$Scene : $($times.Count) frames over $([math]::Round($last, 2))s, $([math]::Round($times.Count / $last, 1)) fps"
