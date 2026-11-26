[CmdletBinding()]
param([string]$Compiler = "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe")
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$stage = Join-Path $root ("artifacts/staging/" + [guid]::NewGuid())
$payload = Join-Path $stage 'publish'
$assets = Join-Path $stage 'assets'
New-Item -ItemType Directory -Path $stage -Force | Out-Null
try {
    python "$root/scripts/prepare-third-party.py" $assets
    if ($LASTEXITCODE) { throw 'Third-party verification failed.' }
    dotnet publish "$root/src/AWL.Desktop" -p:PublishProfile=win-x64 "-p:AwlVerifiedAssets=$assets" -o $payload
    if ($LASTEXITCODE) { throw 'Publish failed.' }
    & $Compiler "/DPayload=$payload" "$PSScriptRoot/AWL.iss"
    if ($LASTEXITCODE) { throw 'Installer compilation failed.' }
} finally {
    Remove-Item -LiteralPath $stage -Recurse -Force
}
