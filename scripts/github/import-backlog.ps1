<#
.SYNOPSIS
    Creates labels, milestones and issues on GitHub from .github/backlog/backlog.json.

.DESCRIPTION
    Idempotent: labels are created or updated, existing milestones are reused and
    issues whose title already starts with the task key (e.g. "[F1-08]") are skipped.
    Dependencies are written as "#<issue number>"; a second pass fills in numbers
    for dependencies that were created later in the same run.

.PARAMETER Repo
    owner/name, e.g. ahdude00/ai-workspace-launcher

.PARAMETER DryRun
    Sends nothing to GitHub. Writes a Markdown preview of every issue (numbered as
    they would be in an empty repository) and prints its path.

.EXAMPLE
    .\scripts\github\import-backlog.ps1 -Repo ahdude00/ai-workspace-launcher -DryRun
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)] [string] $Repo,
    [string] $BacklogPath = (Join-Path $PSScriptRoot '..\..\.github\backlog\backlog.json'),
    [string] $PreviewPath = (Join-Path ([IO.Path]::GetTempPath()) 'awl-backlog-preview.md'),
    [switch] $DryRun
)

$ErrorActionPreference = 'Stop'
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8   # gh prints UTF-8

$backlog = [IO.File]::ReadAllText((Resolve-Path $BacklogPath), $utf8NoBom) | ConvertFrom-Json
$tasksByKey = @{}
foreach ($t in $backlog.tasks) { $tasksByKey[$t.key] = $t }
$milestoneTitle = @{}
foreach ($m in $backlog.milestones) { $milestoneTitle[$m.key] = $m.title }

function Invoke-Gh {
    param([string[]] $GhArgs)
    $output = & gh @GhArgs
    if ($LASTEXITCODE -ne 0) { throw "gh failed ($LASTEXITCODE): gh $($GhArgs -join ' ')" }
    return $output
}

function Get-TaskLabels($task) {
    $labels = @("tür: $($task.type)", "öncelik: $($task.priority)")
    foreach ($a in $task.areas) { $labels += "alan: $a" }
    return $labels
}

function Get-IssueTitle($task) { return "[$($task.key)] $($task.title)" }

function Format-Body($task, $numbers) {
    $lines = New-Object System.Collections.Generic.List[string]
    $lines.Add('## Amaç'); $lines.Add($task.summary); $lines.Add('')
    $lines.Add('## Yapılacaklar')
    foreach ($item in $task.todo) { $lines.Add("- [ ] $item") }
    $lines.Add('')
    $lines.Add('## Kabul kriterleri')
    foreach ($item in $task.acceptance) { $lines.Add("- [ ] $item") }
    $lines.Add('- [ ] CI yeşil, PR `main`''e squash merge edildi')
    $lines.Add('')
    $lines.Add('## Gereksinimler / kararlar'); $lines.Add($task.requirements); $lines.Add('')
    $lines.Add('## Bağımlılıklar')
    if (-not $task.dependsOn -or $task.dependsOn.Count -eq 0) { $lines.Add('Yok') }
    else {
        foreach ($dep in $task.dependsOn) {
            $depTask = $tasksByKey[$dep]
            $ref = if ($numbers.ContainsKey($dep)) { "#$($numbers[$dep])" } else { "``$dep``" }
            $lines.Add("- $ref — $($depTask.title)")
        }
    }
    $lines.Add('')
    $branch = "task/$($task.key.ToLowerInvariant())-$($task.slug)"
    $lines.Add("**Dal:** ``$branch``")
    $lines.Add('')
    $lines.Add("<sub>Kaynak: ``$($backlog.source)`` · backlog anahtarı ``$($task.key)``</sub>")
    return ($lines -join "`n")
}

# Every dependency key must exist
foreach ($t in $backlog.tasks) {
    foreach ($d in $t.dependsOn) {
        if (-not $tasksByKey.ContainsKey($d)) { throw "$($t.key): unknown dependency $d" }
    }
}

