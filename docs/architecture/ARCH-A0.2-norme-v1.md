# ARCH-A0.2 — Contrat normatif consolidé 1.0.0-rc.1

**État : gel normatif `1.0.0-rc.1` explicitement approuvé par le mainteneur le 2026-11-03, commité et publié sur la branche de revue de la PR #19, non fusionné dans `main` et non implémenté.** Les décisions D01..D07, la gouvernance et les résolutions SPEC-01..SPEC-03 sont approuvées. Cette approbation ne vaut ni tests runtime ni autorisation de modifier l'API, les migrations, les clients ou G1.

**Référence vérifiée :** `alescis-wuin/KnowledgeHub main@0f359979235d189cb3059c86224dba738d176e4c` (Git tree `833b6a0fda44b88cdcd60bb9c255b540e1aa3cec`), PR #18 fusionnée. `prepared_base_sha = null`. Les résultats 379 unitaires + 84 intégration antérieurs appartiennent au candidat de formatage et ne qualifient pas ce futur code.

## 1. Objet et dépendances

Monorepo, monolithe modulaire, extraction progressive et sans microservice ni dépôt séparé. Les quatre bibliothèques futures de production sont :

| Bibliothèque | Responsabilité unique | Dépendances autorisées | Propriétaire |
|---|---|---|---|
| `KnowledgeHub.Contracts.Http` | 23 types de payload JSON existants | aucune `ProjectReference` | A2 |
| `KnowledgeHub.Client.Http` | SDK de cinq clients HTTP spécialisés | `KnowledgeHub.Contracts.Http` seule | A2 |
| `KnowledgeHub.Rag.Contracts` | 16 valeurs RAG, trois ports | aucune `ProjectReference` | A3 |
| `KnowledgeHub.Verification` | parsing et politique KH008 déterministes | `KnowledgeHub.Rag.Contracts` seule | A3 |

Tests isolés prévus : `KnowledgeHub.Client.Http.Tests`, `KnowledgeHub.Verification.Tests` ; la création et le câblage de leurs `.csproj`, de `KnowledgeHub.slnx` et du DI sont réservés à l'intégrateur. L'exception observée `Observability.Verify → Tests.Integration` ne constitue pas un précédent de dépendance en production.

## 2. Contrats HTTP v1 — D01=A

Les **23 types** et leurs champs, signatures CLR et noms filaires exacts sont conservés tels qu'inventoriés dans `ARCH-A0.2-contrats-v1.json`. Les DTO source `KnowledgeHub.Application`, y compris ceux du namespace global, restent en place jusqu'au cutover de l'intégrateur. Aucun changement d'identité ou d'énumération implicite, notamment `ImportDocumentOutcome` numérique 0..4. Les réponses normales gardent leur forme : `ApiResponse<T>` pour les routes d'enveloppe, réponses directes pour les deux routes RAG et `POST /api/vector/search`. Champs en camelCase, règles de nullabilité et serializers caractérisés par tests contre MVC réel. Les scores ne sont pas des probabilités.

## 3. Protocole HTTP cible avant SDK — D04=B, SPEC-02 approuvée

Il faut séparer **observation historique** et **contrat cible** :

| Point | Observé à la baseline, non conforme à la cible | Cible approuvée, dépendance bloquante |
|---|---|---|
| WIRE-01 | POST categories/sources/tags : HTTP 200 mais enveloppe `statusCode: 201` | HTTP 201 réel et enveloppe `statusCode: 201` pour les trois |
| WIRE-02 | Middleware d'exception avec enveloppe PascalCase | Tout HTTP non-2xx : `application/problem+json`, `ProblemDetails` RFC 9457 |
| WIRE-03 | Auto-validation `[ApiController]` configurée dans `Program.cs` pour retourner `ApiResponses.Failure<object>` (HTTP 400, enveloppe camelCase par MVC) | Même `ProblemDetails` RFC 9457 que les autres erreurs, extension `errors` limitée aux noms de champs sûrs |

Le corps cible non-2xx utilise `type`, `title`, `status` et les extensions **`code` et `correlationId`** ; `detail` et `instance` sont omis par défaut pour éviter les fuites. Le `type` générique `about:blank` est retenu pour le premier contrat, sans inventer une URL publique de documentation non provisionnée. Le statut HTTP transmis est l'autorité et doit correspondre à `status`; la casse est uniforme. `code` est un identifiant stable documenté et ne contient jamais un texte libre d'exception. `correlationId` : exactement 32 chiffres hexadécimaux minuscules, non zéro, produit côté serveur à partir d'une trace valide ou d'un identifiant aléatoire sûr. Ces champs ne servent ni d'authentification ni d'autorisation.

