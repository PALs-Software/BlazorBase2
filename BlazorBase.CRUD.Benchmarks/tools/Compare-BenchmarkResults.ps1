#requires -Version 7.0
<#
.SYNOPSIS
    Compares two sets of BenchmarkDotNet JSON reports and fails when any
    benchmark regresses beyond the configured threshold.

.DESCRIPTION
    Reads *-report-full-compressed.json files from both the baseline and
    current result folders, joins them by FullName, computes the percentage
    change in Mean (ns) and Allocated (B), and writes a Markdown + CSV
    summary. The script returns a non-zero exit code when any benchmark is
    slower than (1 + ThresholdPercent/100) * baseline.

    Designed to be called from the Compare stage of azure-pipelines.yml.

.PARAMETER BaselinePath
    Folder containing the baseline BenchmarkDotNet JSON reports.

.PARAMETER CurrentPath
    Folder containing the current BenchmarkDotNet JSON reports.

.PARAMETER ThresholdPercent
    Allowed slowdown in percent (e.g. 10 = 10% slower allowed before failing).

.PARAMETER OutputMarkdown
    Destination path for the Markdown comparison report.

.PARAMETER OutputCsv
    Destination path for the CSV comparison report.
#>
param(
    [Parameter(Mandatory = $true)] [string] $BaselinePath,
    [Parameter(Mandatory = $true)] [string] $CurrentPath,
    [Parameter(Mandatory = $false)] [double] $ThresholdPercent = 10,
    [Parameter(Mandatory = $true)] [string] $OutputMarkdown,
    [Parameter(Mandatory = $true)] [string] $OutputCsv
)

$ErrorActionPreference = 'Stop'

function Read-Reports {
    param([string] $Path)

    if (-not (Test-Path $Path)) {
        throw "Result path not found: $Path"
    }

    $files = Get-ChildItem -Path $Path -Recurse -Filter '*-report-full-compressed.json' -File
    if ($files.Count -eq 0) {
        throw "No BenchmarkDotNet JSON reports found in: $Path"
    }

    $results = @{}
    foreach ($file in $files) {
        $document = Get-Content $file.FullName -Raw | ConvertFrom-Json
        foreach ($benchmark in $document.Benchmarks) {
            $key = $benchmark.FullName
            $results[$key] = [pscustomobject]@{
                FullName  = $benchmark.FullName
                Mean      = [double]$benchmark.Statistics.Mean
                StdDev    = [double]$benchmark.Statistics.StandardDeviation
                Allocated = if ($benchmark.Memory) { [double]$benchmark.Memory.BytesAllocatedPerOperation } else { 0 }
            }
        }
    }
    return $results
}

Write-Host "Reading baseline reports from $BaselinePath"
$baseline = Read-Reports -Path $BaselinePath

Write-Host "Reading current reports from $CurrentPath"
$current = Read-Reports -Path $CurrentPath

$rows = New-Object System.Collections.Generic.List[object]
$regressions = New-Object System.Collections.Generic.List[object]

foreach ($key in $current.Keys) {
    $cur = $current[$key]
    if (-not $baseline.ContainsKey($key)) {
        $rows.Add([pscustomobject]@{
            FullName        = $key
            BaselineMeanNs  = $null
            CurrentMeanNs   = $cur.Mean
            MeanDeltaPct    = $null
            BaselineAllocB  = $null
            CurrentAllocB   = $cur.Allocated
            AllocDeltaPct   = $null
            Status          = 'NEW'
        })
        continue
    }

    $base = $baseline[$key]
    $meanDelta = if ($base.Mean -gt 0) { (($cur.Mean - $base.Mean) / $base.Mean) * 100.0 } else { 0 }
    $allocDelta = if ($base.Allocated -gt 0) { (($cur.Allocated - $base.Allocated) / $base.Allocated) * 100.0 } else { 0 }

    $status = if ($meanDelta -gt $ThresholdPercent) { 'REGRESSION' }
              elseif ($meanDelta -lt (-1 * $ThresholdPercent)) { 'IMPROVEMENT' }
              else { 'OK' }

    $row = [pscustomobject]@{
        FullName        = $key
        BaselineMeanNs  = [math]::Round($base.Mean, 1)
        CurrentMeanNs   = [math]::Round($cur.Mean, 1)
        MeanDeltaPct    = [math]::Round($meanDelta, 2)
        BaselineAllocB  = [math]::Round($base.Allocated, 0)
        CurrentAllocB   = [math]::Round($cur.Allocated, 0)
        AllocDeltaPct   = [math]::Round($allocDelta, 2)
        Status          = $status
    }
    $rows.Add($row)
    if ($status -eq 'REGRESSION') { $regressions.Add($row) }
}

foreach ($key in $baseline.Keys) {
    if (-not $current.ContainsKey($key)) {
        $rows.Add([pscustomobject]@{
            FullName        = $key
            BaselineMeanNs  = [math]::Round($baseline[$key].Mean, 1)
            CurrentMeanNs   = $null
            MeanDeltaPct    = $null
            BaselineAllocB  = [math]::Round($baseline[$key].Allocated, 0)
            CurrentAllocB   = $null
            AllocDeltaPct   = $null
            Status          = 'REMOVED'
        })
    }
}

$rows = $rows | Sort-Object -Property @{ Expression = 'Status'; Descending = $false }, FullName

New-Item -ItemType Directory -Path (Split-Path $OutputCsv -Parent) -Force | Out-Null
$rows | Export-Csv -Path $OutputCsv -NoTypeInformation -Encoding UTF8

$markdown = New-Object System.Text.StringBuilder
$null = $markdown.AppendLine('# Benchmark Comparison')
$null = $markdown.AppendLine('')
$null = $markdown.AppendLine("Threshold: $ThresholdPercent% slowdown triggers a regression.")
$null = $markdown.AppendLine("Regressions: $($regressions.Count)")
$null = $markdown.AppendLine('')
$null = $markdown.AppendLine('| Status | Benchmark | Baseline (ns) | Current (ns) | Mean delta % | Alloc delta % |')
$null = $markdown.AppendLine('|---|---|---:|---:|---:|---:|')
foreach ($row in $rows) {
    $null = $markdown.AppendLine("| $($row.Status) | $($row.FullName) | $($row.BaselineMeanNs) | $($row.CurrentMeanNs) | $($row.MeanDeltaPct) | $($row.AllocDeltaPct) |")
}
$markdown.ToString() | Set-Content -Path $OutputMarkdown -Encoding UTF8

if ($regressions.Count -gt 0) {
    Write-Host "##vso[task.logissue type=error]$($regressions.Count) benchmark(s) regressed beyond $ThresholdPercent%."
    foreach ($row in $regressions) {
        Write-Host "##vso[task.logissue type=error]$($row.FullName): $($row.MeanDeltaPct)% slower (baseline $($row.BaselineMeanNs) ns -> current $($row.CurrentMeanNs) ns)"
    }
    exit 1
}

Write-Host "No regressions detected."
exit 0
