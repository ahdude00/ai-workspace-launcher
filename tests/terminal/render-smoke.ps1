$ErrorActionPreference = 'Stop'
$assets = "$PSScriptRoot/../../src/AWL.Desktop/bin/Debug/net10.0-windows/terminal"
$work = Join-Path $env:RUNNER_TEMP ([guid]::NewGuid())
try {
    Copy-Item $assets $work -Recurse
    $page = Join-Path $work 'index.html'
    $html = (Get-Content $page -Raw).Replace('<script src="terminal.js">', '<script src="harness.js"></script><script src="terminal.js">')
    Set-Content $page $html -Encoding utf8
    Copy-Item "$PSScriptRoot/harness.js" $work
    $edge = "${env:ProgramFiles(x86)}\Microsoft\Edge\Application\msedge.exe"
    $dom = & $edge --headless --disable-gpu "--user-data-dir=$work/profile" --virtual-time-budget=5000 --dump-dom ([uri]::new($page).AbsoluteUri) 2>$null | Out-String
    if ($dom -notmatch 'data-drop-verified="yes"') { throw 'Terminal file drop transport check failed.' }
    if ($dom -notmatch 'data-verified="yes"') { throw 'Offline terminal rendering check failed.' }
} finally {
    Remove-Item $work -Recurse -Force -ErrorAction SilentlyContinue
}
