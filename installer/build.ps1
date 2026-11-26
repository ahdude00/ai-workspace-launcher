[CmdletBinding()]
param(
    [string]$Compiler = "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    [ValidatePattern('^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(-[0-9A-Za-z-]+(\.[0-9A-Za-z-]+)*)?$')]
    [string]$ReleaseVersion
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$stage = Join-Path $root ("artifacts/staging/" + [guid]::NewGuid())
$payload = Join-Path $stage 'publish'
$assets = Join-Path $stage 'assets'
New-Item -ItemType Directory -Path $stage -Force | Out-Null
try {
    python "$root/scripts/prepare-third-party.py" $assets
    if ($LASTEXITCODE) { throw 'Third-party verification failed.' }
    $publishArgs = @()
    $compilerArgs = @()
    if ($ReleaseVersion) {
        $publishArgs += "-p:Version=$ReleaseVersion"
        $compilerArgs += "/DAppVersion=$ReleaseVersion"
    }
    dotnet publish "$root/src/AWL.Desktop" -p:PublishProfile=win-x64 "-p:AwlVerifiedAssets=$assets" -o $payload @publishArgs
    if ($LASTEXITCODE) { throw 'Publish failed.' }
    & $Compiler @compilerArgs "/DPayload=$payload" "$PSScriptRoot/AWL.iss"
    if ($LASTEXITCODE) { throw 'Installer compilation failed.' }
} finally {
    Remove-Item -LiteralPath $stage -Recurse -Force
}
