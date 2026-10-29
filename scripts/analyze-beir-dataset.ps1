param(
    [Parameter(Mandatory)]
    [string]$DatasetRoot,

    [string]$OutputFile = ".\dataset-report.md"
)

$report = @()

$report += "# Dataset Report"
$report += ""
$report += "Root: $DatasetRoot"
$report += ""

$report += "## Files"
$report += ""

Get-ChildItem $DatasetRoot -Recurse |
ForEach-Object {
    $report += $_.FullName.Replace((Resolve-Path $DatasetRoot).Path, "")
}

$report += ""

$corpus = Join-Path $DatasetRoot "corpus.jsonl"
$queries = Join-Path $DatasetRoot "queries.jsonl"

if(Test-Path $corpus)
{
    $report += "## corpus.jsonl"
    $report += ""

    $report += "Rows: $((Get-Content $corpus).Count)"
    $report += ""

    $report += "### Sample"

    Get-Content $corpus -TotalCount 3 | ForEach-Object {
        $report += $_
    }
}

if(Test-Path $queries)
{
    $report += ""
    $report += "## queries.jsonl"
    $report += ""

    $report += "Rows: $((Get-Content $queries).Count)"
    $report += ""

    $report += "### Sample"

    Get-Content $queries -TotalCount 3 | ForEach-Object {
        $report += $_
    }
}

$qrelsPath = Join-Path $DatasetRoot "qrels"

if(Test-Path $qrelsPath)
{
    $report += ""
    $report += "## Qrels"

    Get-ChildItem $qrelsPath *.tsv |
    ForEach-Object {

        $report += ""
        $report += $_.Name

        $report += "Rows: $((Get-Content $_.FullName).Count)"

        Get-Content $_.FullName -TotalCount 10 |
        ForEach-Object {
            $report += $_
        }
    }
}

$report | Set-Content $OutputFile -Encoding UTF8

Write-Host "Generated:"
Write-Host $OutputFile