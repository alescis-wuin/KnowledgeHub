# Emplacement :
# C:\Users\aguinot\Documents\developpement\KnowledgeHub\scripts\analyze-fiqa.ps1
#
# Remplacer integralement le contenu du fichier.

[CmdletBinding()]
param(
    [string]$DatasetRoot = ".\Data\benchmarks\fiqa",

    [string]$ReportFile = ".\Data\reports\dataset-fiqa.md",

    [ValidateRange(1, 100)]
    [int]$SampleCount = 5
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version 2.0

function Write-Step {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Message
    )

    Write-Host ("[STEP] {0}" -f $Message) -ForegroundColor Cyan
}

function Write-Success {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Message
    )

    Write-Host ("[OK] {0}" -f $Message) -ForegroundColor Green
}

function Write-WarningMessage {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Message
    )

    Write-Host ("[WARN] {0}" -f $Message) -ForegroundColor Yellow
}

function Get-LineCount {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        return 0
    }

    $count = 0

    $reader = New-Object System.IO.StreamReader($Path)

    try {
        while ($null -ne $reader.ReadLine()) {
            $count++
        }
    }
    finally {
        $reader.Dispose()
    }

    return $count
}

function Get-FileSamples {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [Parameter(Mandatory = $true)]
        [int]$Count
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        return @()
    }

    return @(
        Get-Content `
            -LiteralPath $Path `
            -TotalCount $Count `
            -Encoding UTF8
    )
}

function Get-JsonlAnalysis {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [Parameter(Mandatory = $true)]
        [int]$SampleLimit
    )

    $result = [ordered]@{
        RowCount       = 0
        ValidJsonCount = 0
        InvalidCount   = 0
        EmptyIdCount   = 0
        EmptyTextCount = 0
        Samples        = @()
        Errors         = @()
        PropertyNames  = @()
    }

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        return [PSCustomObject]$result
    }

    $propertyNames = @{}
    $reader = New-Object System.IO.StreamReader($Path)
    $lineNumber = 0

    try {
        while ($null -ne ($line = $reader.ReadLine())) {
            $lineNumber++
            $result.RowCount++

            if ([string]::IsNullOrWhiteSpace($line)) {
                $result.InvalidCount++

                if ($result.Errors.Count -lt $SampleLimit) {
                    $result.Errors += "Ligne $lineNumber : ligne vide."
                }

                continue
            }

            try {
                $item = $line | ConvertFrom-Json -ErrorAction Stop
                $result.ValidJsonCount++

                foreach ($property in $item.PSObject.Properties) {
                    $propertyNames[$property.Name] = $true
                }

                $externalId = $null
                $text = $null

                if ($item.PSObject.Properties.Name -contains "_id") {
                    $externalId = [string]$item._id
                }

                if ($item.PSObject.Properties.Name -contains "text") {
                    $text = [string]$item.text
                }

                if ([string]::IsNullOrWhiteSpace($externalId)) {
                    $result.EmptyIdCount++
                }

                if ([string]::IsNullOrWhiteSpace($text)) {
                    $result.EmptyTextCount++
                }

                if ($result.Samples.Count -lt $SampleLimit) {
                    $result.Samples += $line
                }
            }
            catch {
                $result.InvalidCount++

                if ($result.Errors.Count -lt $SampleLimit) {
                    $result.Errors += (
                        "Ligne {0} : {1}" -f
                        $lineNumber,
                        $_.Exception.Message
                    )
                }
            }
        }
    }
    finally {
        $reader.Dispose()
    }

    $result.PropertyNames = @(
        $propertyNames.Keys | Sort-Object
    )

    return [PSCustomObject]$result
}

function Get-QrelsAnalysis {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [Parameter(Mandatory = $true)]
        [int]$SampleLimit
    )

    $result = [ordered]@{
        RowCount           = 0
        DataRowCount       = 0
        InvalidCount       = 0
        UniqueQueryCount   = 0
        