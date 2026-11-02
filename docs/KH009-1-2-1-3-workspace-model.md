# KH-009.1.2 + KH-009.1.3 — Domaines Workspaces et prévalidation EF Core

État : modèle métier, configurations EF Core et contraintes SQL préparés ; **non déployés**.
Base locale attendue : `d108665` (`feat(kh009): add OIDC application identity foundation`).
Contrat : `docs/KH009-0-api-contract.v1.json` ; plan EF initial : `KnowledgeHub.Workspaces.EFCore_Model_Migrations.md` (document de conception hors dépôt).

## 1. Choix de livraison — prévention de la dérive de migrations

EF Core >= 9 refuse `MigrateAsync()` si le modèle de production diverge du dernier snapshot.
Les 46 tests d'intégration historiques utilisent `IsolatedPostgresFixture`, qui appelle `KnowledgeDbContext.Database.MigrateAsync()`.

Ce lot ajoute donc les quatre configurations standard `IEntityTypeConfiguration<T>` en les marquant
`IWorkspaceStagedConfiguration`. Le `KnowledgeDbContext` **continue à charger ses configurations existantes**
mais **exclut temporairement** les nouvelles. Ainsi, aucune table Workspaces n'est supposée exister dans
la base de développement, les routes anciennes fonctionnent et le snapshot EF reste identique.

Un `WorkspaceSchemaValidationContext` séparé n'est **pas enregistré** dans l'API ; il sélectionne uniquement
ces mappings Workspaces et sert à exécuter `EnsureCreatedAsync()` sur un PostgreSQL **Testcontainers neuf et isolé**.
Il ne doit jamais être utilisé sur la base de développement ou en production. La migration KH-009.1.5
réintégrera ces mappings dans `KnowledgeDbContext` en même temps qu'une migration additive contrôlée.

## 2. Entités et invariants

| Entité | Clé | Principales invariants |
|---|---|---|
| `AppUser` (issu de KH-009.1.1) | `Id`; UNIQUE `(Issuer,Subject)` | Valeurs OIDC sensibles à la casse, collation PostgreSQL `C`, e-mail non unique, `EmailVerified` implique e-mail non nul |
| `Workspace` | `Id`; UNIQUE `SlugNormalized` | Slug ASCII minuscule de 3–80 caractères, créateur `AppUser` obligatoire, version positive, archivage avec timestamp UTC |
| `WorkspaceMember` | PK composite `(WorkspaceId, UserId)` | FKs restrictives vers Workspace et AppUser, rôle `VIEWER`..`OWNER`, statut `INVITED`/`ACTIVE`/`SUSPENDED`, un `OWNER` doit être `ACTIVE` |
| `WorkspaceInvitation` | `Id`; UNIQUE `TokenHash` | Hash SHA-256 minuscule hexadécimal 64 caractères, rôle hors `OWNER`, expiration au plus 14 jours, e-mail vérifié du destinataire, acceptation unique ou révocation, FKs restrictives |

`Workspace.Version`, `WorkspaceMember.Version` et `WorkspaceInvitation.Version` sont des tokens de concurrence
**gérés par l'application**, incrémentés par les méthodes métier. EF impose le `WHERE Version = original` lors du
`SaveChanges`, et les tests PostgreSQL prouvent que deux mises à jour concurrentes ne s'écrasent pas.
La future API traduira les conflits en `412 Precondition Failed` avec `If-Match` ; cette traduction HTTP
n'est pas implémentée à ce jalon.

L'identité du créateur est immuable ; le compte `AppUser` n'est jamais déterminé à partir de l'e-mail.
Le slug ne se renomme pas silencieusement. Le statut `INVITED` est réservé : une invitation acceptée donne
lieu plus tard à une adhésion `ACTIVE` transactionnelle, sans membership fictif.

## 3. Limites de sécurité explicites

- Les méthodes Domain vérifient des invariants **mais ne remplacent jamais l'autorisation serveur**.
  L'acteur doit être obtenu d'un jeton d'accès validé et sa membership active relue dans PostgreSQL.
- **Dernier propriétaire** : la méthode métier refuse de dégrader l'unique `OWNER` selon le nombre fourni,
  et suspendre directement un `OWNER` est interdit. Le nombre doit être calculé sous verrou SQL ou transaction
  sérialisable, avec réessai sur conflit. Sans cette sérialisation, deux changements simultanés pourraient
  retirer chacun un propriétaire malgré les contrôles individuels. L'invariant multi-lignes **n'est pas encore
  garanti par la base** : les gestionnaires transactionnels et leurs tests adversariaux appartiennent à KH-009.2.
- Les invitations ne stockent pas de jeton en clair. Le service futur générera un jeton cryptographiquement
  aléatoire, calculera son SHA-256, remettra le secret une seule fois au destinataire et ne journalisera pas
  cette valeur. Le Domain reçoit seulement le digest et compare les hashes décodés à temps constant.
- Accepter une invitation exige un `AppUser` provenant de persistance fiable, avec adresse vérifiée par
  l'IdP. La méthode `Accept` ne crée pas encore la membership : la transaction de consommation unique
  + insertion et l'émission de l'événement d'audit restent à implémenter.
- Les FK présentes ici empêchent les références vers des utilisateurs/espaces inexistants, mais les FKs
  composites protégeant `Documents`, `Tags`, `Categories` et `Sources` sont prévues en KH-009.3.
- `WorkspaceBranding`, `WorkspaceHomepage`, `WorkspaceHomeSection`, `WorkspaceAsset`, `WorkspaceAuditEvent`
  ne sont pas ajoutés dans ce lot ; ils restent prévus conformément au contrat 46 routes.
- Aucun endpoint ni aucune migration n'est ajouté et le middleware JWT n'est pas encore actif.

## 4. Qualification

1. Compiler l'API en Release avec `TreatWarningsAsErrors=true`.
2. Les tests unitaires valident les modèles, l'absence d'entités Workspaces dans le contexte de production,
   le modèle de prévalidation, les noms et clés, les rôles, la concurrence et l'acceptation d'invitations.
3. Les nouveaux tests PostgreSQL Testcontainers vérifient l'unicité de `(Issuer, Subject)` et du slug,
   les clés composites, FKs, CHECKs, hash unique et la concurrence entre deux DbContexts.
4. Les tests d'intégration historiques sont exécutés avec leur base isolée et les migrations existantes.
5. `dotnet ef migrations has-pending-model-changes` doit retourner zéro pour `KnowledgeDbContext`.
6. Le diff doit rester limité au paquet annoncé ; aucun fichier de migration/snapshot, contrôleur, route,
   client MAUI ou jeu de données de développement ne doit être modifié.

## 5. Passage à KH-009.1.5

Après qualification de KH-009.1.4 et du contrôle d'accès, intégrer ces mappings dans
`KnowledgeDbContext` en retirant le prédicat d'exclusion, ajouter les `DbSet` nécessaires et générer une
migration **additive** destinée aux nouvelles tables. Relire le SQL généré et les contraintes réelles
avant toute application ; tester sauvegarde/rollback avec PostgreSQL isolé. Le corpus historique ne reçoit
aucune attribution automatique à un espace.

Références :
- https://learn.microsoft.com/ef/core/what-is-new/ef-core-9.0/breaking-changes
- https://learn.microsoft.com/ef/core/modeling/relationships/foreign-and-principal-keys
- https://learn.microsoft.com/ef/core/modeling/indexes
- https://learn.microsoft.com/ef/core/saving/concurrency
