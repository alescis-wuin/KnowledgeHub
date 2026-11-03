# ARCH-A0.2 — Registre des décisions (gel approuvé 1.0.0-rc.1 ; amendement candidat 1.1.0-rc.1)

**Référence :** `main@0f359979235d189cb3059c86224dba738d176e4c`. **Statut :** décisions du mainteneur 7/7, gouvernance et SPEC-01..03 explicitement approuvées le 2026-11-03 ; **premier commit signé `81c806779e25a92501e1aa040ad138ae5465f5a8` présent sur la branche de PR #19 ; fusion dans `main` non réalisée** ; `prepared_base_sha=null`. Le JSON de provenance extérieur au dépôt `REGISTRE-DECISIONS-MAINTENEUR.json` conserve le texte détaillé et les réserves de chaque lot.

| ID | Choix approuvé | Effets normatifs et limites |
|---|---|---|
| A02-D01 | A | 23 types HTTP, `KnowledgeHub.Contracts.Http`, shapes JSON observées préservées. |
| A02-D02 | B | Clients spécialisés ; monolithe `KnowledgeHubHttpClient(HttpClient,TimeSpan)` **rejeté** ; 22 opérations préservées, réparties SPEC-01. |
| A02-D03 | A + réserve | 4 familles .NET, code stable et corrélation sûre pour toutes ; partage par interface approuvé SPEC-03, pas par Exception de base. |
| A02-D04 | B | API normalisée avant SDK ; aucun fallback 200/201 ou PascalCase canonisé ; SPEC-02 retient `ProblemDetails` RFC 9457. |
| A02-D05 | A | 16 types RAG, ports IClaimProposalGenerator, IQualifiedEvidenceAssessor, IEvidenceRevalidator ; bridge Application, sécurité KH008 intacte. |
| A02-D06 | A | IVerifiedVerdictEvaluator différé à A3.2 ; **absent du contrat v1**, nouvelle approbation. |
| A02-D07 | A | 48 propriétés historiques + propriété documents architecture par intégrateur ; trois scopes externes protégés. |
| GOV | A | CR obligatoire et SemVer ; pas de modification hors owner ou sans preuves. |

## Résolutions techniques finales approuvées (en complément des sept choix d'orientation)

| ID | Solution approuvée | Contrôle requis avant implémentation |
|---|---|---|
| SPEC-01 | Cinq clients : Metadata (6), Documents (12), Statistics (1), Rag (2), VectorSearch (1). Options et transport uniques internes. | Revue du découpage et contrats CLR ; test 22/22 opérations et aucun retry. |
| SPEC-02 | Normaliser API : les trois créations de métadonnées en HTTP/enveloppe 201 ; toutes erreurs non-2xx au format `application/problem+json` RFC 9457 avec `code`, `correlationId`. | Revue du format cible ; tests MVC end-to-end 400/401/403/404/409/422/429/500, pas de fuite. |
| SPEC-03 | Quatre sous-classes .NET et interface commune `IKnowledgeHubClientFailure` portant `KnowledgeHubErrorInfo`. Code et corrélation validés. | Revue des signatures ; tests catch polymorphes, course timeout/cancel, confidentialité des messages. |

Aucune normalisation API ni DLL SDK n'a été exécutée dans ARCH-A0.2. Ces solutions ne constituent pas des instructions autorisant des modifications des contrôleurs, DI ou migrations.

## Écarts de source historiques conservés comme faits observés

| Référence | Défaut/propriété observée | État et responsable |
|---|---|---|
| WIRE-01 | POST catégories/sources/tags : HTTP 200 vs corps 201 | Intégrateur API : corriger et qualifier avant SDK. |
| WIRE-02 | Middleware d'exception émet PascalCase | Intégrateur API : normaliser en SPEC-02. |
| WIRE-03 | `InvalidModelStateResponseFactory` configuré dans `KnowledgeHub.Api/Program.cs` : HTTP 400 via `ApiResponses.Failure<object>`, sérialisé en camelCase par MVC | Intégrateur API : unifier les erreurs non-2xx. |
| WIRE-04 | DTO historiques en namespace global | Intégrateur : mapper/extraction sans renommage massif. |
| ARCH-01 | Observability.Verify référence Tests.Integration | Dette outillage distincte, pas modèle production. |
| ARCH-02 | Tests.Integration → CorpusImporter en double | Dette existante, sans changement ici. |
| COR-001 | Chaîne JWT OIDC à revoir | Correctif sécurité isolé, test spécifique obligatoire. |
| COR-002 | Réordonnancement WorkspaceHomepage et contraintes EF | Correctif PostgreSQL isolé, jamais migration implicite. |

## Change Request post-gel

