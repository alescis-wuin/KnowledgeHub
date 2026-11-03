# KH-009.1.6.0 — ADR : modèles de présentation, médias privés et audit

- **Statut : ACCEPTÉE — décisions D01–D09 (acceptation explicite par le mainteneur à l’application du lot de clôture).**
- **Date :** 2026-11-02.
- **Projet :** `alescis-wuin/KnowledgeHub`, branche `feature/kh-009-workspaces-backend`.
- **Référence Git vérifiée sur GitHub :** `11a6f71dbffac71dc1f37469232a754497ee5f8a`.
- **Environnement de qualification :** Arch Linux/CachyOS, .NET SDK conforme à `global.json` (`10.0.403`, `latestPatch`), EF Core 10.0.14 et PostgreSQL via Docker/Testcontainers. Qualification locale obtenue le 2026-11-03 ; preuves détaillées dans le dossier d'exécution v1r2.
- **Périmètre du présent jalon :** décisions de conception, invariants, dépendances et tests futurs. Aucun modèle, mapping, DbSet, endpoint, migration ni objet PostgreSQL n'est modifié par cette ADR.

## 1. Sources et compatibilité

Le code du commit de référence, `docs/KH009-0-api-contract.v1.json` (46 opérations), `evaluation/kh009-contract-v1/security-cases.json` (36 obligations encore `PENDING`), les documents de reprise `01`, `02`, `04`, `05` et la proposition initiale `KnowledgeHub.Workspaces.EFCore_Model_Migrations.md` fondent cette ADR. En cas de divergence, le contrat versionné et le code réellement présent priment sur les anciens guides de conception.

Les modèles déjà persistés (`AppUser`, `Workspace`, `WorkspaceMember`, `WorkspaceInvitation`) et la migration `20261102140918_Kh009AddWorkspaceIdentity` restent inchangés. Aucune attribution de documents historiques à un workspace, modification KH-008, activation de routes ou opération sur la base de développement n'est autorisée.

## 2. Décisions d’architecture D01–D09

### D01 — Principes communs

- Créer exactement les cinq entités `WorkspaceBranding`, `WorkspaceHomepage`, `WorkspaceHomeSection`, `WorkspaceAsset`, `WorkspaceAuditEvent` dans `KnowledgeHub.Domain/Entities/` au futur KH-009.1.6.1.
- Utiliser des classes `sealed`, propriétés à écriture privée, constructeur EF privé, constructeurs/fabriques métier validants et méthodes de mutation explicites. Vérifier `Guid.Empty`, valeurs d'enums inconnues, chaînes de contrôle, dépassements et timestamps non UTC.
- Les objets portant un `WorkspaceId` ne peuvent jamais être déplacés vers un autre tenant. L'existence d'un identifiant ou la validité Domain n'accorde jamais un droit d'accès : l'autorisation dépend du principal OIDC validé, de la membership ACTIVE relue et de la politique serveur.
- Les ressources d'un workspace archivé ne peuvent être modifiées par les futurs services applicatifs, sauf procédures explicitement autorisées (restauration, audit, rétention). Ne pas ajouter aujourd'hui de logique HTTP ou d'accès SQL dans Domain.

### D02 — WorkspaceBranding

**Propriétés :** `WorkspaceId: Guid` (clé et FK), `DisplayName: string` (1–150), `LogoAssetId: Guid?`, `BannerAssetId: Guid?`, `PrimaryColor`, `SecondaryColor`, `AccentColor`, `BackgroundColor`, `SurfaceColor`, `TextColor`, `TextOnPrimaryColor` (`string`, sept couleurs), `Version: long`.

