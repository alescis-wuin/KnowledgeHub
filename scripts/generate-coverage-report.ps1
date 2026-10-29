Write-Host ""
Write-Host "======================" -ForegroundColor Cyan
Write-Host " NETTOYAGE" -ForegroundColor Cyan
Write-Host "======================" -ForegroundColor Cyan

dotnet clean

Write-Host ""
Write-Host "======================" -ForegroundColor Cyan
Write-Host " BUILD" -ForegroundColor Cyan
Write-Host "======================" -ForegroundColor Cyan

dotnet build

Write-Host ""
Write-Host "======================" -ForegroundColor Cyan
Write-Host " TESTS + COVERAGE" -ForegroundColor Cyan
Write-Host "======================" -ForegroundColor Cyan

dotnet test `
    --collect:"XPlat Code Coverage" `
    --settings coverlet.runsettings

Write-Host ""
Write-Host "======================" -ForegroundColor Cyan
Write-Host " REPORT GENERATION" -ForegroundColor Cyan
Write-Host "======================" -ForegroundColor Cyan

reportgenerator `
-reports:**/coverage.cobertura.xml `
-targetdir:coveragereport `
-reporttypes:Html

Write-Host ""
Write-Host "======================" -ForegroundColor Green
Write-Host " OPEN REPORT" -ForegroundColor Green
Write-Host "======================" -ForegroundColor Green

start .\coveragereport\index.html
