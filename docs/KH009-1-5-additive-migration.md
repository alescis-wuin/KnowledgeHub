# KH-009.1.5 — Migration additive PostgreSQL / EF Core

État : **à exécuter et à qualifier sur Windows**. Base Git vérifiée : `5912115f4ea03a47fb9a5aa42fe589d2f4016992`.

## Portée

1. Intégrer `AppUser`, `Workspace`, `WorkspaceMember`, `WorkspaceInvitation` au `KnowledgeDbContext` de production, en réutilisant les configurations déjà prévalidées par KH-009.1.2+1.3.
2. Générer via `dotnet ef migrations add Kh009AddWorkspaceIdentity` une migration additive et le snapshot EF Core associé, à partir des migrations existantes `InitialCreate` et `AddExternalDocumentIdentity`.
3. Utiliser un seul accès autoritatif `EfWorkspaceAccessStore` qui joint le couple `(Issuer, Subject)` du principal authentifié, `AppUsers`, `WorkspaceMembers` et `Workspaces`. Aucune autorisation n'est accordée à partir de l'e-mail ou de claims `role` du JWT.
4. Tester une montée de version d'une base PostgreSQL 17 Testcontainers peuplée avec un document historique et vérifier la préservation des données, les tables, les contraintes et les accès.
5. Générer un script SQL idempotent pour revue **sans application à la base de développement**.

## Barrières de sécurité et périmètre SQL

- `Up()` de la migration générée ne doit contenir que quatre `CreateTable` sur `AppUsers`, `Workspaces`, `WorkspaceMembers`, `WorkspaceInvitations` et leurs `CreateIndex`. Toute autre opération interrompt la qualification et nécessite une revue manuelle.
- Aucune attribution automatique des `Documents`, `DocumentChunks`, `Tags`, `Categories`, `KnowledgeSources` ou index vectoriels historiques à un espace. L'isolement documentaire inter-espace relève de KH-009.3/4.
- Aucun `dotnet ef database update`, `docker compose down --volumes` ni modification de la base PostgreSQL locale/partagée. L'intégration utilise exclusivement des conteneurs Testcontainers créés pour les tests.
- Le `DenyAllWorkspaceAccessStore` historique reste disponible dans les sources mais n'est plus enregistré en DI ; les tables neuves étant vides, l'autorisation reste **fermée par défaut** jusqu'à la création contrôlée des utilisateurs et adhésions en KH-009.2.
- Les anciennes routes `/api/*` ne sont pas encore tenantisées et ne doivent pas servir à exposer des contenus privés. Les contrôleurs Workspaces restent à implémenter.
- L'EF migration peut comporter un `Down()` destructif **uniquement pour les quatre nouvelles tables**. Ne pas exécuter ce retour arrière sur une base contenant des données Workspaces sans procédure validée.

## Critères de qualification

- Preflight : branche Git, commit `5912115`, HEAD distant identique, worktree propre, .NET 10, `dotnet ef`, Docker accessibles.
- Intégrité : comparaison SHA-256 du paquet et de chaque fichier ; seuls les chemins annoncés peuvent changer.
- `dotnet ef migrations add`, inspection stricte des opérations `Up`, génération SQL ciblée, `dotnet ef migrations has-pending-model-changes` sans dérive.
- Build API Release strict sans avertissements, 320+ tests unitaires, 68+ tests d'intégration (nouveaux tests en sus).
- Un rapport `summary.json`, les journaux de commandes, le SQL et les empreintes des fichiers générés servent de preuves avant un **commit/push séparé**.
- En cas d'échec : ne pas commiter, conserver le diagnostic et les sauvegardes, transmettre le rapport pour correction ciblée.

## Avant une application réelle

La qualification dans Testcontainers ne vaut pas autorisation de migrer le PostgreSQL de développement ou de production. Examiner le script SQL et la version `__EFMigrationsHistory`, effectuer une sauvegarde vérifiée et organiser une fenêtre de migration/retour arrière avant toute application manuelle. Les migrations sur la base réelle sont une étape ultérieure expressément validée.

Références : https://learn.microsoft.com/ef/core/managing-schemas/migrations/applying ; https://learn.microsoft.com/ef/core/managing-schemas/migrations/managing

## Reprise consolidee (UNIFIED-001)

Le lanceur unique reconnait les fichiers deja appliques et une migration deja generee.
Une migration existante n'est ni supprimee, ni regeneree, ni renumerotee. Les six sources
sont comparees aux versions connues (UTF-8, fins de ligne LF/CRLF equivalentes).
Un contenu divergent, un second identifiant de migration, un conflit Git ou une branche
inattendue interrompt la reprise sans ecraser le travail local.

Le test de preservation verifie les quatre compteurs captures immediatement apres
la migration, avant toute execution des tests partageant le fixture. Un cinquieme
test ecrit volontairement dans les nouvelles tables, puis rejoue les assertions de
preservation. Les anciens tests d'identite, de droits et d'unicite sont conserves.
Les seuils de qualification sont 320 tests unitaires et 73 tests d'integration,
avec au moins cinq tests de migration, aucun echec ni test ignore. Le filtre migration
est execute deux fois avant les suites completes.

Le lanceur produit un audit des objets MigrationOperation avec un programme .NET
temporaire hors depot, ainsi que le SQL idempotent de revue. Une validation d'index
Git temporaire inclut les fichiers non suivis sans toucher a l'index de l'utilisateur.
La publication n'a lieu qu'avec l'option explicite -CommitAndPush, apres toutes les
validations. Une relance recontrole les fichiers, la branche, la migration et les tests;
elle ne reutilise pas un PASS anterieur comme preuve nouvelle. Les fichiers existants
et le snapshot sont sauvegardes avant toute modification. Un echec de test conserve
les sources pour diagnostic; aucun retour arriere SQL n'est execute.

Le ZIP de rapport contient les journaux, TRX, SQL, empreintes et copies des sources
concernees, mais pas les fichiers appsettings, ni un dump de la base de developpement.
Verifier les journaux avant de les partager. Les anciens lanceurs FIX1/FIX2 ne sont
plus necessaires pour cette reprise.