- Une instance au maximum par workspace, créée avec les valeurs du contrat à la future création transactionnelle du workspace.
- Les couleurs d'entrée au format `#[0-9A-Fa-f]{6}` sont normalisées en hexadécimal majuscule `#[0-9A-F]{6}` ; refuser alpha, noms CSS, raccourcis `#RGB`, espaces et formats incompatibles.
- Exiger un contraste WCAG AA de **4,5:1** pour `(TextColor,BackgroundColor)`, `(TextColor,SurfaceColor)` et `(TextOnPrimaryColor,PrimaryColor)` selon le calcul de luminance relative sRGB. Les autres paires ne sont pas promises comme lisibles si elles sont utilisées sans texte.
- `LogoAssetId` et `BannerAssetId` sont facultatifs, distincts, et doivent désigner des assets du même workspace, respectivement `Purpose=LOGO` et `Purpose=BANNER`. La FK composite SQL garantit le tenant ; la correspondance du purpose est validée au niveau métier/service.
- Version initiale `1`. Toute mise à jour acceptée vérifie `expectedVersion` et incrémente `Version` avec contrôle de dépassement ; futur `If-Match` et ETag fort sont gérés par l'API, jamais par Domain.
- `IsVerifiedOrganization` n'est pas un champ de branding : l'appellation ou le logo ne prouvent aucune affiliation.

### D03 — WorkspaceHomepage

**Propriétés :** `WorkspaceId: Guid` (PK/FK), `HeaderTitle: string` (1–150), `Subtitle: string?` (max. 300), `WelcomeMessage: string?` (max. 2 000), `Version: long`, collection privée de `WorkspaceHomeSection` exposée en lecture seule.

- Une homepage au maximum par workspace. L'ensemble homepage + sections constitue l'agrégat de mise à jour.
- Version initiale `1`. Le remplacement du contenu ou des sections vérifie `expectedVersion`, refuse le dépassement et incrémente la version de la **homepage**, même si seules les sections changent.
- `PUT` futur : remplacement complet validé et atomique, jamais mise à jour partielle silencieuse. Les identifiants de sections conservées doivent appartenir à la homepage ; de nouveaux identifiants sont générés côté serveur. Le service rejette les doublons et les références étrangères.

### D04 — WorkspaceHomeSection et widgets

**Propriétés :** `Id: Guid`, `WorkspaceId: Guid`, `Type: WorkspaceSectionType`, `Title: string` (1–150), `Body: string?` (max. 4 000), `SortOrder: int` (0–19), liste de liens typés, persistée plus tard dans `LinksJson: jsonb`.

**Enum stable :** `TEXT=0`, `LINKS=1`, `STATS=2`.

- Un workspace possède de **0 à 20 sections** ; ordre unique par workspace, positions contiguës de `0` à `N-1` en état enregistré. Toute opération métier sur la collection contrôle tenant, identifiants, types, ordre et limite.
- `TEXT` : texte brut non vide, aucune interprétation de HTML/JS et aucune liste de liens. Le rendu futur doit échapper le contenu.
- `LINKS` : `Body=null`, de 1 à 10 liens avec `label` borné à 100 caractères et `target` parmi `DOCUMENTS`, `SOURCES`, `CHAT`. Liens vers les points d'entrée internes du workspace uniquement, sans URL externe, paramètre HTML, URI scriptable ou identifiant d'une ressource étrangère. La structure de liens est fortement typée côté Domain et sérialisée en JSON borné côté Infrastructure.
- `STATS` : `Body=null`, `links=[]`, métriques non paramétrables ; les futurs services ne retourneront que des métriques réellement filtrées sur le workspace autorisé. Pas de requête SQL, expression ou source choisie par le client.
- Garantie SQL future : `CHECK (SortOrder BETWEEN 0 AND 19)` et `UNIQUE(WorkspaceId,SortOrder)` ; ensemble ces contraintes plafonnent à 20 lignes pour un workspace, même en SQL direct. La continuité de l'ordre est une garantie métier/transactionnelle, **pas** une garantie de ce CHECK.
- Réordonner la collection via remplacement transactionnel contrôlé afin d'éviter les collisions transitoires de l'index UNIQUE. Valider ce comportement sous PostgreSQL à KH-009.1.6.2/2.4.

### D05 — WorkspaceAsset et stockage privé

**Propriétés :** `Id: Guid`, `WorkspaceId: Guid`, `Purpose: WorkspaceAssetPurpose`, `ContentType: string`, `StorageKey: string`, `Sha256: string`, `Length: long`, `PixelWidth: int`, `PixelHeight: int`, `CreatedAtUtc: DateTime`, `CreatedByUserId: Guid`.

**Enum stable :** `LOGO=0`, `BANNER=1`.

