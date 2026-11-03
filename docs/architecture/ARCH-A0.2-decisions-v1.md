# ARCH-A0.2 — Registre des décisions et plan de conformité 1.0.0-rc.1

**Référence :** `main@0f359979235d189cb3059c86224dba738d176e4c`. **Statut :** décisions du mainteneur 7/7, gouvernance et SPEC-01..03 explicitement approuvées le 2026-11-03 ; **intégration Git non réalisée** ; `prepared_base_sha=null`. Le JSON de provenance extérieur au dépôt `REGISTRE-DECISIONS-MAINTENEUR.json` conserve le texte détaillé et les réserves de chaque lot.

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
| WIRE-03 | Validation automatique peut émettre ValidationProblemDetails | Intégrateur API : unifier les erreurs non-2xx. |
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
- Approbateur : **mainteneur du projet** (message explicite KnowledgeHub du 2026-11-03) ; SHA d'intégration : **non créé** ; `prepared_base_sha` : **null**.
- Gel normatif approuvé, **non fusionné et non implémenté** ; ne déduit aucun résultat .NET des validations documentaires.
