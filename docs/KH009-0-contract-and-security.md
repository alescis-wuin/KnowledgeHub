# KH-009.0 — Contrat API, identité et plan de qualification Workspaces

**État :** baseline de conception et contrôles statiques ; routes Workspaces NON IMPLÉMENTÉES.
**Base audité :** `main` au commit `53f032f2861ad823f78338b5ac0058e275afe059` (PR #16 KH-008.4 fusionnée).
**Objet du jalon :** verrouiller les invariants nécessaires aux lots KH-009.1 à KH-009.6 sans modifier l'API fonctionnelle, EF Core ou les données.

## 1. Artefacts contractuels

- `docs/KH009-0-api-contract.v1.json` : 46 opérations, rôles requis, conventions de réponse, préconditions et exemple de DTO. Dérivé de la proposition `KnowledgeHub.Workspaces.ApiRoutes.v1.json` du 02/11/2026, avec ajouts documentés ci-dessous.
- `evaluation/kh009-contract-v1/security-cases.json` : 36 scénarios de sécurité **attendus**, tous `PENDING`. Ce fichier n'est **pas** un résultat d'exécution.
- `KnowledgeHub.Tests.Unit/Contracts/Kh009ContractSpecificationTests.cs` : tests xUnit de cohérence documentaire uniquement. Leur succès ne signifie pas que les 46 endpoints ou l'isolation des données fonctionnent.

La référence canonique des chemins est `/api/v1/workspaces`. Les réponses applicatives sont enveloppées dans `ApiResponse<T>` sauf les 5 opérations héritant délibérément du JSON brut des services RAG/retrieval et la réponse binaire d'asset. Les réponses 204 ne doivent jamais inclure de corps.

**Différences délibérées avec la proposition initiale :** `status` indique une baseline non implémentée ; SHA de base explicite ; description de la clé d'identité `(iss, sub)` ; sémantique 401/403/404/412/428 ; statut HTTP 428 pour `If-Match` absent ; décisions encore ouvertes sur fournisseur OIDC et migration historique. Les 46 opérations de la proposition sont conservées.

## 2. Identité et authentification (KH-009.1)

- Jetons OAuth2/OIDC **access tokens** Bearer destinés à l'API, pas des ID tokens ; validation systématique de la signature, de `iss`, `aud`, `exp` et de la configuration du schéma JWT. Clés et fournisseur proviennent de configuration sécurisée, jamais d'un secret commité.
- Le principal applicatif est identifié par le couple `(issuer, subject)` et relié à un `AppUser` persistant, pas par l'adresse e-mail (mutable) ni par un rôle émis par un client. `email_verified` exigé pour accepter une invitation.
- Tous les endpoints v1 exigent un principal authentifié. Réponse 401 avec `WWW-Authenticate: Bearer` quand le jeton est absent ou invalide. Les mécanismes de renouvellement de jeton appartiennent au client/IdP, pas à l'API.
- **Non-membre** : 404 pour ne pas révéler l'existence de l'espace. **Membre actif sans droit suffisant** : 403. Objets d'un autre espace : 404. Sémantique uniforme et sans contenu confidentiel.
- Le fournisseur OIDC et le mode d'acquisition du jeton côté MAUI Windows sont des décisions à prendre en KH-009.1. Aucun endpoint local ne contourne l'authentification au prétexte du développement.

## 3. Autorisation et données (KH-009.2 / KH-009.3)

- Rôles : `OWNER > ADMIN > EDITOR > MEMBER > VIEWER`. Les capacités spéciales (transfert de propriété, administration des membres) sont contrôlées par des politiques explicites, pas uniquement un rang supérieur.
- L'appartenance **ACTIVE** et ses permissions sont revérifiées sur le serveur ; ne pas accepter le `WorkspaceId` ou le rôle d'un DTO comme preuve d'autorisation. Prévenir l'auto-éjection du dernier propriétaire et invalider rapidement les caches d'autorisation.
- Toutes les entités métier tenantisées auront une clé `WorkspaceId` côté persistance, y compris les associations. Les contraintes relationnelles et index uniques devront intégrer ce périmètre lorsque nécessaire (notamment `ExternalId`, catégorie, tag et source).
- Les filtres globaux EF Core sont des garde-fous, **pas** une défense suffisante contre `IgnoreQueryFilters`, les accès SQL directs, les liens transverses ou les services singleton. Les opérations de lecture, écriture et jointure doivent imposer l'autorisation au niveau service/repos et les contraintes DB.
- **Données historiques** : aucune attribution automatique à un compte ou un espace. Plan de migration additive, revue explicite et sauvegarde avant migration. Pas de `Database.Migrate()` au démarrage ni de remise à zéro des volumes Docker.
- Les anciens endpoints `/api/*` restent inchangés dans KH-009.0 ; avant KH-009.3, décider comment les déprécier ou limiter leur corpus afin qu'ils n'offrent jamais un chemin de contournement vers des données d'espace.

## 4. RAG, vecteurs et preuves (KH-009.4)

- Les résultats `vector/search`, `context`, RAG classique et RAG vérifié sont filtrés par espace **avant le classement / topK / génération**, avec identité d'espace autorisée côté serveur.
- Le snapshot et le cache vectoriel doivent être cloisonnés par `WorkspaceId` et révision. Invalidation cohérente, sans mélange de chunks entre deux espaces, et sans augmentation imprévue de la mémoire.
- `EvidenceSnapshotProvider` de KH-008 vérifie l'identité, la version et la validité de chaque document et chunk, **ainsi que le même WorkspaceId à la capture et à la revalidation**. Une preuve étrangère ou obsolète entraîne l'abstention.
- Les politiques KH-008 restent fail-closed : évaluateur sémantique qualifié manquant => `ABSTAIN`, jamais un verdict factuel automatique.

## 5. HTTP, concurrence et médias (KH-009.2 / KH-009.5)

- `ApiResponse<T>` est le format standard des réponses JSON métier, avec `success`, `statusCode`, `traceId`, `data` et `errors`. Le contrat liste explicitement les exceptions JSON brutes, binaire et corps vide.
- Les ressources `getWorkspace`, `getBranding`, `getHomepage`, `listMembers` exposent un **ETag fort**. Les opérations `updateWorkspace`, `archiveWorkspace`, `putBranding`, `putHomepage`, `changeRole` exigent `If-Match` ; absent => 428, désaccord => 412 ; mise à jour atomique et ETag actualisé.
- Assets privés : autorisation à chaque téléchargement, type MIME contrôlé sur contenu décodé, tailles et dimensions bornées, métadonnées potentiellement sensibles retirées ; `Cache-Control: private, no-store`, `X-Content-Type-Options: nosniff`.
- Limiter et sécuriser `X-Correlation-ID` : valeur non fiable, jamais d'injection de retours à la ligne ni d'identifiants secrets dans journaux/traces. Limitation de débit, quotas, limites sur fichiers et sur appels LLM à mettre en place dans KH-009.5.
- Les endpoints sensibles ne doivent journaliser ni jetons, ni corps documentaires, ni prompts, ni codes d'invitation. Conserver les garanties d'observabilité KH-005 à KH-008.

## 6. Critères d'acceptation

### KH-009.0 — vérifiables dès maintenant

1. Contrat contenant exactement 46 `operationId` et couples méthode/chemin distincts ; conventions et droits cohérents.
2. Matrice déclarative contenant 36 scénarios identifiés, affectés à un lot et encore `PENDING`.
3. Tests xUnit de cohérence documentaire réussis (sans prétendre tester l'autorisation HTTP).
4. Tests unitaires et intégration historiques réussis (`dotnet test` sur Postgres Testcontainers isolé), sans changement d'API, de migrations ni de client MAUI.
5. Rapport des commandes et diff limité aux quatre nouveaux fichiers de ce jalon ; aucun commit/push effectué automatiquement.

### KH-009.1 à KH-009.6 — futurs critères obligatoires

- Tests HTTP réels `WebApplicationFactory` : 401/403/404/412/428 et mutations basées sur rôle ; principal test signé/contrôlé, pas de bypass en production.
- Tests PostgreSQL/Testcontainers : deux espaces A/B, même `ExternalId` dans chaque espace, FK croisées rejetées, absence d'accès global aux lignes privées, migration de base existante sans perte de données.
- Tests retrieval/IA : pas de chunk ou preuve de B dans les résultats de A, cache partitionné, `ABSTAIN` en cas d'incohérence ou d'évaluateur absent.
- Tests médias, données personnelles, accès concurrent, révocation de membres, historique, logs et OpenAPI généré depuis le code réel.
- Tests fonctionnels de MAUI Windows après fusion du backend ; pas de merge MAUI sans recette sur vrais endpoints.

### Documentation officielle consultée

- JWT : https://learn.microsoft.com/aspnet/core/security/authentication/configure-jwt-bearer-authentication?view=aspnetcore-10.0
- EF Core multitenancy : https://learn.microsoft.com/en-us/ef/core/miscellaneous/multitenancy
- EF Core query filters : https://learn.microsoft.com/en-us/ef/core/querying/filters
- Tests d'intégration : https://learn.microsoft.com/fr-fr/aspnet/core/test/integration-tests?view=aspnetcore-10.0
