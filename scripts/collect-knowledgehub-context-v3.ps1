<#
.SYNOPSIS
    Exporte un contexte complet de reprise pour le projet KnowledgeHub.

.DESCRIPTION
    Version 3, compatible Windows PowerShell 5.1 et PowerShell 7+.
    Produit au maximum trois fichiers finaux :
      - KnowledgeHub-Context.md
      - KnowledgeHub-Tree.json
      - KnowledgeHub-Diagnostics.md

    Le script collecte l'environnement .NET, Git, Docker, PostgreSQL, Ollama,
    les resultats clean/restore/build/test/coverage, un probe API/RAG, les
    metriques RAG et le contenu des fichiers critiques.

.NOTES
    Le script ne modifie pas la strategie d'execution PowerShell.
    Si le fichier provient d'Internet, utilisez une seule fois :
        Unblock-File .\scripts\collect-knowledgehub-context-v3.ps1
#>

[CmdletBinding()]
param(
    [ValidateSet("Full", "Light")]
    [string]$Mode = "Full",

    [string]$PostgresContainer = "knowledgehub-postgres",
    [string]$PostgresDatabase = "knowledgehub",
    [string]$PostgresUser = "postgres",
    [string]$ApiBaseUrl = "http://localhost:5115",
    [string]$RagQuestion = "Comment mettre en place CQRS avec MediatR ?",
    [string]$OutputDirectory = ".",

    [switch]$SkipBuild,
    [switch]$SkipTests,
    [switch]$SkipCoverage,
    [switch]$SkipDocker,
    [switch]$SkipOllama,
    [switch]$SkipApiProbe,
    [switch]$FailOnDiagnosticError
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Continue"

# ============================================================================
# INITIALISATION
# ============================================================================

$ScriptDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectRoot = (Resolve-Path (Join-Path $ScriptDirectory "..")).Path
Set-Location $ProjectRoot

if (-not [System.IO.Path]::IsPathRooted($OutputDirectory)) {
    $OutputDirectory = Join-Path $ProjectRoot $OutputDirectory
}

New-Item -Path $OutputDirectory -ItemType Directory -Force | Out-Null
$OutputDirectory = (Resolve-Path $OutputDirectory).Path

$ContextFile = Join-Path $OutputDirectory "KnowledgeHub-Context.md"
$TreeFile = Join-Path $OutputDirectory "KnowledgeHub-Tree.json"
$DiagnosticsFile = Join-Path $OutputDirectory "KnowledgeHub-Diagnostics.md"
$TemporaryDirectory = Join-Path $ProjectRoot ".diagnostic-temp"
$CoverageDirectory = Join-Path $TemporaryDirectory "coverage"
$CoverageReportDirectory = Join-Path $CoverageDirectory "report"
$Fence = ([string][char]96) * 3
$StartTime = Get-Date

$script:StepNumber = 0
$script:WarningCount = 0
$script:ErrorCount = 0
$script:StepResults = New-Object 'System.Collections.Generic.List[object]'
$script:RagProbeResult = $null
$script:DatabaseMetrics = $null

$ExcludedDirectoryNames = @(
    ".git", ".vs", ".idea", ".diagnostic-temp", "bin", "obj",
    "TestResults", "coverage", "logs", "node_modules", "packages"
)

$TextExtensions = @(
    ".cs", ".csproj", ".props", ".targets", ".sln", ".slnx",
    ".xaml", ".xml", ".json", ".md", ".txt", ".yml", ".yaml",
    ".ps1", ".psm1", ".sh", ".sql", ".http", ".editorconfig",
    ".gitignore", ".dockerignore", ".config", ".razor"
)

foreach ($Path in @($ContextFile, $TreeFile, $DiagnosticsFile)) {
    if (Test-Path -LiteralPath $Path) {
        Remove-Item -LiteralPath $Path -Force -ErrorAction SilentlyContinue
    }
}

if (Test-Path -LiteralPath $TemporaryDirectory) {
    Remove-Item -LiteralPath $TemporaryDirectory -Recurse -Force -ErrorAction SilentlyContinue
}

New-Item -Path $CoverageDirectory -ItemType Directory -Force | Out-Null

# ============================================================================
# JOURNALISATION ET MARKDOWN
# ============================================================================

function Write-DiagnosticLog {
    param(
        [ValidateSet("INFO", "STEP", "OK", "WARN", "ERROR", "DONE")]
        [string]$Level,
        [string]$Message
    )

    $Color = switch ($Level) {
        "INFO"  { "Cyan" }
        "STEP"  { "Blue" }
        "OK"    { "Green" }
        "WARN"  { "Yellow" }
        "ERROR" { "Red" }
        "DONE"  { "Green" }
        default  { "White" }
    }

    $Timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    Write-Host "[$Timestamp] [$Level] $Message" -ForegroundColor $Color
}

function Start-DiagnosticStep {
    param([string]$Message)
    $script:StepNumber++
    Write-DiagnosticLog -Level "STEP" -Message ("Etape {0}: {1}" -f $script:StepNumber, $Message)
}

function Add-MarkdownLine {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [AllowEmptyString()][string]$Text = ""
    )

    Add-Content -LiteralPath $Path -Value $Text -Encoding UTF8
}

function Add-MarkdownSection {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Title,
        [ValidateRange(1, 6)][int]$Level = 2
    )

    Add-MarkdownLine -Path $Path
    Add-MarkdownLine -Path $Path -Text (("#" * $Level) + " " + $Title)
    Add-MarkdownLine -Path $Path
}

function Add-MarkdownCodeBlock {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [AllowEmptyString()][string]$Content = "",
        [string]$Language = "text"
    )

    if ($null -eq $Content) { $Content = "" }
    Add-MarkdownLine -Path $Path -Text ($Fence + $Language)
    Add-MarkdownLine -Path $Path -Text $Content.TrimEnd()
    Add-MarkdownLine -Path $Path -Text $Fence
    Add-MarkdownLine -Path $Path
}

# ============================================================================
# COMPATIBILITE POWERSHELL 5.1 / 7+
# ============================================================================