- Types MIME autorisés : `image/png`, `image/jpeg`, `image/webp` uniquement, canoniques. Limites sur **octets après réencodage** : logo ≤ **5 Mio** (`5*1024*1024`), bannière ≤ **10 Mio** (`10*1024*1024`), longueur strictement positive.
- Dimensions positives, chaque axe ≤ 8 192 pixels, aire ≤ 16 777 216 pixels ; contrôle anti-décompression requis avant décodage dans le futur service média. Les bornes peuvent être resserrées ultérieurement par une décision versionnée, jamais élargies silencieusement.
- `Sha256` est le digest des octets effectivement stockés, composé exactement de 64 chiffres hexadécimaux minuscules. `StorageKey` est un identifiant serveur opaque de 64 caractères hexadécimaux minuscules, non un chemin filesystem/URL et jamais transmis comme secret d'accès client.
- Contenu binaire **hors PostgreSQL** ; métadonnées uniquement dans `WorkspaceAssets`. Le futur service de stockage réalise détection de contenu réel, réencodage, suppression EXIF, publication sûre, nettoyage d'orphelins et contrôle de lecture à chaque requête.
- Clés globales de stockage et chemins physiques ne sont **pas** déterminés par le Domain. Pas de déduplication ni de visibilité entre tenants ; pas d'accès public ni jeton d'authentification en URL.
- `CreatedByUserId` doit correspondre à un `AppUser` existant. La provenance du principal est une responsabilité serveur. La suppression d'un asset encore référencé est interdite tant que les liens n'ont pas été détachés par une opération autorisée.

### D06 — WorkspaceAuditEvent

**Propriétés :** `Id: Guid`, `WorkspaceId: Guid`, `ActorUserId: Guid?`, `EventType: WorkspaceAuditEventType`, `TargetType: WorkspaceAuditTargetType`, `TargetId: Guid`, `Outcome: WorkspaceAuditOutcome`, `OccurredAtUtc: DateTime`.

- Événements autorisés initialement : `WORKSPACE_CREATED`, `WORKSPACE_UPDATED`, `WORKSPACE_ARCHIVED`, `WORKSPACE_RESTORED`, `OWNER_TRANSFERRED`, `MEMBER_ROLE_CHANGED`, `MEMBER_SUSPENDED`, `MEMBER_REACTIVATED`, `INVITATION_CREATED`, `INVITATION_ACCEPTED`, `INVITATION_REVOKED`, `BRANDING_UPDATED`, `HOMEPAGE_UPDATED`, `ASSET_CREATED`, `ASSET_DELETED`, `ORGANIZATION_VERIFICATION_CHANGED`.
- Types de cible : `WORKSPACE`, `MEMBER`, `INVITATION`, `BRANDING`, `HOMEPAGE`, `ASSET`. Pour un membre, `TargetId` désigne le `UserId` dans le `WorkspaceId` de l'événement ; pour une homepage et un branding, `TargetId=WorkspaceId`. Résultats : `SUCCEEDED`, `DENIED`, `FAILED`.
- Aucun champ libre de payload, exception, corps HTTP, message utilisateur, e-mail, jeton, code d'invitation, prompt ou contenu documentaire. Les enums et GUID sont créés depuis des sources serveur, non depuis le client.
- `ActorUserId` absent uniquement pour un événement système créé par une fabrique métier séparée et invoquée via un service autorisé. Les échecs d'accès à des workspaces inconnus/non autorisés relèvent de la télémétrie de sécurité globale, pas d'une insertion d'audit tenantisée.
- Une entrée n'a aucune méthode de modification/suppression. Prévoir une barrière EF Core refusant UPDATE/DELETE sur l'entité à KH-009.1.6.2, avec tests. **Limite explicite :** sans rôles SQL restreints ou trigger dédié, un administrateur SQL peut altérer le journal. Une protection SQL anti-altération exige une décision et un périmètre de migration spécifiques ultérieurs ; ne jamais qualifier le journal d'infalsifiable à KH-009.1.6.
- Index de consultation prévu : `(WorkspaceId,OccurredAtUtc,Id)`. Politique de rétention/export et rôle SQL d'audit à finaliser au durcissement KH-009.5 avant exploitation réelle.