Toutes les réponses client/serveur 400, 401, 403, 404, 409, 422, 429, 500 et leurs variantes (contrôleur, validation, authentification, middleware, absence de route) doivent être caractérisées et conformes avant le raccordement SDK ; les erreurs du middleware ne peuvent divulguer données documentaires, corps d'entrée, tokens, stacktrace, SQL ou chemins internes. La politique de validation doit préserver les règles de sécurité KH009 et le comportement 404 nullable uniquement sur les deux lectures documentaires prévues. `ApiResponse<T>` reste **réservé aux réponses enveloppées de succès** ; ce n'est pas le format d'erreur cible. **Compatibilité :** passer de HTTP 200 à 201 et remplacer des enveloppes d'erreurs est potentiellement incompatible pour les consommateurs déjà déployés ; l'intégrateur doit inventorier ces consommateurs, choisir une stratégie de transition/versionnement et n'autoriser la normalisation qu'après des tests de compatibilité et une CR si le gel est approuvé.

Ce choix technique, **approuvé avec SPEC-02**, est fondé sur RFC 9457 et l'API ASP.NET Core `AddProblemDetails`. La normalisation est un chantier API distinct, réservé à l'intégrateur, sans migration DB ni ouverture implicite des contrats Workspaces.

## 4. SDK spécialisé — D02=B, SPEC-01 approuvée

Une façade monolithique `KnowledgeHubHttpClient` avec 22 méthodes est **rejetée**. Les signatures déjà inventoriées des **22 opérations** conservent leurs noms, paramètres et `CancellationToken` ; seule leur propriété est répartie :

| Client public retenu (`KnowledgeHub.Client.Http`) | Nombre | Surface |
|---|---:|---|
| `KnowledgeHubMetadataClient` | 6 | categories, sources, tags GET/POST |
| `KnowledgeHubDocumentsClient` | 12 | document CRUD, import, details, search et index |
| `KnowledgeHubStatisticsClient` | 1 | statistiques |
| `KnowledgeHubRagClient` | 2 | question standard et vérifiée |
| `KnowledgeHubVectorSearchClient` | 1 | recherche vectorielle |

Constructeur contractuel retenu pour chaque classe : `(HttpClient httpClient, KnowledgeHubHttpClientOptions options)`. Options retenues : `public sealed record KnowledgeHubHttpClientOptions(TimeSpan TotalRequestTimeout)`. L'hôte possède le `HttpClient` (base, authentification, durée de vie) et le SDK ne le détruit pas. L'infrastructure interne unique `KnowledgeHubHttpTransport` gère HTTP, sérialisation, annulation, budget global y compris lecture du corps, mappage d'erreurs, corrélation et disposition des requêtes/réponses. La construction de chaque client doit refuser un `HttpClient.Timeout` hôte égal ou inférieur à `TotalRequestTimeout` (sauf `Timeout.InfiniteTimeSpan`), ainsi que les budgets SDK nuls, négatifs ou infinis ; le SDK ne change jamais la configuration de `HttpClient`. Cette exigence empêche un délai hôte plus court de produire une annulation que le SDK attribuerait à tort à son propre budget. Les tests A2 couvriront 100s/120s (refus), 10s/10s (refus), infini/120s (accepté), 120s/60s (accepté), l'annulation simultanée et la lecture streaming. Les clients n'importent ni MAUI/Avalonia, ni API/Application/EF/Ollama, et n'ont aucun retry automatique. Un verdict `ABSTAIN` HTTP 200 est une réponse métier valide, non une exception. Une réponse 2xx malformée n'est pas transformée en succès par défaut.

Les 22 routes avec leur propriétaire figurent explicitement dans `ARCH-A0.2-contrats-v1.json`. Les Workspaces KH009, l'administration d'embeddings, les diagnostics et migrations restent hors SDK v1. **Pas de raccordement client tant que la normalisation WIRE-01..03 n'a pas été qualifiée.**

## 5. Erreurs SDK — D03=A, SPEC-03 approuvée

