$ErrorActionPreference = "Continue"

$Report = @()

function Add-Section {
    param([string]$Title)

    $Report += ""
    $Report += ("=" * 100)
    $Report += $Title
    $Report += ("=" * 100)
}

Add-Section "1. DATASETS"

$datasetRoot = Join-Path $PWD "Data\benchmarks"

$Report += "Expected path:"
$Report += $datasetRoot

if(Test-Path $datasetRoot)
{
    $Report += "[OK] Dataset folder exists"

    Get-ChildItem $datasetRoot -Recurse |
    ForEach-Object {
        $Report += $_.FullName
    }
}
else
{
    $Report += "[ERROR] Dataset folder missing"
}

Add-Section "2. PROJECT FILES"

$files = @(
    "KnowledgeHub.Domain\Entities\EvaluationDataset.cs",
    "KnowledgeHub.Domain\Entities\EvaluationQuestion.cs",
    "KnowledgeHub.Domain\Entities\EvaluationReferenceDocument.cs",
    "KnowledgeHub.Infrastructure\Persistence\Configurations\EvaluationDatasetConfiguration.cs",
    "KnowledgeHub.Infrastructure\Persistence\Configurations\EvaluationQuestionConfiguration.cs",
    "KnowledgeHub.Infrastructure\Persistence\Configurations\EvaluationReferenceDocumentConfiguration.cs"
)

foreach($file in $files)
{
    if(Test-Path $file)
    {
        $Report += "[OK] $file"
    }
    else
    {
        $Report += "[MISSING] $file"
    }
}

Add-Section "3. DBCONTEXT"

$dbContext = Get-ChildItem -Recurse *DbContext.cs |
    Select-Object -First 1

if($dbContext)
{
    $Report += $dbContext.FullName

    $content = Get-Content $dbContext.FullName -Raw

    foreach($name in @(
        "EvaluationDataset",
        "EvaluationQuestion",
        "EvaluationReferenceDocument"
    ))
    {
        if($content -match $name)
        {
            $Report += "[OK] Found $name"
        }
        else
        {
            $Report += "[MISSING] $name"
        }
    }
}

Add-Section "4. EF CONFIGURATIONS"

Get-ChildItem `
    -Recurse `
    -Filter *.cs |
ForEach-Object {

    $content = Get-Content $_.FullName -Raw

    if($content -match "EvaluationDatasetConfiguration")
    {
        $Report += "[FOUND] EvaluationDatasetConfiguration"
    }

    if($content -match "EvaluationQuestionConfiguration")
    {
        $Report += "[FOUND] EvaluationQuestionConfiguration"
    }

    if($content -match "EvaluationReferenceDocumentConfiguration")
    {
        $Report += "[FOUND] EvaluationReferenceDocumentConfiguration"
    }
}

Add-Section "5. MIGRATIONS"

$migrations = Get-ChildItem `
    -Recurse `
    "*Migration*.cs"

if($migrations)
{
    $Report += "Migration count: $($migrations.Count)"

    $migrations |
    ForEach-Object {
        $Report += $_.FullName
    }
}
else
{
    $Report += "[ERROR] No migrations found"
}

Add-Section "6. POSTGRES TABLES"

docker exec knowledgehub-postgres `
    psql `
    -U postgres `
    -d knowledgehub `
    -c '\dt' |
    ForEach-Object {
        $Report += $_
    }

Add-Section "7. POSTGRES EVALUATION TABLES"

docker exec knowledgehub-postgres `
    psql `
    -U postgres `
    -d knowledgehub `
    -c "SELECT table_name
        FROM information_schema.tables
        WHERE table_schema='public'
        ORDER BY table_name;" |
ForEach-Object {
    $Report += $_
}

Add-Section "8. PENDING MIGRATIONS"

dotnet ef migrations list `
    --project KnowledgeHub.Infrastructure `
    --startup-project KnowledgeHub.Api |
ForEach-Object {
    $Report += $_
}

Add-Section "9. IMPORTER SOURCE"

$program = Get-ChildItem `
    -Recurse `
    Program.cs |
    Where-Object {
        $_.FullName -match "CorpusImporter"
    } |
    Select-Object -First 1

if($program)
{
    $Report += $program.FullName

    Select-String `
        -Path $program.FullName `
        -Pattern "benchmarks|Data|Evaluation" |
    ForEach-Object {

        $Report += $_.Line
    }
}

$outfile = ".\EvaluationVerificationReport.txt"

$Report | Set-Content $outfile

Write-Host ""
Write-Host "Generated:"
Write-Host $outfile
Write-Host ""