### D07 — Contraintes EF Core/PostgreSQL à implémenter plus tard

| Table | Clé et relations | Contrôles SQL minimaux |
| --- | --- | --- |
| `WorkspaceBrandings` | PK/FK `WorkspaceId`; FK `(WorkspaceId,LogoAssetId)` et `(WorkspaceId,BannerAssetId)` vers clé alternative `(WorkspaceId,Id)` des assets, optionnelles | `Version >= 1`, couleur canonique, champs bornés |
| `WorkspaceHomepages` | PK/FK `WorkspaceId` | `Version >= 1`, longueurs |
| `WorkspaceHomeSections` | PK `Id`, FK `WorkspaceId` vers homepage, UNIQUE `(WorkspaceId,SortOrder)` | `Type BETWEEN 0 AND 2`, `SortOrder BETWEEN 0 AND 19`, structure de base `jsonb` |
| `WorkspaceAssets` | PK `Id`, clé alternative `(WorkspaceId,Id)`, FK vers Workspace/AppUser, UNIQUE `(WorkspaceId,StorageKey)` | Purpose, MIME, longueur, SHA, storage key, dimensions et aire bornés |
| `WorkspaceAuditEvents` | PK `Id`, FK Workspace, FK optionnelle AppUser, index `(WorkspaceId,OccurredAtUtc,Id)` | enums connus, timestamp UTC depuis Domain ; aucune colonne de données sensibles |

- `DeleteBehavior.Restrict` (ou effet SQL restrictif équivalent) pour empêcher la destruction transitive d'assets, branding, homepage et événements.
- Index sur `(WorkspaceId,...)` quand l'unicité ou la requête est propre au tenant ; pas d'index UNIQUE global sur une clé métier tenantisée.
- `IsConcurrencyToken()` sur les deux `Version`, avec incrémentation explicite avant `SaveChanges` ; tests avec deux `DbContext` pour éviter les mises à jour perdues.
- Le schéma vise des clés alternatives et FKs composées officielles EF Core, pas de simple vérification client.
- Pas de `Database.Migrate()`, `EnsureCreated()` sur la base de développement, `docker compose down --volumes`, ni modification des migrations existantes.

### D08 — Stratégie par sous-jalon, sans dérive de modèle

**KH-009.1.6.0 :** cette ADR seulement ; vérifier localement branche, HEAD, worktree propre, contrat 46 et matrice 36 ; approbation explicite.

**KH-009.1.6.1 :** cinq classes Domain, enums, éventuel objet typé de lien et tests unitaires. **Aucun `DbSet`, mapping EF, migration, route ou enregistrement DI.** Conserver les noms de propriétés conformes au contrat et les constructeurs privés nécessaires à EF. Confirmer que le modèle de production n'a pas dérivé.

**KH-009.1.6.2 :** configs isolées et tests de modèle/contraintes sur une base PostgreSQL Testcontainers créée uniquement pour les tests. `KnowledgeDbContext.ApplyConfigurationsFromAssembly` charge actuellement toutes les configurations : introduire une exclusion **ciblée sur les cinq nouveaux mappings**, ou livrer ensemble `.2` et `.3` avec preuves distinctes, pour ne pas faire diverger le snapshot de production entre les étapes. Ne pas exclure les quatre configurations déjà migrées.

**KH-009.1.6.3 :** activer les mappings et `DbSet` en même temps qu'une **nouvelle migration additive** nommée `Kh009AddWorkspacePresentationAndAudit` et la mise à jour du snapshot. Ne jamais retoucher, renuméroter ou remplacer `20261102140918_Kh009AddWorkspaceIdentity`. `Up` limité aux cinq nouvelles tables et à leurs index/contraintes, selon le gate de la roadmap. Revoir les dépendances FK et le SQL. Aucun backfill et aucune migration réelle.

**KH-009.1.6.4 :** qualification complète : nouveaux tests Domain, tests PostgreSQL contre SQL malveillant/croisé, migration depuis schéma antérieur et depuis KH-009.1.5, `has-pending-model-changes` à zéro **après** migration, suites historiques au moins 320 unitaires/73 intégration, zéro échec/skip, journaux/sha/diff à relire. Ne déclarer DONE qu'après preuves et acceptation explicite.

