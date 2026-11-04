# ARCH-A1.2 — Preparation des assemblies et du gate G1

## Etat et perimetre

- Base de reference : `main@c423081159136d93078377bab1d5d3c2739251e1`, arbre `51e24af477138e0ae92ccccb07e0e9a01aa7dfdb`.
- Six projets neufs : quatre bibliotheques et deux projets xUnit autonomes.
- Aucun fichier C# existant, DTO serveur, controleur, fichier DI, migration, ou service KH008 n'est modifie.
- Les nouvelles bibliotheques sont volontairement sans type public : elles sont des coquilles architecturales, pas une fonctionnalite livree.
- Cette preparation ne represente ni le gate G1 accepte ni le commit `I0`. `prepared_base_sha` reste `null`.
- La norme `1.0.0-rc.1` a ete approuvee. Le candidat `1.1.0-rc.1` reste soumis a approbation explicite, notamment ses limites et implications de compatibilite.

## Graphe direct requis a A1.2

| Projet | Reference(s) directes |
|---|---|
| `KnowledgeHub.Contracts.Http` | Aucune |
| `KnowledgeHub.Client.Http` | `KnowledgeHub.Contracts.Http` |
| `KnowledgeHub.Rag.Contracts` | Aucune |
| `KnowledgeHub.Verification` | `KnowledgeHub.Rag.Contracts` |
| `KnowledgeHub.Client.Http.Tests` | `KnowledgeHub.Client.Http`, `KnowledgeHub.Contracts.Http` |
| `KnowledgeHub.Verification.Tests` | `KnowledgeHub.Verification`, `KnowledgeHub.Rag.Contracts` |

Les projets historiques ne gagnent aucune nouvelle reference durant A1.2. Aucun lien entre les deux families de contrats, aucune dependance ASP.NET/EF/UI/Ollama des quatre bibliotheques.

## Carte de migration et proprietes

La carte structuree `ARCH-A1.2-type-movement-v1.json` decrit les **23** types HTTP, les **16** valeurs RAG et les **3** ports publics retenus, depuis la norme `ARCH-A0.2-contrats-v1.json`. Pour chaque symbole : source archivee, assembly actuel, assembly cible et phase, chemin cible **propose** mais non cree, consommateurs candidats par recherche lexicale. Ces consommateurs doivent etre valides lors de la bascule ; un match textuel n'est pas une resolution du compilateur. Les `cutover_commit_sha` restent `null`.

- A2.1 : creer les DTO HTTP et fixtures JSON reels ; normalisation API avant raccordement SDK.
- A2.2 : realiser Client.Http, handlers et qualification transport.
- A3.1 : extraire les valeurs et trois ports publics dans Rag.Contracts en un changement coordonne sans double definition CLR.
- A3.2 : deplacer parser, ancres, politique et verdict dans Verification ; `IEvidenceSnapshotProvider` demeure dans Application, `IVerifiedVerdictEvaluator` n'est pas expose en v1.
- Les consommateurs, namespaces, proprietaires, fixtures wire, anciennes definitions et changements de DI sont revises/valides au gate ; ils ne sont pas modifies par A1.2.

## Fixtures de structure (pas d'API fictive)

- `KnowledgeHub.Client.Http.Tests/Fixtures/architecture.json` : operations SDK futures, aucun changement du protocole HTTP, aucune qualification filaire revendiquee.
- `KnowledgeHub.Verification.Tests/Fixtures/architecture.json` : absence d'assessor qualifie, obligation ABSTAIN, pas de deplacement KH008.

Les vraies fixtures HTTP (17 cas normatifs) et les caracterisations KH008 ne sont pas transposees a ce jalon. Les tests xUnit couvrent des **invariants architecturaux**, pas des operations fonctionnelles.

## Resolution du depot lors des tests externes (Windows PowerShell)

Les deux suites xUnit lisent leurs fixtures et les fichiers de projet depuis le depot.
Avec `dotnet test --artifacts-path` place sous `Downloads`, les assemblies executees
sont hors de l'arborescence source. Le chemin de travail des tests ne permet alors
pas de decouvrir `KnowledgeHub.slnx` en remontant les dossiers.

Definir **avant chaque lancement de tests** la variable d'environnement de processus
`KNOWLEDGEHUB_REPOSITORY_ROOT` vers la racine du depot verifiee. Exemple depuis le depot :

```powershell
$env:KNOWLEDGEHUB_REPOSITORY_ROOT = (Resolve-Path '.').Path
$artifacts = Join-Path $HOME 'Downloads\KH-A1.2-architecture-artifacts'

dotnet test 'KnowledgeHub.Client.Http.Tests/KnowledgeHub.Client.Http.Tests.csproj' `
    -c Release --artifacts-path $artifacts --nologo

dotnet test 'KnowledgeHub.Verification.Tests/KnowledgeHub.Verification.Tests.csproj' `
    -c Release --artifacts-path $artifacts --nologo
```

La variable est lue par les deux helpers `RepositoryRoot()`. Les scripts de qualification
doivent la propager a chaque processus `dotnet test` ; `--artifacts-path` seul n'est pas
suffisant. Ne pas creer de faux `KnowledgeHub.slnx` dans les artefacts.

## Conditions de qualification locale

1. Preflight du depot Windows (origin, main, HEAD, arbre, `git status` propre), sans reset/clean/pull automatiques.
2. Appliquer seulement ce diff sur une nouvelle branche locale de travail avec `git apply --check` au prealable.
3. Construire les quatre assemblies et l'API/importeur (sans `KnowledgeHub.Client` MAUI), puis executer les deux projets de tests nouveaux et la suite unitaire historique. Utiliser un `--artifacts-path` distinct sous Telechargements.
4. Les integrations PostgreSQL Testcontainers restent disponibles en option sur Windows, sans lancer de `database update` sur la base personnelle. Leur statut doit etre mentionne explicitement avant G1.
5. Verifier statuts de sortie, TRX des suites, chemins touches, invariants de reference et absence de changement de migration ; conserver les rapports hors depot.
6. Revue du diff et commit par le mainteneur seulement ; sur un poste sans cle GPG, un commit non signe doit etre identifie explicitement et reste soumis aux protections GitHub. `G1/I0` necessitera une approbation distincte liee au SHA/arbre testes, a la norme finale et a la cartographie des proprietaires/consommateurs.

## Hors lot et dettes conservees

A11-COD20-01/02/03 et A11-OBS-04 restent ouverts/differes : le validateur A1.1 precommit n'est **pas** relance sur cette base. COR-001 (OIDC) et COR-002 (EF/SQL Reorder) restent des observations statiques pour leurs parcours cibles. KH009.2-.6, MAUI, Avalonia et KH0012-Lab sont exclus.

## Fichiers modifies

- Existing : `KnowledgeHub.slnx` (six entrees Project uniquement).
- New : six repertoires de projets (csproj, marqueurs d'assembly, tests xUnit et fixtures structurelles), puis les deux documents de cette preparation sous `docs/architecture`.