if ($DryRun) {
    $numbers = @{}
    $n = 1
    foreach ($t in $backlog.tasks) { $numbers[$t.key] = $n; $n++ }
    $sb = New-Object System.Text.StringBuilder
    [void]$sb.AppendLine("# Backlog önizlemesi ($($backlog.tasks.Count) issue)")
    [void]$sb.AppendLine('')
    [void]$sb.AppendLine('Numaralar boş bir depoda oluşacak sıraya göredir.')
    foreach ($t in $backlog.tasks) {
        [void]$sb.AppendLine('')
        [void]$sb.AppendLine('---')
        [void]$sb.AppendLine("# #$($numbers[$t.key]) $(Get-IssueTitle $t)")
        [void]$sb.AppendLine("*Milestone:* $($milestoneTitle[$t.milestone]) · *Etiketler:* $((Get-TaskLabels $t) -join ', ')")
        [void]$sb.AppendLine('')
        [void]$sb.AppendLine((Format-Body $t $numbers))
    }
    [IO.File]::WriteAllText($PreviewPath, $sb.ToString(), $utf8NoBom)
    Write-Host "[dry-run] $($backlog.labels.Count) labels, $($backlog.milestones.Count) milestones, $($backlog.tasks.Count) issues"
    Write-Host "[dry-run] Preview: $PreviewPath"
    return
}

# 1) Labels (create or update)
foreach ($l in $backlog.labels) {
    Invoke-Gh @('label', 'create', $l.name, '--repo', $Repo, '--color', $l.color, '--description', $l.description, '--force') | Out-Null
}
Write-Host "Labels: $($backlog.labels.Count) ok"

# 2) Milestones (reuse existing by title)
$existingMilestones = (Invoke-Gh @('api', "repos/$Repo/milestones?state=all&per_page=100")) -join "`n" | ConvertFrom-Json
foreach ($m in $backlog.milestones) {
    if (-not ($existingMilestones | Where-Object { $_.title -eq $m.title })) {
        Invoke-Gh @('api', "repos/$Repo/milestones", '-f', "title=$($m.title)", '-f', "description=$($m.description)") | Out-Null
        Write-Host "Milestone created: $($m.title)"
    }
}

# 3) Issues (skip those whose key already exists)
$numbers = @{}
$existingIssues = (Invoke-Gh @('issue', 'list', '--repo', $Repo, '--state', 'all', '--limit', '1000', '--json', 'number,title')) -join "`n" | ConvertFrom-Json
foreach ($i in $existingIssues) {
    if ($i.title -match '^\[(F\d-\d{2})\]') { $numbers[$Matches[1]] = $i.number }
}

$created = New-Object System.Collections.Generic.List[string]
$tmp = [IO.Path]::GetTempFileName()
try {
    foreach ($t in $backlog.tasks) {
        if ($numbers.ContainsKey($t.key)) { Write-Host "Skip (exists): #$($numbers[$t.key]) $($t.key)"; continue }
        [IO.File]::WriteAllText($tmp, (Format-Body $t $numbers), $utf8NoBom)
        $ghArgs = @('issue', 'create', '--repo', $Repo, '--title', (Get-IssueTitle $t), '--body-file', $tmp, '--milestone', $milestoneTitle[$t.milestone])
        foreach ($label in (Get-TaskLabels $t)) { $ghArgs += @('--label', $label) }
        $url = (Invoke-Gh $ghArgs | Select-Object -Last 1).Trim()
        $numbers[$t.key] = [int]($url -replace '.*/', '')
        $created.Add($t.key)
        Write-Host "Created: #$($numbers[$t.key]) $($t.key)"
    }

    # 4) Second pass: fill in dependency numbers that did not exist at creation time
    foreach ($key in $created) {
        $t = $tasksByKey[$key]
        $hasForwardDep = $false
        foreach ($d in $t.dependsOn) { if ($created.IndexOf($d) -gt $created.IndexOf($key)) { $hasForwardDep = $true } }
        if ($hasForwardDep) {
            [IO.File]::WriteAllText($tmp, (Format-Body $t $numbers), $utf8NoBom)
            Invoke-Gh @('issue', 'edit', "$($numbers[$key])", '--repo', $Repo, '--body-file', $tmp) | Out-Null
            Write-Host "Updated dependencies: #$($numbers[$key]) $key"
        }
    }
}
finally {
    Remove-Item $tmp -ErrorAction SilentlyContinue
}
Write-Host "Done. Created $($created.Count) issue(s)."