### D09 — Traçabilité contrat/sécurité

- Les 46 opérations, leurs méthodes, URLs, permissions et réponses restent inchangées. Les opérations concernées par le modèle sont `createWorkspace`, `getBranding`, `putBranding`, `getHomepage`, `putHomepage`, `uploadAsset`, `getAssetContent`, `deleteAsset`.
- Les 36 scénarios S001–S036 restent suivis individuellement dans la matrice contractuelle, sans modification opportuniste de leur état `PENDING`. Préparer des preuves partielles de FK/assets et concurrence pour S026–S030, et d'absence de fuite pour S032 et S035, sans les marquer PASS avant tests fonctionnels au jalon prévu.
- Les contrôles fail-closed d'identité JWT/OIDC, de membership multi-tenant et du verdict documentaire KH-008 ne changent pas.

## 3. Décisions volontairement différées et propriétaires

Ces sujets ne bloquent pas les cinq **modèles de métadonnées**, mais nécessiteront une décision versionnée avant leur propre implémentation :

| Sujet | Jalon visé | Contrainte actuelle |
| --- | --- | --- |
| Fournisseur physique de stockage, réencodage et nettoyage des assets | KH-009.2.5 | Interface de stockage privée, aucune opération binaire au 1.6 |
| Source d'identité OIDC réelle et acquisition de token côté client | KH-009.2.0 / MAUI | Les contrôles existants restent fail-closed |
| Immutabilité SQL, privilèges séparés et rétention de l'audit | KH-009.5 | Immutabilité métier/EF et risque SQL résiduel documenté |
| Stratégie de migration des documents historiques | KH-009.3 | Aucune attribution automatique |
| RLS PostgreSQL | Décision séparée, si retenue | Pas d'activation implicite |
| Isolement du cache vectoriel et des preuves KH-008 | KH-009.4 | Aucune modification dans 1.6 |

## 4. Tests et critères d'acceptation

- **ADR / revue :** les cinq entités et les trois enums initiales existent dans la conception ; les propriétés du DTO sont mappées ; sections ≤20, sept couleurs, limites d'assets, événements structurés et contraintes multi-tenant sont définis ; les décisions différées sont explicites.
- **Domain (1.6.1) :** champs vides, longueurs limites (+1), chaînes de contrôle, horodatages non UTC, `Guid.Empty`, enums invalides, 20/21 sections, positions dupliquées, section d'un autre workspace, doublons d'IDs, version obsolète ou débordement, contraste insuffisant, liens interdits, MIME/longueur/hash/dimensions invalides, absence de données sensibles dans l'audit.
- **EF / PostgreSQL isolé (1.6.2) :** FKs A/B (dont logo/bannière), unicités, 21e position impossible, contraintes SQL directes, `DbUpdateConcurrencyException` pour éditions concurrentes, suppression restrictive, préservation de la migration existante.
- **Migration (1.6.3) :** inspecter `Up/Down` et SQL idempotent, aucune suppression ou réécriture de tables existantes, sauvegarder la preuve du snapshot, vérifier `HasPendingModelChanges` égal à faux.
- **Gate :** aucune acceptation automatique à partir du succès d'un simple build, d'un rapport antérieur ou d'un statut de fichier ; les résultats doivent venir des commandes réellement exécutées dans le worktree Linux vérifié.

## 5. Validation humaine pour figer l'ADR

Après inspection du worktree local propre au commit prévu, le responsable du projet doit confirmer explicitement l'acceptation de D01–D09, puis modifier `Statut` en **ACCEPTÉE**. Le dépôt ne doit recevoir que `docs/KH009-1-6-architecture.md` pour KH-009.1.6.0, sauf test statique de conception expressément validé.

La création de la prochaine étape **KH-009.1.6.1** pourra alors être préparée comme delta séparé, en dehors de toute migration, API, fichier binaire, RAG, MAUI ou mutation de base PostgreSQL réelle.

## 6. Enregistrement de clôture de l’ADR et de KH-009.1.6

### 6.1 Acceptation explicite