Les quatre **catégories** sont validées : HTTP/transport, protocole, annulation, délai. Elles restent capturables selon les conventions .NET. Pour respecter l'héritage simple des exceptions .NET, le contrat normatif approuvé est une **interface de diagnostic commune**, pas une classe Exception de base unique :

```csharp
public enum KnowledgeHubErrorCategory { Http = 0, Protocol = 1, Cancellation = 2, Timeout = 3 }
public sealed record KnowledgeHubErrorInfo(
    string Code, KnowledgeHubErrorCategory Category,
    string CorrelationId, HttpStatusCode? HttpStatusCode);
public interface IKnowledgeHubClientFailure { KnowledgeHubErrorInfo Error { get; } }
```

Les types publics du SDK retenus sont `KnowledgeHubHttpRequestException : HttpRequestException`, `KnowledgeHubHttpProtocolException : Exception`, `KnowledgeHubRequestCanceledException : OperationCanceledException` et `KnowledgeHubRequestTimeoutException : TimeoutException`, **tous implémentant** `IKnowledgeHubClientFailure`. Chacun fournit des métadonnées immuables (`Error`). Les fautes de transport sans réponse HTTP relèvent de la catégorie Http mais gardent un `HttpStatusCode=null`; aucune fausse réponse HTTP ne leur est attribuée.

`CorrelationId` est généré localement **pour chaque requête** (trace W3C valide ou 128 bits aléatoires), validé et opaque ; le code reçu du serveur est mappé par liste blanche vers un code stable (fallback générique). Les codes extensibles et versionnés incluent `http.not_found`, `http.rate_limited`, `transport.network_failure`, `protocol.invalid_json`, `request.cancelled_by_caller`, `request.deadline_exceeded` ; un nouveau type d'exception exige une CR. Les messages ne contiennent aucun corps, token, URL/question privée ou valeur de header libre. Une annulation demandée par l'appelant prime sur le délai quand les signaux concourent ; une annulation non attribuable ne doit pas être renommée arbitrairement en timeout. Les deux lectures documentaires explicitement nullables gardent leur 404→null ; les autres HTTP 404 lèvent une exception. Tester les `catch` .NET, le token d'annulation, l'`InnerException` sûr et les courses d'annulation.

**SPEC-01, SPEC-02 et SPEC-03 sont explicitement approuvés par le mainteneur (2026-11-03) ; leur implémentation et leur qualification restent à réaliser séparément.**

## 6. RAG et KH008 — D05=A, D06=A

`KnowledgeHub.Rag.Contracts` détient les **16 types** exacts et exactement les trois ports publics v1 : `IClaimProposalGenerator`, `IQualifiedEvidenceAssessor`, `IEvidenceRevalidator`. Le nouveau `IEvidenceRevalidator.RevalidateAsync(EvidenceRetrievalSnapshot, CancellationToken)` doit rester étroit. `IEvidenceSnapshotProvider` reste **dans Application**, garde `CaptureAsync` et hérite de ce port en réutilisant la **même instance scoped**. Aucun doublon de type CLR dans plusieurs assemblies n'est permis lors de la bascule : compiler les consommateurs et tester le DI.

`IVerifiedVerdictEvaluator` n'est **pas un type public v1** et ne doit être créé par aucun jalon A0/A1/A2/A3.1. Il reste une **réservation documentaire** pour A3.2, avec nouvelle justification, preuve des consommateurs et approbation.

KH008 est inchangé : schemaVersion=1, 65 536 octets maximum, profondeur 16, 12 claims, 3 citations/claim, 600 UTF-16/citation, 2 000 UTF-16/statement, ID de claim ≤32 ; GUID exacts, empreintes SHA-256 lowerhex et citations textuellement exactes avec offsets UTF-16 (sans troncature des surrogates). Les citations attestent la provenance, pas l'implication sémantique ; assessor qualifié indépendant requis. Sans assessor qualifié, **ABSTAIN avant retrieval et génération**. Après assessment, revalidation, puis revalidation finale avant publication. `VerifiedEvidenceAnswer.Claims` conserve, en interne, certains claims rejetés (ABSTAIN/CONFLICT) et leurs statuts pour diagnostic, conformément aux tests KH008 ; seule la projection publique `VerifiedRagResponseDto.FromVerifiedDecision` exclut systématiquement ces claims des réponses HTTP. Le snapshot ne prouve ni fraîcheur ni permission à lui seul.

