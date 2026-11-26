# Run only on a disposable Windows runner with a clean user profile.
$ErrorActionPreference = 'Stop'
$installer = @(Get-ChildItem "$PSScriptRoot/Output/AWL-Setup-*.exe")
if ($installer.Count -ne 1) { throw 'Expected one installer.' }
$dir = Join-Path $env:LOCALAPPDATA 'Programs/AWL'
$data = Join-Path $env:APPDATA 'AWL'
$key = 'HKCU:\Software\Classes\Directory\shell\AWL'
if ((Test-Path $dir) -or (Test-Path $data) -or (Test-Path $key)) { throw 'A clean test profile is required.' }
function Run-Checked($exe) {
    $process = Start-Process -FilePath $exe -ArgumentList '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART' -Wait -PassThru
    if ($process.ExitCode) { throw "Process failed: $exe ($($process.ExitCode))" }
}
Run-Checked $installer[0].FullName
foreach ($file in 'AWL.exe','third_party/uv.exe','third_party/windows-terminal.zip','THIRD-PARTY-NOTICES.txt') {
    if (!(Test-Path (Join-Path $dir $file))) { throw "Missing payload: $file" }
}
New-Item $data -ItemType Directory -Force | Out-Null
$marker = Join-Path $data 'upgrade-marker.txt'
Set-Content $marker 'preserved'
Run-Checked $installer[0].FullName
if ((Get-Content $marker) -ne 'preserved') { throw 'Upgrade lost user data.' }
New-Item $key -Force | Out-Null
Run-Checked (Join-Path $dir 'unins000.exe')
if ((Test-Path "$dir/AWL.exe") -or (Test-Path $key) -or !(Test-Path $marker)) { throw 'Uninstall cleanup failed.' }