Les décisions **D01–D09** sont acceptées par l’opérateur responsable du dépôt **uniquement après** exécution volontaire du lot de revue avec l’option `--accept-d01-d09`. La preuve locale inclut la date UTC d’exécution, l’empreinte de cette ADR et le manifeste exhaustif des fichiers sélectionnés pour la PR. Le maintien du présent statut dans Git exige que cet enregistrement local ait réussi. Cette acceptation n’est **ni** une approbation d’un déploiement **ni** une autorisation de migration d’une base utilisateur.

### 6.2 Qualification technique observée le 2026-11-03

- **KH-009.1.6.1 et .2 :** modèles Domain et mappings EF/PostgreSQL qualifiés ; leurs validations sont incluses dans la suite complète du jalon.
- **KH-009.1.6.3 :** migration nouvelle `20261103035104_Kh009AddWorkspacePresentationAndAudit`, `Up()` limité à cinq `CreateTable` (`WorkspaceAssets`, `WorkspaceAuditEvents`, `WorkspaceHomepages`, `WorkspaceBrandings`, `WorkspaceHomeSections`) et sept `CreateIndex` ; `Down()` limité à ces cinq tables. Anciennes migrations intactes, absence de backfill et de modification du schéma préexistant.
- **KH-009.1.6.4 :** résultats observés dans les rapports TRX v1r2 : **379 tests unitaires exécutés/réussis et 84 tests d’intégration PostgreSQL exécutés/réussis**, aucun échec, aucun test ignoré. Génération du snapshot et `has-pending-model-changes` sans divergence. Upgrade/downgrade vérifiés sur PostgreSQL éphémère, sans opération sur la base de développement.
- **SQL idempotent :** génération `migrations script 20261102140918_Kh009AddWorkspaceIdentity 20261103035104_Kh009AddWorkspacePresentationAndAudit --idempotent`, `BEGIN/COMMIT`, gardes `__EFMigrationsHistory` et insertion de l'identifiant de migration ; SHA-256 du script de revue : `4b6ff1dba153a7997c173e972ab6bf82ceb225db259ca1a933e737a2b34e7015`. Il suppose les migrations précédentes déjà appliquées et ne répare pas un schéma partiellement créé.
- **Preuves :** `v1r2-20261103T035053Z-43760.zip`, sources et hashes publiés dans son manifeste ; revue du SQL idempotent séparé, sans exécution sur une base utilisateur.

### 6.3 Limites et décisions qui restent ouvertes

- Les **36 scénarios de sécurité** `S001`–`S036` restent `PENDING` tant que leurs tests contractuels/HTTP dédiés ne sont pas qualifiés. Les 463 tests du jalon ne les remplacent pas.
- Les routes Workspaces, la tenantisation des documents historiques et les contrôleurs médias ne sont pas activés par cette migration. Ne pas présenter l’API historique comme multi-tenant.
- La continuité des positions des sections, la validation du `Purpose` des assets et la gestion du réordonnancement sont des règles métier/service, pas des invariants totalement garantis par le SQL.
- L’intercepteur EF interdit UPDATE/DELETE des événements d’audit via EF ; l’immutabilité contre un acteur SQL privilégié et la rétention restent différées à KH-009.5.
- Aucun déploiement, import de documents historiques, migration de base réelle, modification KH-008 ou modification d’API n’est autorisé par cette ADR.

### 6.4 Revue Git et PR

Le jalon 1.6 constitue un lot de **33 chemins** : **3 modifications** (`KnowledgeDbContext.cs`, `WorkspaceSchemaValidationContext.cs`, snapshot EF) et **30 ajouts**, ADR comprise. Le dossier de revue produit une liste d’inclusion exhaustive, un index Git temporaire et le diff proposé ; les fichiers hors liste, journaux, TRX, caches, rapports, secrets et bases locales sont exclus sans `git add .`.

La PR de `feature/kh-009-workspaces-backend` vers `main` comprend aussi les **5 commits antérieurs KH-009** déjà présents sur cette branche au commit de référence `11a6f71dbffac71dc1f37469232a754497ee5f8a` ; son titre et son corps doivent refléter ce périmètre total. Aucun commit, push ou ouverture de PR n’est réalisé par le lot de préparation. La revue finale du diff, les signatures Git, les CI et la publication appartiennent à une action ultérieure explicitement autorisée.