## 7. Propriété et change-control — D07=A, GOV=A

Les **48 règles historiques** de propriété, la **49e règle `docs/architecture/` → intégrateur** et les trois périmètres externes (KH0012-Lab privé, Avalonia PoC Windows, worktree MAUI Windows) demeurent. Un chemin réservé exact ou un glob `**/*.csproj` prime sur toute allocation de répertoire. Sont particulièrement réservés : solution, Program.cs et composition DI, contrôleurs et DTO Application, `KnowledgeDbContext`, migrations EF et configurations, fournisseur de snapshots, tests communs, contrats documentaires. Aucun périmètre privé n'est copié dans le monorepo public.

Après gel approuvé, chaque évolution passe une CR avec : `task_id`, `base_sha`, `current_sha`, `requested_paths`, `affected_contract`, `concrete_need`, `old_behavior`, `new_behavior`, `impacted_consumers`, `CLR_wire_data_compatibility`, `proposed_tests`, `integration_order`, `owner`, `approval_decision`. Le versionnement suit **MAJOR pour rupture**, **MINOR pour ajout compatible**, **PATCH pour correction sans changement observable**. La mise à jour de ces documents ne passe pas automatiquement à « approved » : seul le mainteneur peut autoriser le gel. Les migrations ou changements de sécurité demandent une autorisation spécialisée en plus de la CR. Aucune CI distante requise, mais preuve de validation locale sur l'identité réelle du code testé.

## 8. Ordre de passage et verdict

1. **Acquis** : approbation explicite du gel normatif et de SPEC-01..03 ; aucune modification applicative ni base de données.
2. **À réaliser** : intégrer les six documents par un changement documentaire isolé appartenant à l'intégrateur, vérifié sur le HEAD courant.
3. Finaliser la qualification ARCH-A1.1 sur la base effective et préparer ARCH-A1.2, ses quatre projets de production et deux de tests, en respectant les réservations et le statut de G1.
4. Après I0 (compilation, références, sécurité, tests, SHA réellement validés) : renseigner `prepared_base_sha` au vrai commit de base G1 ; seulement alors A2/A3 peuvent développer en parallèle.
5. L'intégrateur conduit séparément la normalisation API D04-B, avant l'intégration du SDK ; COR-001/COR-002 restent ouvertes et ne sont pas corrigées ici.

**Verdict actuel :** gel **normatif** ARCH-A0.2 `1.0.0-rc.1` approuvé (7/7 + gouvernance + SPEC-01..03), **six fichiers documentaires committés sur la branche Git de la PR #19** (premier commit signé `81c806779e25a92501e1aa040ad138ae5465f5a8`), mais **aucune fusion dans `main`**, `prepared_base_sha=null` et G1 non obtenu ; aucun changement applicatif/API/DB par ce dossier. Les validations statiques des fichiers JSON/graphes/ownership ne sont pas des tests .NET des nouvelles bibliothèques.

## Références externes distinguées des faits du dépôt

- [RFC 9457, Problem Details for HTTP APIs](https://www.rfc-editor.org/rfc/rfc9457.html) : modèle `application/problem+json`, `status` cohérent avec HTTP, extensions et confidentialité.
- [Microsoft Learn, ASP.NET Core 10 — API error handling](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/error-handling-api?view=aspnetcore-10.0) : `AddProblemDetails`, personnalisation et middleware.
- [Microsoft Learn, .NET 10 — HttpRequestException.StatusCode](https://learn.microsoft.com/en-us/dotnet/api/system.net.http.httprequestexception.statuscode?view=net-10.0) : propriété nullable sur erreur de transport sans statut.

## Amendement PR #19 — Codex Review (ARCH-A0.2-PR19-REV-001)

Quatre corrections documentaires sont proposées après revue du premier commit signé : (1) statut Git distinct entre branche PR publiée et `main` non fusionné ; (2) baseline HTTP 400 selon `InvalidModelStateResponseFactory` configuré ; (3) claims diagnostiques internes conservés et projection HTTP filtrée ; (4) contrat explicite du délai hôte `HttpClient.Timeout`. Aucune signature publique, valeur d’enum, route, règle de sécurité KH008, propriétaire ou dépendance de projet n’est modifiée. Elles ne supposent ni fusion de la PR, ni implémentation du SDK/API, ni G1. La qualification de ce delta documentaire n’équivaut pas à la qualification runtime.