function Convert-ToRelativePath {
    param(
        [Parameter(Mandatory = $true)][string]$BasePath,
        [Parameter(Mandatory = $true)][string]$TargetPath
    )

    $BaseFull = [System.IO.Path]::GetFullPath($BasePath).TrimEnd('\', '/')
    $TargetFull = [System.IO.Path]::GetFullPath($TargetPath)
    $Prefix = $BaseFull + [System.IO.Path]::DirectorySeparatorChar

    if ($TargetFull.StartsWith($Prefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        return $TargetFull.Substring($Prefix.Length).Replace('\', '/')
    }

    try {
        $BaseUri = New-Object System.Uri(($BaseFull.TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar))
        $TargetUri = New-Object System.Uri($TargetFull)
        return [System.Uri]::UnescapeDataString($BaseUri.MakeRelativeUri($TargetUri).ToString()).Replace('\', '/')
    }
    catch {
        return $TargetFull.Replace('\', '/')
    }
}

function Get-RelativeProjectPath {
    param([Parameter(Mandatory = $true)][string]$FullName)
    return Convert-ToRelativePath -BasePath $ProjectRoot -TargetPath $FullName
}

function Invoke-WebRequestCompat {
    param(
        [Parameter(Mandatory = $true)][string]$Uri,
        [ValidateSet("GET", "POST")][string]$Method = "GET",
        [string]$ContentType = "application/json",
        [AllowNull()][string]$Body = $null,
        [int]$TimeoutSec = 30
    )

    $Parameters = @{
        Uri = $Uri
        Method = $Method
        TimeoutSec = $TimeoutSec
        ErrorAction = "Stop"
    }

    if ($PSVersionTable.PSVersion.Major -le 5) {
        $Parameters["UseBasicParsing"] = $true
    }

    if ($Method -eq "POST") {
        $Parameters["ContentType"] = $ContentType
        $Parameters["Body"] = $Body
    }

    return Invoke-WebRequest @Parameters
}

# ============================================================================
# PROTECTION DES DONNEES SENSIBLES
# ============================================================================

function Protect-SensitiveText {
    param([AllowEmptyString()][string]$Text)

    if ([string]::IsNullOrEmpty($Text)) { return $Text }

    $ProtectedText = $Text
    $Patterns = @(
        @{ Pattern = '(?im)(Password\s*=\s*)[^;''"\r\n]+'; Replacement = '$1***REDACTED***' },
        @{ Pattern = '(?im)("?(?:Password|ClientSecret|ApiKey|AccessToken|RefreshToken|Secret)"?\s*:\s*")[^"]+(")'; Replacement = '$1***REDACTED***$2' },
        @{ Pattern = '(?im)((?:Bearer|token)\s+)[A-Za-z0-9\-._~+/]+=*'; Replacement = '$1***REDACTED***' },
        @{ Pattern = '(?im)(Host=[^;\r\n]+;[^\r\n]*Password=)[^;\r\n]+'; Replacement = '$1***REDACTED***' },
        @{ Pattern = '(?im)(ConnectionStrings?[^\r\n]*Password[^=]*=)[^;\r\n]+'; Replacement = '$1***REDACTED***' }
    )

    foreach ($Item in $Patterns) {
        $ProtectedText = [regex]::Replace($ProtectedText, $Item.Pattern, $Item.Replacement)
    }

    return $ProtectedText
}

# ============================================================================
# UTILITAIRES FICHIERS
# ============================================================================

function Test-IsExcludedPath {
    param([Parameter(Mandatory = $true)][string]$FullName)

    $Relative = Get-RelativeProjectPath -FullName $FullName
    $Segments = $Relative -split '[\\/]'
    foreach ($Segment in $Segments) {
        if ($Segment -in $ExcludedDirectoryNames) { return $true }
    }
    return $false
}

function Test-IsTextFile {
    param([Parameter(Mandatory = $true)][System.IO.FileInfo]$File)

    if ($File.Length -gt 2MB) { return $false }
    if ($File.Name -in @("Dockerfile", "Directory.Build.props", "Directory.Build.targets")) { return $true }
    return $File.Extension.ToLowerInvariant() -in $TextExtensions
}

function Get-Sha256Safe {
    param([Parameter(Mandatory = $true)][string]$Path)

    try { return (Get-FileHash -LiteralPath $Path -Algorithm SHA256 -ErrorAction Stop).Hash }
    catch { return $null }
}

function Test-CommandAvailable {
    param([Parameter(Mandatory = $true)][string]$CommandName)
    return $null -ne (Get-Command -Name $CommandName -ErrorAction SilentlyContinue)
}

function Get-FileLanguage {
    param([System.IO.FileInfo]$File)

    switch ($File.Extension.ToLowerInvariant()) {
        ".cs" { return "csharp" }
        ".xaml" { return "xml" }
        ".xml" { return "xml" }
        ".csproj" { return "xml" }
        ".props" { return "xml" }
        ".targets" { return "xml" }
        ".json" { return "json" }
        ".ps1" { return "powershell" }
        ".sql" { return "sql" }
        ".md" { return "markdown" }
        ".yml" { return "yaml" }
        ".yaml" { return "yaml" }
        default { return "text" }
    }
}

# ============================================================================
# EXECUTION DES COMMANDES
# ============================================================================

function Invoke-DiagnosticCommand {
    param(
        [Parameter(Mandatory = $true)][string]$Title,
        [Parameter(Mandatory = $true)][string]$CommandDisplay,
        [Parameter(Mandatory = $true)][scriptblock]$Command,
        [ValidateRange(2, 6)][int]$HeadingLevel = 2,
        [switch]$TreatNonZeroAsError
    )

    Start-DiagnosticStep -Message $Title
    $CommandStart = Get-Date
    $Output = ""
    $ExitCode = 0

    try {
        $global:LASTEXITCODE = 0
        $Output = (& $Command 2>&1 | Out-String)
        $ExitCode = $LASTEXITCODE
        if ($null -eq $ExitCode) { $ExitCode = 0 }

        if ($ExitCode -eq 0) {
            Write-DiagnosticLog -Level "OK" -Message ("{0} termine en {1:n1} s" -f $Title, ((Get-Date) - $CommandStart).TotalSeconds)
        }
        elseif ($TreatNonZeroAsError) {
            $script:ErrorCount++
            Write-DiagnosticLog -Level "ERROR" -Message ("{0} termine avec le code {1}" -f $Title, $ExitCode)
        }
        else {
            $script:WarningCount++
            Write-DiagnosticLog -Level "WARN" -Message ("{0} termine avec le code {1}" -f $Title, $ExitCode)
        }
    }
    catch {
        $ExitCode = 1
        $script:ErrorCount++
        $Output = $_ | Out-String
        Write-DiagnosticLog -Level "ERROR" -Message ("Echec de {0}: {1}" -f $Title, $_.Exception.Message)
    }

    $Output = Protect-SensitiveText -Text $Output
    $Duration = [math]::Round(((Get-Date) - $CommandStart).TotalSeconds, 2)

    Add-MarkdownSection -Path $DiagnosticsFile -Title $Title -Level $HeadingLevel
    Add-MarkdownLine -Path $DiagnosticsFile -Text ("**Commande :** ``{0}``" -f $CommandDisplay)
    Add-MarkdownLine -Path $DiagnosticsFile
    Add-MarkdownLine -Path $DiagnosticsFile -Text ("**Code de sortie :** ``{0}``" -f $ExitCode)
    Add-MarkdownLine -Path $DiagnosticsFile -Text ("**Duree :** ``{0:n2} s``" -f $Duration)
    Add-MarkdownLine -Path $DiagnosticsFile
    Add-MarkdownCodeBlock -Path $DiagnosticsFile -Content $Output -Language "text"

    $script:StepResults.Add([pscustomobject]@{
        Title = $Title
        Command = $CommandDisplay
        ExitCode = $ExitCode
        DurationSeconds = $Duration
    })

    return [pscustomobject]@{ Output = $Output; ExitCode = $ExitCode; DurationSeconds = $Duration }
}

function Invoke-PostgresDiagnostic {
    param(
        [Parameter(Mandatory = $true)][string]$Title,
        [Parameter(Mandatory = $true)][string]$Sql
    )

    return Invoke-DiagnosticCommand `
        -Title $Title `
        -CommandDisplay "docker exec $PostgresContainer psql -U $PostgresUser -d $PostgresDatabase -c <SQL>" `
        -Command {
            & docker exec $PostgresContainer psql -X -A -F '|' -P footer=off -U $PostgresUser -d $PostgresDatabase -c $Sql
        }
}

# ============================================================================
# EN-TETES ET CONTEXTE FONCTIONNEL
# ============================================================================

Write-DiagnosticLog -Level "INFO" -Message "Initialisation de l'export KnowledgeHub v3"
Write-DiagnosticLog -Level "INFO" -Message ("PowerShell: {0}" -f $PSVersionTable.PSVersion)
Write-DiagnosticLog -Level "INFO" -Message ("Racine: {0}" -f $ProjectRoot)

Add-MarkdownLine -Path $ContextFile -Text "# KnowledgeHub - Contexte de reprise v3"
Add-MarkdownLine -Path $ContextFile
Add-MarkdownLine -Path $ContextFile -Text ("- **Genere le :** {0:yyyy-MM-dd HH:mm:ss zzz}" -f $StartTime)
Add-MarkdownLine -Path $ContextFile -Text ("- **Mode :** ``{0}``" -f $Mode)
Add-MarkdownLine -Path $ContextFile -Text ("- **Racine :** ``{0}``" -f $ProjectRoot)
Add-MarkdownLine -Path $ContextFile -Text ("- **PowerShell :** ``{0}``" -f $PSVersionTable.PSVersion)
Add-MarkdownLine -Path $ContextFile
Add-MarkdownLine -Path $ContextFile -Text "> Fichier destine a une nouvelle conversation Copilot. Les secrets detectables sont masques automatiquement."

Add-MarkdownLine -Path $DiagnosticsFile -Text "# KnowledgeHub - Diagnostic technique v3"
Add-MarkdownLine -Path $DiagnosticsFile
Add-MarkdownLine -Path $DiagnosticsFile -Text ("- **Genere le :** {0:yyyy-MM-dd HH:mm:ss zzz}" -f $StartTime)
Add-MarkdownLine -Path $DiagnosticsFile -Text ("- **Mode :** ``{0}``" -f $Mode)
Add-MarkdownLine -Path $DiagnosticsFile

Add-MarkdownSection -Path $ContextFile -Title "Description et objectifs" -Level 2
Add-MarkdownLine -Path $ContextFile -Text @"
KnowledgeHub est une solution .NET 10 composee d'une API ASP.NET Core, d'une application cliente .NET MAUI, de couches Domain/Application/Infrastructure, d'un importeur de corpus et de projets de tests. L'objectif est de fournir un assistant documentaire local de type RAG : import et decoupage des documents, generation d'embeddings, recherche semantique, construction du contexte, generation d'une reponse citee, puis presentation dans le client MAUI.
"@

Add-MarkdownSection -Path $ContextFile -Title "Architecture logique" -Level 2
Add-MarkdownCodeBlock -Path $ContextFile -Language "text" -Content @"
KnowledgeHub.Client (.NET MAUI)
  -> KnowledgeHub.Api (ASP.NET Core)
     -> KnowledgeHub.Application
     -> KnowledgeHub.Infrastructure
        -> PostgreSQL / EF Core
        -> Ollama / nomic-embed-text
        -> Ollama / qwen3:8b via /api/chat
     -> KnowledgeHub.Domain

Pipeline RAG:
Question -> embedding -> cache RAM -> similarite cosinus -> top documents
-> contexte -> Ollama /api/chat -> reponse + citations + sources
"@

Add-MarkdownSection -Path $ContextFile -Title "Travail deja effectue" -Level 2
@(
    "Solution .NET 10 structuree en couches Domain, Application, Infrastructure, API et Client MAUI.",
    "Import du corpus, decoupage en chunks et persistance PostgreSQL.",
    "Generation des embeddings avec nomic-embed-text via Ollama.",
    "Stockage actuel des embeddings en JSON texte mappe vers float[] par EF Core.",
    "Recherche semantique par similarite cosinus en C#.",
    "Deduplication des resultats par document.",
    "Construction d'un contexte documentaire avec scores de pertinence.",
    "Endpoint POST /api/rag/ask avec reponse et sources.",
    "Migration de /api/generate vers /api/chat.",
    "Gestion de message.content et message.thinking pour qwen3:8b.",
    "Citations, extraits et pourcentages de pertinence dans la reponse RAG.",
    "Activation de l'Intel Arc integree via Vulkan avec OLLAMA_IGPU_ENABLE=1.",
    "Cache memoire des embeddings et chunks avec warmup au demarrage.",
    "Integration du client RAG dans l'application MAUI initiee.",
    "Correction de la structure XAML de ChatPage effectuee.",
    "81 tests avaient ete observes comme reussis avant les derniers changements MAUI."
) | ForEach-Object { Add-MarkdownLine -Path $ContextFile -Text ("- {0}" -f $_) }

Add-MarkdownSection -Path $ContextFile -Title "Travail en cours" -Level 2
@(
    "Finalisation du ChatViewModel et de l'experience utilisateur MAUI.",
    "Validation de l'appel HTTP du client vers l'API selon la plateforme.",
    "Validation de l'affichage de la reponse, des sources, des erreurs et du chargement.",
    "Mesure de la latence reelle du pipeline RAG dans l'application cliente."
) | ForEach-Object { Add-MarkdownLine -Path $ContextFile -Text ("- {0}" -f $_) }

Add-MarkdownSection -Path $ContextFile -Title "Travail restant et pistes" -Level 2
@(
    "Finaliser ChatViewModel, navigation et injection de dependances MAUI.",
    "Gerer localhost sous Windows, 10.0.2.2 sous emulateur Android et l'adresse LAN sur appareil physique.",
    "Ajouter annulation, reprise, erreur utilisateur et timeout dans le chat.",
    "Ajouter l'historique conversationnel.",
    "Etudier le streaming des tokens.",
    "Evaluer un modele non-reasoning ou un backend plus configurable.",
    "Ajouter tests unitaires et d'integration pour le pipeline RAG et le client.",
    "Invalider ou reconstruire le cache lorsque le corpus change.",
    "Reconsiderer pgvector lorsque la volumetrie l'exigera."
) | ForEach-Object { Add-MarkdownLine -Path $ContextFile -Text ("- {0}" -f $_) }

Add-MarkdownSection -Path $ContextFile -Title "Points d'attention connus" -Level 2
@(
    "Verifier regulierement la coherence entre DocumentChunks et ChunkEmbeddings.",
    "Les tests d'integration peuvent creer des chunks sans embedding dans la base de developpement.",
    "Ollama utilise l'Intel Arc uniquement si OLLAMA_IGPU_ENABLE=1 est defini avant le demarrage.",
    "qwen3:8b peut consacrer une partie importante de num_predict au raisonnement.",
    "Le collecteur masque les secrets de maniere best-effort; une revue manuelle reste recommandee.",
    "Le script est compatible PowerShell 5.1 et 7+ et n'utilise pas Path.GetRelativePath."
) | ForEach-Object { Add-MarkdownLine -Path $ContextFile -Text ("- {0}" -f $_) }

# ============================================================================
# INVENTAIRE DES FICHIERS
# ============================================================================

Start-DiagnosticStep -Message "Inventaire des fichiers et checksums"
$AllFiles = Get-ChildItem -Path $ProjectRoot -Recurse -Force -ErrorAction SilentlyContinue |
    Where-Object { -not $_.PSIsContainer -and -not (Test-IsExcludedPath -FullName $_.FullName) } |
    Sort-Object FullName

$TreeEntries = New-Object 'System.Collections.Generic.List[object]'
foreach ($File in $AllFiles) {
    $RelativePath = Get-RelativeProjectPath -FullName $File.FullName
    $Directory = Split-Path $RelativePath -Parent
    if ($null -eq $Directory) { $Directory = "" }

    $TreeEntries.Add([pscustomobject]@{
        path = $RelativePath
        name = $File.Name
        directory = $Directory.Replace('\', '/')
        extension = if ([string]::IsNullOrWhiteSpace($File.Extension)) { $null } else { $File.Extension.ToLowerInvariant() }
        sizeBytes = [long]$File.Length
        sizeKiB = [math]::Round($File.Length / 1KB, 2)
        createdUtc = $File.CreationTimeUtc.ToString("o")
        modifiedUtc = $File.LastWriteTimeUtc.ToString("o")
        attributes = $File.Attributes.ToString()
        isReadOnly = $File.IsReadOnly
        sha256 = Get-Sha256Safe -Path $File.FullName
    })
}

$TotalSizeBytes = ($TreeEntries | Measure-Object -Property sizeBytes -Sum).Sum
if ($null -eq $TotalSizeBytes) { $TotalSizeBytes = 0 }

$TreeDocument = [ordered]@{
    schemaVersion = "3.0"
    generatedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    projectRoot = $ProjectRoot
    mode = $Mode
    excludedDirectories = $ExcludedDirectoryNames
    fileCount = $TreeEntries.Count
    totalSizeBytes = $TotalSizeBytes
    extensionSummary = @(
        $TreeEntries | Group-Object extension | Sort-Object Count -Descending | ForEach-Object {
            [pscustomobject]@{
                extension = if ([string]::IsNullOrWhiteSpace($_.Name)) { "<none>" } else { $_.Name }
                count = $_.Count
                totalSizeBytes = ($_.Group | Measure-Object -Property sizeBytes -Sum).Sum
            }
        }
    )
    files = $TreeEntries
}

$TreeDocument | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $TreeFile -Encoding UTF8
Write-DiagnosticLog -Level "OK" -Message ("Inventaire: {0} fichiers" -f $TreeEntries.Count)

Add-MarkdownSection -Path $ContextFile -Title "Synthese de l'arborescence" -Level 2
Add-MarkdownLine -Path $ContextFile -Text ("- **Fichiers inventories :** {0}" -f $TreeEntries.Count)
Add-MarkdownLine -Path $ContextFile -Text ("- **Taille totale :** {0:n2} MiB" -f ($TotalSizeBytes / 1MB))
Add-MarkdownLine -Path $ContextFile -Text ("- **Inventaire detaille :** ``{0}``" -f (Split-Path $TreeFile -Leaf))

# ============================================================================
# ENVIRONNEMENT, GIT, SOLUTION ET PACKAGES
# ============================================================================

Invoke-DiagnosticCommand -Title "Informations PowerShell" -CommandDisplay '$PSVersionTable; [Environment]::Version' -Command {
    $PSVersionTable | Format-List
    [pscustomobject]@{ DotNetRuntime = [System.Environment]::Version.ToString(); Is64BitProcess = [Environment]::Is64BitProcess } | Format-List
} | Out-Null

Invoke-DiagnosticCommand -Title "Informations .NET" -CommandDisplay "dotnet --info" -Command { & dotnet --info } | Out-Null
Invoke-DiagnosticCommand -Title "SDK .NET installes" -CommandDisplay "dotnet --list-sdks" -Command { & dotnet --list-sdks } | Out-Null
Invoke-DiagnosticCommand -Title "Runtimes .NET installes" -CommandDisplay "dotnet --list-runtimes" -Command { & dotnet --list-runtimes } | Out-Null
Invoke-DiagnosticCommand -Title "Workloads .NET" -CommandDisplay "dotnet workload list" -Command { & dotnet workload list } | Out-Null

Invoke-DiagnosticCommand -Title "Variables d'environnement utiles" -CommandDisplay "DOTNET*, NUGET*, OLLAMA*" -Command {
    Get-ChildItem Env: |
        Where-Object { $_.Name -like "DOTNET*" -or $_.Name -like "NUGET*" -or $_.Name -like "OLLAMA*" } |
        Sort-Object Name |
        Format-Table -AutoSize
} | Out-Null

if (Test-CommandAvailable -CommandName "git") {
    Invoke-DiagnosticCommand -Title "Git Version" -CommandDisplay "git --version" -Command { & git --version } | Out-Null
    Invoke-DiagnosticCommand -Title "Git Status" -CommandDisplay "git status --short --branch" -Command { & git status --short --branch } | Out-Null
    Invoke-DiagnosticCommand -Title "Git Branches" -CommandDisplay "git branch --all --verbose" -Command { & git branch --all --verbose } | Out-Null
    Invoke-DiagnosticCommand -Title "Git Log" -CommandDisplay "git log --oneline --decorate -50" -Command { & git log --oneline --decorate -50 } | Out-Null
    Invoke-DiagnosticCommand -Title "Git Remotes" -CommandDisplay "git remote -v" -Command { & git remote -v } | Out-Null
    Invoke-DiagnosticCommand -Title "Git Diff Stat" -CommandDisplay "git diff --stat" -Command { & git diff --stat } | Out-Null
    Invoke-DiagnosticCommand -Title "Git Diff noms" -CommandDisplay "git diff --name-status" -Command { & git diff --name-status } | Out-Null
}

$SolutionFile = Get-ChildItem -Path $ProjectRoot -Filter "*.sln*" -ErrorAction SilentlyContinue | Where-Object { -not $_.PSIsContainer } | Select-Object -First 1
if ($null -ne $SolutionFile) {
    Invoke-DiagnosticCommand -Title "Projets de la solution" -CommandDisplay ("dotnet sln `"{0}`" list" -f $SolutionFile.Name) -Command { & dotnet sln $SolutionFile.FullName list } | Out-Null
}

Invoke-DiagnosticCommand -Title "Packages NuGet" -CommandDisplay "dotnet list package --include-transitive" -Command { & dotnet list package --include-transitive } | Out-Null
Invoke-DiagnosticCommand -Title "Packages vulnerables" -CommandDisplay "dotnet list package --vulnerable --include-transitive" -Command { & dotnet list package --vulnerable --include-transitive } | Out-Null
if ($Mode -eq "Full") {
    Invoke-DiagnosticCommand -Title "Packages obsoletes" -CommandDisplay "dotnet list package --outdated --include-transitive" -Command { & dotnet list package --outdated --include-transitive } | Out-Null
}

# ============================================================================
# CLEAN, RESTORE, BUILD, TESTS ET COVERAGE
# ============================================================================

if (-not $SkipBuild) {
    Invoke-DiagnosticCommand -Title "Clean" -CommandDisplay "dotnet clean" -Command { & dotnet clean } | Out-Null
    Invoke-DiagnosticCommand -Title "Restore" -CommandDisplay "dotnet restore" -Command { & dotnet restore } | Out-Null
    Invoke-DiagnosticCommand -Title "Build solution" -CommandDisplay "dotnet build --no-restore" -Command { & dotnet build --no-restore } | Out-Null

    foreach ($Project in @("KnowledgeHub.Domain", "KnowledgeHub.Application", "KnowledgeHub.Infrastructure", "KnowledgeHub.Api")) {
        $ProjectFile = Join-Path $ProjectRoot (Join-Path $Project ($Project + ".csproj"))
        if (Test-Path -LiteralPath $ProjectFile) {
            Invoke-DiagnosticCommand -Title ("Build isole - {0}" -f $Project) -CommandDisplay ("dotnet build {0} --no-restore" -f $ProjectFile) -Command {
                & dotnet build $ProjectFile --no-restore
            } | Out-Null
        }
    }
}

if (-not $SkipTests) {
    Invoke-DiagnosticCommand -Title "Tests" -CommandDisplay 'dotnet test --no-restore --logger "console;verbosity=normal"' -Command {
        & dotnet test --no-restore --logger "console;verbosity=normal"
    } | Out-Null
}

if (-not $SkipCoverage) {
    Invoke-DiagnosticCommand -Title "Collecte de la couverture" -CommandDisplay 'dotnet test --collect:"XPlat Code Coverage"' -Command {
        & dotnet test --no-restore --collect:"XPlat Code Coverage" --results-directory $CoverageDirectory --logger "console;verbosity=minimal"
    } | Out-Null

    $CoverageFiles = @(Get-ChildItem -Path $CoverageDirectory -Recurse -Filter "coverage.cobertura.xml" -ErrorAction SilentlyContinue | Where-Object { -not $_.PSIsContainer })
    Add-MarkdownSection -Path $DiagnosticsFile -Title "Couverture de code" -Level 2

    if ($CoverageFiles.Count -eq 0) {
        Add-MarkdownLine -Path $DiagnosticsFile -Text "Aucun fichier coverage.cobertura.xml trouve. Verifier coverlet.collector."
    }
    else {
        foreach ($CoverageFile in $CoverageFiles) {
            Add-MarkdownLine -Path $DiagnosticsFile -Text ("- ``{0}``" -f (Get-RelativeProjectPath -FullName $CoverageFile.FullName))
        }

        $ReportGeneratorCommand = Get-Command -Name "reportgenerator" -ErrorAction SilentlyContinue
        $LocalReportGenerator = Join-Path $env:USERPROFILE ".dotnet\tools\reportgenerator.exe"
        $ReportGeneratorPath = if ($null -ne $ReportGeneratorCommand) { $ReportGeneratorCommand.Source } elseif (Test-Path $LocalReportGenerator) { $LocalReportGenerator } else { $null }

        if ($null -ne $ReportGeneratorPath) {
            $ReportsArgument = ($CoverageFiles.FullName -join ';')
            Invoke-DiagnosticCommand -Title "Generation du rapport de couverture" -CommandDisplay "reportgenerator MarkdownSummary;JsonSummary" -Command {
                & $ReportGeneratorPath "-reports:$ReportsArgument" "-targetdir:$CoverageReportDirectory" "-reporttypes:MarkdownSummary;JsonSummary"
            } | Out-Null

            $SummaryMarkdown = Join-Path $CoverageReportDirectory "Summary.md"
            if (Test-Path -LiteralPath $SummaryMarkdown) {
                Add-MarkdownSection -Path $DiagnosticsFile -Title "Synthese de la couverture" -Level 2
                Add-MarkdownLine -Path $DiagnosticsFile -Text (Get-Content -LiteralPath $SummaryMarkdown -Raw)
            }
        }
        else {
            Add-MarkdownLine -Path $DiagnosticsFile -Text "> ReportGenerator absent. Installation: ``dotnet tool install --global dotnet-reportgenerator-globaltool``"
        }
    }
}

# ============================================================================
# DOCKER ET POSTGRESQL
# ============================================================================

if (-not $SkipDocker -and (Test-CommandAvailable -CommandName "docker")) {
    Invoke-DiagnosticCommand -Title "Docker Version" -CommandDisplay "docker version" -Command { & docker version } | Out-Null
    Invoke-DiagnosticCommand -Title "Docker Info" -CommandDisplay "docker info" -Command { & docker info } | Out-Null
    Invoke-DiagnosticCommand -Title "Docker Containers" -CommandDisplay "docker ps -a --no-trunc" -Command { & docker ps -a --no-trunc } | Out-Null
    Invoke-DiagnosticCommand -Title "Docker Images" -CommandDisplay "docker images --digests" -Command { & docker images --digests } | Out-Null
    Invoke-DiagnosticCommand -Title "Docker Compose Config" -CommandDisplay "docker compose config" -Command { & docker compose config } | Out-Null
    Invoke-DiagnosticCommand -Title "PostgreSQL container inspect" -CommandDisplay ("docker inspect {0}" -f $PostgresContainer) -Command { & docker inspect $PostgresContainer } | Out-Null
    Invoke-DiagnosticCommand -Title "PostgreSQL container logs" -CommandDisplay ("docker logs --tail 200 {0}" -f $PostgresContainer) -Command { & docker logs --tail 200 $PostgresContainer } | Out-Null

    Invoke-PostgresDiagnostic -Title "PostgreSQL - version et base" -Sql 'SELECT version(); SELECT current_database();' | Out-Null
    Invoke-PostgresDiagnostic -Title "PostgreSQL - extensions" -Sql 'SELECT extname, extversion FROM pg_extension ORDER BY extname;' | Out-Null
    Invoke-PostgresDiagnostic -Title "PostgreSQL - tables" -Sql "SELECT table_name FROM information_schema.tables WHERE table_schema='public' ORDER BY table_name;" | Out-Null
    Invoke-PostgresDiagnostic -Title "PostgreSQL - migrations EF" -Sql 'SELECT * FROM "__EFMigrationsHistory" ORDER BY "MigrationId";' | Out-Null
    Invoke-PostgresDiagnostic -Title "PostgreSQL - type Embedding" -Sql "SELECT data_type, udt_name FROM information_schema.columns WHERE table_schema='public' AND table_name='ChunkEmbeddings' AND column_name='Embedding';" | Out-Null
    Invoke-PostgresDiagnostic -Title "PostgreSQL - tailles des tables" -Sql "SELECT relname AS table_name, pg_size_pretty(pg_total_relation_size(relid)) AS total_size FROM pg_catalog.pg_statio_user_tables ORDER BY pg_total_relation_size(relid) DESC;" | Out-Null
    Invoke-PostgresDiagnostic -Title "PostgreSQL - index" -Sql "SELECT tablename, indexname, indexdef FROM pg_indexes WHERE schemaname='public' ORDER BY tablename, indexname;" | Out-Null

    $MetricsSql = @'
SELECT
    (SELECT COUNT(*) FROM "Documents") AS documents,
    (SELECT COUNT(*) FROM "DocumentChunks") AS chunks,
    (SELECT COUNT(*) FROM "ChunkEmbeddings") AS embeddings,
    (SELECT COUNT(*) FROM "DocumentChunks" dc LEFT JOIN "ChunkEmbeddings" ce ON ce."ChunkId" = dc."Id" WHERE ce."ChunkId" IS NULL) AS chunks_without_embedding,
    (SELECT COUNT(*) FROM "ChunkEmbeddings" ce LEFT JOIN "DocumentChunks" dc ON dc."Id" = ce."ChunkId" WHERE dc."Id" IS NULL) AS orphan_embeddings,
    (SELECT COUNT(*) FROM "DocumentChunks") - (SELECT COUNT(*) FROM "ChunkEmbeddings") AS raw_difference;
'@

    $MetricsResult = Invoke-PostgresDiagnostic -Title "PostgreSQL - coherence RAG" -Sql $MetricsSql
    $script:DatabaseMetrics = $MetricsResult.Output

    Invoke-PostgresDiagnostic -Title "PostgreSQL - chunks sans embedding" -Sql 'SELECT dc."Id", dc."DocumentId", dc."ChunkIndex", LEFT(dc."Content", 120) AS preview FROM "DocumentChunks" dc LEFT JOIN "ChunkEmbeddings" ce ON ce."ChunkId" = dc."Id" WHERE ce."ChunkId" IS NULL ORDER BY dc."CreatedAt" DESC LIMIT 100;' | Out-Null
    Invoke-PostgresDiagnostic -Title "PostgreSQL - embeddings orphelins" -Sql 'SELECT ce."Id", ce."ChunkId", ce."CreatedAt" FROM "ChunkEmbeddings" ce LEFT JOIN "DocumentChunks" dc ON dc."Id" = ce."ChunkId" WHERE dc."Id" IS NULL ORDER BY ce."CreatedAt" DESC LIMIT 100;' | Out-Null
    Invoke-PostgresDiagnostic -Title "PostgreSQL - repartition des chunks" -Sql 'SELECT MIN("TokenCount") AS min_tokens, MAX("TokenCount") AS max_tokens, ROUND(AVG("TokenCount")::numeric, 2) AS avg_tokens, COUNT(*) AS chunks FROM "DocumentChunks";' | Out-Null
}

# ============================================================================
# OLLAMA
# ============================================================================

if (-not $SkipOllama -and (Test-CommandAvailable -CommandName "ollama")) {
    Invoke-DiagnosticCommand -Title "Ollama Version" -CommandDisplay "ollama --version" -Command { & ollama --version } | Out-Null
    Invoke-DiagnosticCommand -Title "Ollama Models" -CommandDisplay "ollama list" -Command { & ollama list } | Out-Null
    Invoke-DiagnosticCommand -Title "Ollama Processes" -CommandDisplay "ollama ps" -Command { & ollama ps } | Out-Null
    Invoke-DiagnosticCommand -Title "Variables Ollama" -CommandDisplay "Get-ChildItem Env:OLLAMA*" -Command {
        Get-ChildItem Env: | Where-Object { $_.Name -like "OLLAMA*" } | Sort-Object Name | Format-Table -AutoSize
    } | Out-Null

    Invoke-DiagnosticCommand -Title "Materiel graphique" -CommandDisplay "Win32_VideoController" -Command {
        Get-CimInstance Win32_VideoController | Select-Object Name, DriverVersion, AdapterRAM, VideoProcessor | Format-List
    } | Out-Null

    Invoke-DiagnosticCommand -Title "Processeur et NPU Intel" -CommandDisplay "Win32_Processor / Win32_PnPEntity" -Command {
        Get-CimInstance Win32_Processor | Select-Object Name, NumberOfCores, NumberOfLogicalProcessors | Format-List
        Get-CimInstance Win32_PnPEntity | Where-Object { $_.Name -match 'AI Boost|NPU|Arc' } | Select-Object Name, Status | Format-Table -AutoSize
    } | Out-Null

    try {
        $TagsResponse = Invoke-WebRequestCompat -Uri "http://127.0.0.1:11434/api/tags" -Method GET -TimeoutSec 10
        Add-MarkdownSection -Path $DiagnosticsFile -Title "Ollama API tags" -Level 2
        Add-MarkdownCodeBlock -Path $DiagnosticsFile -Content (Protect-SensitiveText -Text $TagsResponse.Content) -Language "json"
    }
    catch {
        Add-MarkdownSection -Path $DiagnosticsFile -Title "Ollama API tags" -Level 2
        Add-MarkdownLine -Path $DiagnosticsFile -Text ("Ollama API inaccessible: {0}" -f $_.Exception.Message)
    }
}

# ============================================================================
# API ET PROBE RAG AVEC METRIQUES
# ============================================================================

if (-not $SkipApiProbe) {
    Start-DiagnosticStep -Message "Probe API et RAG"
    Add-MarkdownSection -Path $DiagnosticsFile -Title "Probe API et RAG" -Level 2

    try {
        $RootWatch = [System.Diagnostics.Stopwatch]::StartNew()
        $RootResponse = Invoke-WebRequestCompat -Uri $ApiBaseUrl -Method GET -TimeoutSec 15
        $RootWatch.Stop()
        Add-MarkdownLine -Path $DiagnosticsFile -Text ("- **API racine :** HTTP {0}, {1:n0} ms" -f [int]$RootResponse.StatusCode, $RootWatch.Elapsed.TotalMilliseconds)
    }
    catch {
        Add-MarkdownLine -Path $DiagnosticsFile -Text ("- **API racine :** inaccessible - {0}" -f $_.Exception.Message)
    }

    try {
        $RagUri = $ApiBaseUrl.TrimEnd('/') + "/api/rag/ask"
        $RequestBody = @{ question = $RagQuestion } | ConvertTo-Json
        $RagWatch = [System.Diagnostics.Stopwatch]::StartNew()
        $RagResponse = Invoke-WebRequestCompat -Uri $RagUri -Method POST -Body $RequestBody -ContentType "application/json" -TimeoutSec 300
        $RagWatch.Stop()

        $RagJson = $RagResponse.Content | ConvertFrom-Json
        $AnswerText = [string]$RagJson.answer
        $SourceCount = @($RagJson.sources).Count
        $TopScore = $null
        if ($SourceCount -gt 0) { $TopScore = $RagJson.sources[0].score }

        $script:RagProbeResult = [pscustomobject]@{
            StatusCode = [int]$RagResponse.StatusCode
            DurationMilliseconds = [math]::Round($RagWatch.Elapsed.TotalMilliseconds, 0)
            AnswerLength = $AnswerText.Length
            SourceCount = $SourceCount
            TopScore = $TopScore
            Question = $RagQuestion
        }

        Add-MarkdownLine -Path $DiagnosticsFile -Text ("- **Endpoint :** ``POST {0}``" -f $RagUri)
        Add-MarkdownLine -Path $DiagnosticsFile -Text ("- **HTTP :** ``{0}``" -f [int]$RagResponse.StatusCode)
        Add-MarkdownLine -Path $DiagnosticsFile -Text ("- **Duree totale :** ``{0:n0} ms``" -f $RagWatch.Elapsed.TotalMilliseconds)
        Add-MarkdownLine -Path $DiagnosticsFile -Text ("- **Longueur de reponse :** ``{0}`` caracteres" -f $AnswerText.Length)
        Add-MarkdownLine -Path $DiagnosticsFile -Text ("- **Sources :** ``{0}``" -f $SourceCount)
        if ($null -ne $TopScore) { Add-MarkdownLine -Path $DiagnosticsFile -Text ("- **Meilleur score :** ``{0:n4}``" -f [double]$TopScore) }
        Add-MarkdownLine -Path $DiagnosticsFile
        Add-MarkdownCodeBlock -Path $DiagnosticsFile -Content (Protect-SensitiveText -Text ($RagJson | ConvertTo-Json -Depth 20)) -Language "json"
        Write-DiagnosticLog -Level "OK" -Message ("Probe RAG reussi en {0:n1} s" -f $RagWatch.Elapsed.TotalSeconds)
    }
    catch {
        $script:WarningCount++
        Add-MarkdownLine -Path $DiagnosticsFile -Text ("- **RAG :** inaccessible ou en erreur - {0}" -f $_.Exception.Message)
        Write-DiagnosticLog -Level "WARN" -Message ("Probe RAG impossible: {0}" -f $_.Exception.Message)
    }
}

Add-MarkdownSection -Path $ContextFile -Title "Metriques RAG observees" -Level 2
if ($null -ne $script:RagProbeResult) {
    Add-MarkdownLine -Path $ContextFile -Text ("- **Question de test :** {0}" -f $script:RagProbeResult.Question)
    Add-MarkdownLine -Path $ContextFile -Text ("- **HTTP :** {0}" -f $script:RagProbeResult.StatusCode)
    Add-MarkdownLine -Path $ContextFile -Text ("- **Duree de bout en bout :** {0:n0} ms" -f $script:RagProbeResult.DurationMilliseconds)
    Add-MarkdownLine -Path $ContextFile -Text ("- **Longueur de la reponse :** {0} caracteres" -f $script:RagProbeResult.AnswerLength)
    Add-MarkdownLine -Path $ContextFile -Text ("- **Nombre de sources :** {0}" -f $script:RagProbeResult.SourceCount)
    if ($null -ne $script:RagProbeResult.TopScore) { Add-MarkdownLine -Path $ContextFile -Text ("- **Meilleur score :** {0:n4}" -f [double]$script:RagProbeResult.TopScore) }
}
else {
    Add-MarkdownLine -Path $ContextFile -Text "- Aucun probe RAG reussi pendant cette collecte."
}

if ($null -ne $script:DatabaseMetrics) {
    Add-MarkdownLine -Path $ContextFile -Text "- **Coherence base RAG :** voir la sortie SQL dans KnowledgeHub-Diagnostics.md."
}

# ============================================================================
# FICHIERS CRITIQUES ET CONFIGURATION
# ============================================================================

Add-MarkdownSection -Path $ContextFile -Title "Fichiers critiques" -Level 2
Add-MarkdownLine -Path $ContextFile -Text "Les contenus critiques incluent les entites, DTO, interfaces, services, controleurs, composants RAG, configuration EF et composants MAUI."

$CriticalRelativePaths = @(
    "KnowledgeHub.Domain/Entities/DocumentChunk.cs",
    "KnowledgeHub.Domain/Entities/KnowledgeDocument.cs",
    "KnowledgeHub.Domain/Entities/ChunkEmbedding.cs",
    "KnowledgeHub.Infrastructure/Persistence/Configurations/DocumentChunkConfiguration.cs",
    "KnowledgeHub.Infrastructure/Persistence/Configurations/ChunkEmbeddingConfiguration.cs",
    "KnowledgeHub.Infrastructure/Persistence/Context/KnowledgeDbContext.cs",
    "KnowledgeHub.Application/DTOs/CreateDocumentDto.cs",
    "KnowledgeHub.Application/DTOs/DocumentDto.cs",
    "KnowledgeHub.Application/DTOs/AskQuestionRequestDto.cs",
    "KnowledgeHub.Application/DTOs/AskQuestionResponseDto.cs",
    "KnowledgeHub.Application/DTOs/SourceReferenceDto.cs",
    "KnowledgeHub.Application/DTOs/VectorSearchRequestDto.cs",
    "KnowledgeHub.Application/DTOs/VectorSearchResultDto.cs",
    "KnowledgeHub.Application/Interfaces/IDocumentService.cs",
    "KnowledgeHub.Application/Interfaces/IEmbeddingService.cs",
    "KnowledgeHub.Application/Interfaces/IVectorSearchService.cs",
    "KnowledgeHub.Application/Interfaces/IContextBuilderService.cs",
    "KnowledgeHub.Application/Interfaces/IChatCompletionService.cs",
    "KnowledgeHub.Application/Services/DocumentService.cs",
    "KnowledgeHub.Api/Controllers/DocumentsController.cs",
    "KnowledgeHub.Api/Controllers/VectorSearchController.cs",
    "KnowledgeHub.Api/Controllers/ContextController.cs",
    "KnowledgeHub.Api/Controllers/RagController.cs",
    "KnowledgeHub.Api/Program.cs",
    "KnowledgeHub.Infrastructure/AI/OllamaEmbeddingService.cs",
    "KnowledgeHub.Infrastructure/AI/VectorSearchService.cs",
    "KnowledgeHub.Infrastructure/AI/ContextBuilderService.cs",
    "KnowledgeHub.Infrastructure/AI/OllamaChatCompletionService.cs",
    "KnowledgeHub.Infrastructure/AI/RagService.cs",
    "KnowledgeHub.Infrastructure/AI/EmbeddingCacheWarmupService.cs",
    "KnowledgeHub.Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs",
    "KnowledgeHub.Client/Models/AskQuestionRequestDto.cs",
    "KnowledgeHub.Client/Models/AskQuestionResponseDto.cs",
    "KnowledgeHub.Client/Models/SourceReferenceDto.cs",
    "KnowledgeHub.Client/Services/RagApiClient.cs",
    "KnowledgeHub.Client/ViewModels/ChatViewModel.cs",
    "KnowledgeHub.Client/Views/ChatPage.xaml",
    "KnowledgeHub.Client/Views/ChatPage.xaml.cs",
    "KnowledgeHub.Client/MauiProgram.cs",
    "KnowledgeHub.Client/AppShell.xaml",
    "KnowledgeHub.Client/AppShell.xaml.cs",
    "docker-compose.yml",
    "docker-compose.yaml",
    "Directory.Build.props",
    "Directory.Build.targets",
    "README.md"
)

if ($Mode -eq "Full") {
    $AdditionalPatterns = @("*Controller.cs", "I*Service.cs", "*Service.cs", "*Configuration.cs", "*DbContext.cs", "*ViewModel.cs", "*Page.xaml", "*Page.xaml.cs")
    foreach ($Pattern in $AdditionalPatterns) {
        Get-ChildItem -Path $ProjectRoot -Recurse -Filter $Pattern -ErrorAction SilentlyContinue |
            Where-Object { -not $_.PSIsContainer -and -not (Test-IsExcludedPath -FullName $_.FullName) } |
            ForEach-Object {
                $Relative = Get-RelativeProjectPath -FullName $_.FullName
                if ($Relative -notin $CriticalRelativePaths) { $CriticalRelativePaths += $Relative }
            }
    }
}

$CriticalRelativePaths = $CriticalRelativePaths | Sort-Object -Unique
$IncludedCriticalCount = 0
$CriticalTotalBytes = 0
$MaxCriticalBytes = if ($Mode -eq "Full") { 4MB } else { 1MB }

foreach ($RelativePath in $CriticalRelativePaths) {
    $NormalizedRelativePath = $RelativePath.Replace('/', [System.IO.Path]::DirectorySeparatorChar)
    $FullPath = Join-Path $ProjectRoot $NormalizedRelativePath
    if (-not (Test-Path -LiteralPath $FullPath -PathType Leaf)) { continue }

    $File = Get-Item -LiteralPath $FullPath
    if (-not (Test-IsTextFile -File $File)) { continue }
    if (($CriticalTotalBytes + $File.Length) -gt $MaxCriticalBytes) {
        Add-MarkdownLine -Path $ContextFile -Text ("> Limite de taille atteinte; fichier non inclus: ``{0}``" -f $RelativePath)
        continue
    }

    $Content = Protect-SensitiveText -Text (Get-Content -LiteralPath $FullPath -Raw -ErrorAction SilentlyContinue)
    Add-MarkdownSection -Path $ContextFile -Title $RelativePath -Level 3
    Add-MarkdownLine -Path $ContextFile -Text ("- **Taille :** {0:n0} octets" -f $File.Length)
    Add-MarkdownLine -Path $ContextFile -Text ("- **Cree :** {0:o}" -f $File.CreationTimeUtc)
    Add-MarkdownLine -Path $ContextFile -Text ("- **Modifie :** {0:o}" -f $File.LastWriteTimeUtc)
    Add-MarkdownLine -Path $ContextFile -Text ("- **SHA-256 :** ``{0}``" -f (Get-Sha256Safe -Path $File.FullName))
    Add-MarkdownLine -Path $ContextFile
    Add-MarkdownCodeBlock -Path $ContextFile -Content $Content -Language (Get-FileLanguage -File $File)
    $IncludedCriticalCount++
    $CriticalTotalBytes += $File.Length
}

Add-MarkdownLine -Path $ContextFile
Add-MarkdownLine -Path $ContextFile -Text ("> Fichiers critiques inclus: {0}; volume source: {1:n2} MiB." -f $IncludedCriticalCount, ($CriticalTotalBytes / 1MB))

Add-MarkdownSection -Path $ContextFile -Title "Configurations projet" -Level 2
$ProjectConfigurationFiles = Get-ChildItem -Path $ProjectRoot -Recurse -ErrorAction SilentlyContinue |
    Where-Object {
        -not $_.PSIsContainer -and -not (Test-IsExcludedPath -FullName $_.FullName) -and
        ($_.Extension -in @(".csproj", ".props", ".targets", ".sln", ".slnx") -or $_.Name -in @("Dockerfile", "docker-compose.yml", "docker-compose.yaml"))
    } | Sort-Object FullName

foreach ($File in $ProjectConfigurationFiles) {
    $RelativePath = Get-RelativeProjectPath -FullName $File.FullName
    $Content = Protect-SensitiveText -Text (Get-Content -LiteralPath $File.FullName -Raw -ErrorAction SilentlyContinue)
    Add-MarkdownSection -Path $ContextFile -Title $RelativePath -Level 3
    Add-MarkdownCodeBlock -Path $ContextFile -Content $Content -Language (Get-FileLanguage -File $File)
}

# ============================================================================
# RESUME FINAL
# ============================================================================

$EndTime = Get-Date
$Duration = $EndTime - $StartTime

Add-MarkdownSection -Path $DiagnosticsFile -Title "Resume d'execution" -Level 2
Add-MarkdownLine -Path $DiagnosticsFile -Text ("- **Etapes :** {0}" -f $script:StepNumber)
Add-MarkdownLine -Path $DiagnosticsFile -Text ("- **Avertissements :** {0}" -f $script:WarningCount)
Add-MarkdownLine -Path $DiagnosticsFile -Text ("- **Erreurs techniques :** {0}" -f $script:ErrorCount)
Add-MarkdownLine -Path $DiagnosticsFile -Text ("- **Duree totale :** {0:hh\:mm\:ss}" -f $Duration)
Add-MarkdownLine -Path $DiagnosticsFile
Add-MarkdownLine -Path $DiagnosticsFile -Text "| Etape | Code | Duree (s) |"
Add-MarkdownLine -Path $DiagnosticsFile -Text "|---|---:|---:|"
foreach ($Step in $script:StepResults) {
    Add-MarkdownLine -Path $DiagnosticsFile -Text ("| {0} | {1} | {2:n2} |" -f $Step.Title.Replace('|', '/'), $Step.ExitCode, $Step.DurationSeconds)
}

Write-Host
Write-Host "============================================================" -ForegroundColor Green
Write-DiagnosticLog -Level "DONE" -Message "Export KnowledgeHub v3 termine"
Write-Host ("1. {0}" -f $ContextFile) -ForegroundColor Green
Write-Host ("2. {0}" -f $TreeFile) -ForegroundColor Green
Write-Host ("3. {0}" -f $DiagnosticsFile) -ForegroundColor Green
Write-Host ("Duree: {0:hh\:mm\:ss}" -f $Duration) -ForegroundColor Green
Write-Host "============================================================" -ForegroundColor Green

Remove-Item -LiteralPath $TemporaryDirectory -Recurse -Force -ErrorAction SilentlyContinue

if ($FailOnDiagnosticError -and $script:ErrorCount -gt 0) { exit 1 }
exit 0
