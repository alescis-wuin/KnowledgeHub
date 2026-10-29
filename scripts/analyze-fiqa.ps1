param(
    [string]$DatasetRoot = ".\Data\benchmarks\fiqa",
    [string]$ReportFile = ".\Data\reports\dataset-fiqa.md"
)

New-Item `
    -ItemType Directory `
    -Force `
    -Path (Split-Path $ReportFile) | Out-Null

$corpusFile = Join-Path $DatasetRoot "corpus.jsonl"
$queriesFile = Join-Path $DatasetRoot "queries.jsonl"

$qrelsFiles = Get-ChildItem `
    -Path (Join-Path $DatasetRoot "qrels") `
    -Filter *.tsv `
    -ErrorAction SilentlyContinue

$report = @()

$report += "# Dataset FIQA"
$report += ""

$report += "## Arborescence"
$report += ""

Get-ChildItem $DatasetRoot -Recurse |
ForEach-Object {
    $report += $_.FullName.Replace((Resolve-Path $DatasetRoot).Path,"")
}

$report += ""

if(Test-Path $corpusFile)
{
    $corpusCount = (Get-Content $corpusFile).Count

    $report += "## Corpus"
    $report += ""
    $report += "- Documents : $corpusCount"
    $report += ""

    $report += "### Premier document"

    Get-Content $corpusFile -TotalCount 1 |
    ForEach-Object {
        $report += $_
    }

    $report += ""
}

if(Test-Path $queriesFile)
{
    $queryCount = (Get-Content $queriesFile).Count

    $report += "## Queries"
    $report += ""
    $report += "- Questions : $queryCount"
    $report += ""

    $report += "### Premiere query"

    Get-Content $queriesFile -TotalCount 1 |
    ForEach-Object {
        $report += $_
    }

    $report += ""
}

$report += "## Qrels"

foreach($file in $qrelsFiles)
{
    $count = (Get-Content $file.FullName).Count

    $report += "- $($file.Name) : $count lignes"
}

$report | Set-Content $ReportFile -Encoding UTF8

Write-Host ""
Write-Host "Rapport genere :"
Write-Host $ReportFile
Write-Host ""