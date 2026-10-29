param(
    [string]$OutputFile = ".\EvaluationProjectReport.txt"
)

$ErrorActionPreference = "SilentlyContinue"

function Write-Section {
    param([string]$Title)

    Add-Content $OutputFile ""
    Add-Content $OutputFile ("=" * 120)
    Add-Content $OutputFile $Title
    Add-Content $OutputFile ("=" * 120)
    Add-Content $OutputFile ""
}

Remove-Item $OutputFile -Force -ErrorAction SilentlyContinue

Write-Section "GENERAL"

Get-Date | Out-String | Add-Content $OutputFile

Write-Section "CURRENT DIRECTORY"

Get-Location | Out-String | Add-Content $OutputFile

Write-Section "DOTNET INFO"

dotnet --info | Out-String | Add-Content $OutputFile

Write-Section "SOLUTION FILES"

Get-ChildItem -Recurse -Filter *.sln |
    Select-Object FullName |
    Format-Table -AutoSize |
    Out-String |
    Add-Content $OutputFile

Write-Section "PROJECTS"

Get-ChildItem -Recurse -Filter *.csproj |
    Select-Object FullName |
    Sort-Object FullName |
    Format-Table -AutoSize |
    Out-String |
    Add-Content $OutputFile

Write-Section "DOTNET SLN LIST"

$solution = Get-ChildItem -Recurse -Filter *.sln | Select-Object -First 1

if ($solution)
{
    dotnet sln $solution.FullName list |
        Out-String |
        Add-Content $OutputFile
}

Write-Section "FULL DIRECTORY TREE"

cmd /c tree /F |
    Out-String |
    Add-Content $OutputFile

Write-Section "SOURCE FILES"

Get-ChildItem -Recurse -Filter *.cs |
    Select-Object FullName |
    Sort-Object FullName |
    Out-String |
    Add-Content $OutputFile

Write-Section "APPSETTINGS"

Get-ChildItem -Recurse -Include appsettings*.json |
ForEach-Object {

    Add-Content $OutputFile ""
    Add-Content $OutputFile "FILE: $($_.FullName)"
    Add-Content $OutputFile ""

    Get-Content $_.FullName |
        Out-String |
        Add-Content $OutputFile
}

Write-Section "DBCONTEXT FILES"

Get-ChildItem -Recurse -Filter *DbContext*.cs |
ForEach-Object {

    Add-Content $OutputFile ""
    Add-Content $OutputFile "FILE: $($_.FullName)"
    Add-Content $OutputFile ""

    Get-Content $_.FullName |
        Out-String |
        Add-Content $OutputFile
}

Write-Section "ENTITY CONFIGURATIONS"

Get-ChildItem -Recurse -Filter *Configuration.cs |
ForEach-Object {

    Add-Content $OutputFile ""
    Add-Content $OutputFile "FILE: $($_.FullName)"
    Add-Content $OutputFile ""

    Get-Content $_.FullName |
        Out-String |
        Add-Content $OutputFile
}

Write-Section "DOMAIN ENTITIES"

Get-ChildItem -Recurse |
Where-Object {
    $_.FullName -match "\\Entities\\" -and $_.Extension -eq ".cs"
}
|
ForEach-Object {

    Add-Content $OutputFile ""
    Add-Content $OutputFile "FILE: $($_.FullName)"
    Add-Content $OutputFile ""

    Get-Content $_.FullName |
        Out-String |
        Add-Content $OutputFile
}

Write-Section "REPOSITORIES"

Get-ChildItem -Recurse |
Where-Object {
    $_.Name -like "*Repository*.cs"
}
|
ForEach-Object {

    Add-Content $OutputFile ""
    Add-Content $OutputFile "FILE: $($_.FullName)"
    Add-Content $OutputFile ""

    Get-Content $_.FullName |
        Out-String |
        Add-Content $OutputFile
}

Write-Section "SERVICES"

Get-ChildItem -Recurse |
Where-Object {
    $_.Name -like "*Service*.cs"
}
|
ForEach-Object {

    Add-Content $OutputFile ""
    Add-Content $OutputFile "FILE: $($_.FullName)"
    Add-Content $OutputFile ""

    Get-Content $_.FullName |
        Out-String |
        Add-Content $OutputFile
}

Write-Section "CONTROLLERS"

Get-ChildItem -Recurse |
Where-Object {
    $_.FullName -match "\\Controllers\\" -and $_.Extension -eq ".cs"
}
|
ForEach-Object {

    Add-Content $OutputFile ""
    Add-Content $OutputFile "FILE: $($_.FullName)"
    Add-Content $OutputFile ""

    Get-Content $_.FullName |
        Out-String |
        Add-Content $OutputFile
}

Write-Section "DTOS"

Get-ChildItem -Recurse |
Where-Object {
    $_.Name -like "*Dto.cs" -or
    $_.Name -like "*DTO.cs"
}
|
ForEach-Object {

    Add-Content $OutputFile ""
    Add-Content $OutputFile "FILE: $($_.FullName)"
    Add-Content $OutputFile ""

    Get-Content $_.FullName |
        Out-String |
        Add-Content $OutputFile
}

Write-Section "COMMANDS"

Get-ChildItem -Recurse |
Where-Object {
    $_.Name -like "*Command*.cs"
}
|
ForEach-Object {

    Add-Content $OutputFile ""
    Add-Content $OutputFile "FILE: $($_.FullName)"
    Add-Content $OutputFile ""

    Get-Content $_.FullName |
        Out-String |
        Add-Content $OutputFile
}

Write-Section "QUERIES"

Get-ChildItem -Recurse |
Where-Object {
    $_.Name -like "*Query*.cs"
}
|
ForEach-Object {

    Add-Content $OutputFile ""
    Add-Content $OutputFile "FILE: $($_.FullName)"
    Add-Content $OutputFile ""

    Get-Content $_.FullName |
        Out-String |
        Add-Content $OutputFile
}

Write-Section "PROGRAM FILES"

Get-ChildItem -Recurse |
Where-Object {
    $_.Name -eq "Program.cs"
}
|
ForEach-Object {

    Add-Content $OutputFile ""
    Add-Content $OutputFile "FILE: $($_.FullName)"
    Add-Content $OutputFile ""

    Get-Content $_.FullName |
        Out-String |
        Add-Content $OutputFile
}

Write-Section "PACKAGE REFERENCES"

Get-ChildItem -Recurse -Filter *.csproj |
ForEach-Object {

    Add-Content $OutputFile ""
    Add-Content $OutputFile "PROJECT: $($_.FullName)"
    Add-Content $OutputFile ""

    Get-Content $_.FullName |
        Out-String |
        Add-Content $OutputFile
}

Write-Host ""
Write-Host "Report generated:"
Write-Host $OutputFile
Write-Host ""