Chaque CR fournit `task_id`, `base_sha`, `current_sha`, `requested_paths`, `affected_contract`, `concrete_need`, `old_behavior`, `new_behavior`, `impacted_consumers`, `CLR_wire_data_compatibility`, `proposed_tests`, `integration_order`, `owner`, `approval_decision`. La qualification distingue **schéma filaire**, **ABI/CLR**, **données stockées** et **consommateurs**. Tout ajout de catégorie d'erreur, d'enum, d'opération ou toute migration d'API comporte des tests ciblés et une version selon MAJOR/MINOR/PATCH. Le propriétaire intégrateur arbitre les fichiers partagés.

## Approbation du gel final

- Les choix D01–D07 et GOV sont déjà approuvés ; ne pas les redemander.
- L'approbation explicite du gel documentaire `1.0.0-rc.1` inclut SPEC-01, SPEC-02 et SPEC-03, le 2026-11-03.
- Approbateur : **mainteneur du projet** (message explicite KnowledgeHub du 2026-11-03) ; SHA du premier commit documentaire : **`81c806779e25a92501e1aa040ad138ae5465f5a8`**, SHA de fusion dans `main` : **non créé** ; `prepared_base_sha` : **null**.
- Gel normatif approuvé et commité sur branche PR, **non fusionné dans `main` et non implémenté** ; ne déduit aucun résultat .NET des validations documentaires.

## CR ARCH-A0.2-PR19-REV-001 — Correction post-gel proposée

- `task_id` : `ARCH-A0.2-PR19-REV-001` ; `base_sha` et `current_sha` : `81c806779e25a92501e1aa040ad138ae5465f5a8` (commit exact au moment des quatre remarques) ; `requested_paths` : les six fichiers sous `docs/architecture/` de la PR #19.
- `affected_contract` : statut documentaire et gates A0.2, baseline WIRE-03, visibilité des claims KH008, budget SDK HTTP.
- `concrete_need` : résoudre 1 remarque P1 et 3 remarques P2 publiées par Codex sur la PR #19.
- `old_behavior` : documentation indiquant intégration Git absente, `ValidationProblemDetails` historique, claims sans distinction public/interne et budget SDK non coordonné au timeout hôte.
- `new_behavior` : statut branche PR publié mais `main` non fusionné, baseline HTTP 400 camelCase `ApiResponse`, claims internes conservés et filtrés seulement à la projection publique, garde `HttpClient.Timeout == Timeout.InfiniteTimeSpan || HttpClient.Timeout > TotalRequestTimeout` et budget SDK fini strictement positif.
- `impacted_consumers` : intégrateur ARCH-A1.2, équipes A2/API, A2/Client.Http, A3/Verification, validation documentaire et fixtures.
- `CLR_wire_data_compatibility` : aucune classe ni DTO modifiés dans ce lot documentaire ; correction de la spécification et des fixtures, sans migration de données ; le garde timeout est un complément contractuel à tester lors de l’implémentation.
- `proposed_tests` : lint JSON et Markdown, vérification des invariants 23/22/16, 49 ownership, graphes DAG, fixtures baseline/target, tests A2 de timeout et tests KH008 de projection interne/publique.
- `integration_order` : valider le delta sur la branche existante, commit signé distinct, publier la PR mise à jour après autorisation, attendre les revues Codex, fusion uniquement après autorisation.
- `owner` : intégrateur (dossier `docs/architecture/`) ; `approval_decision` : résolution des revues explicitement demandée par le mainteneur, **acceptation du delta à confirmer sur le rapport d’exécution**.
- Le gel approuvé `1.0.0-rc.1` et ses 7 décisions ne sont pas annulés. Aucun `prepared_base_sha` ni commit de fusion dans `main` ne peut être inventé.

## CR ARCH-A0.2-PR19-REV-002 — Renforcement après nouvelle revue Codex

- `task_id` : `ARCH-A0.2-PR19-REV-002` ; `base_sha` et `current_sha` de la revue : `570466b039c128f7c5e0f1fed7bcdc0d15fe8db8` ; `requested_paths` : les six documents normatifs `docs/architecture/` de la PR #19.
- `affected_contract` : transport et configuration `KnowledgeHub.Client.Http`, délai SDK, normalisation API WIRE-02/03, 404 des lectures documentaires, fixtures et règles de sécurité.
- `concrete_need` : résoudre 1 remarque P1 (redirections) et 5 remarques P2 après la revue du second commit ; éviter toute fuite de documents et toute classification 404 ou timeout inexacte.
- `old_behavior` : pas de borne maximale `CancelAfter`, redirections hôte non spécifiées, BaseAddress non validée, fixture source sans `url:null`, 404 nullable basé seulement sur le statut, regex `.` non échappée.
- `new_behavior` : délai SDK borné [1, 2 147 483 647] ms ; handler hôte non redirigeant obligatoire avec preuve d'intégration ; adresse API absolue validée et HTTPS hors loopback ; fixture `url:null` ; 404 documentaire typé `document.not_found` vs `route.not_found`/proxy ; regex ancrée à point littéral.
- `impacted_consumers` : intégrateur API normalisation SPEC-02, SDK A2, `KnowledgeHub.CorpusImporter`, interfaces futures et tests de contrat.
- `CLR_wire_data_compatibility` : aucun CLR ni migration modifiés dans cette correction. Le futur code `document.not_found`/`route.not_found` élargit le vocabulaire filaire cible et requiert qualification/CR d'intégration ; les options et constructeurs SDK approuvés sont conservés. L'obligation de configuration hôte nécessite un vrai contrôle d'intégration, car `HttpClient` ne révèle pas son handler.
- `proposed_tests` : JSON strict ; validation des quatre familles d'erreur, 23 types/22 opérations/16 RAG/49 réservations, bornes timeout et BaseAddress, réseau 307/308 POST vers origine non fiable, 404 document/route/proxy et code regex, sérialisation MVC de `url:null`.
- `integration_order` : appliquer seulement le delta documentaire sur la branche existante, vérifier le retour v3, créer un commit signé distinct, publier après autorisation, examiner nouvelle revue Codex, fusionner uniquement après accord explicite. Prévoir les gates HTTP/API/SDK avant le cutover A2, et G1 indépendant.
- `owner` : intégrateur du répertoire `docs/architecture/` ; `approval_decision` : correction candidate soumise à l'acceptation du mainteneur après retour de qualification ; aucune approbation ni fusion automatique.
- Les sept décisions d'orientation et la gouvernance approuvées en `1.0.0-rc.1` restent inchangées ; `prepared_base_sha=null`.

## CR ARCH-A0.2-PR19-REV-003 — Revue Codex III

- `task_id` : `ARCH-A0.2-PR19-REV-003`.
- `base_sha` : `4409b10a5bbb7b03a6a36c2d26df94b4be075e96` (tree `5c12fe6028be01193e6ff42267e0c6e0f5b2b18e`).
- `current_sha` : base Git vérifiée ci-dessus ; **SHA du futur commit de correction = inconnu** jusqu'au commit local et à son push ; résolution lors de la revue via `git rev-parse HEAD` et la tête PR #19, sans hash auto-référent.
- `requested_paths` : les six fichiers suivis `docs/architecture/ARCH-A0.2-*` de la PR #19.
- `affected_contract` : provenance de revue, version normative, TLS des clients HTTP, limites mémoire/réponses JSON et catégories diagnostiques existantes.
- `concrete_need` : quatre suggestions P2 Codex sur `4409b10` (SHA de provenance, version, certificats, réponses volumineuses).
- `old_behavior` : métadonnées rattachées à `570466b`, version `1.0.0-rc.1` indistincte des ajouts récents, TLS hôte non attesté, corps HTTP sans limite explicite.
- `new_behavior` : base examinée `4409b10` et arbre exact ; candidat `1.1.0-rc.1` en attente d'acceptation ; vérification TLS chaîne/nom par le handler hôte, tests négatifs réels ; limites par route en octets décodés et en éléments avant désérialisation ; exception protocolaire sûre sur dépassement.
- `impacted_consumers` : futur SDK A2, importeur, MAUI/Avalonia, API pour futures formes de réponse ; aucune intégration de consommateur effectuée.
- `CLR_wire_data_compatibility` : aucun fichier C# ni schéma DB modifié ; les nouveaux codes HTTP `document.not_found`/`route.not_found` et le code SDK `protocol.response_limit_exceeded` sont des ajouts MINOR candidats et exigent approbation/qualification avant diffusion. Les plafonds peuvent refuser de grands résultats existants : transition explicite et décision sur pagination future, sans prétendre que celle-ci existe.
- `proposed_tests` : JSON strict, graphes et comptages inchangés, identité Git base, TLS invalid/expired/wrong-host et callback dangereux, dépassement 2xx/4xx, chunked, decompression, JSON profondeur et listes, couverture exacte des 22 routes.
- `integration_order` : appliquer correction documentaire REV-003 en local ; vérifier ; commit signé ; publier sans fusion ; demander revue Codex ; acceptation explicite de `1.1.0-rc.1` par le mainteneur ; fusion contrôlée ; implémentation API/SDK dans les phases autorisées seulement.
- `owner` : intégrateur `docs/architecture/` ; aucune modification hors six documents.
- `approval_decision` : **PENDING**, l'approbation historique du gel `1.0.0-rc.1` et des sept choix reste préservée, ne vaut pas approbation automatique du candidat `1.1.0-rc.1`.
