# KH-008.4 — Endpoint de RAG vérifié (contrat conservateur)

## Surface HTTP

`POST /api/rag/ask/verified` accepte le corps existant `{ "question": "..." }`
et retourne un objet `{ "verdict", "answer", "claims" }`. Chaque claim publié
ne peut provenir que d'une assertion ayant le statut `Supported`, et contient
les seules citations exactes admises. Les statuts publics sont `ANSWER`,
`PARTIAL`, `ABSTAIN` et `CONFLICT`. Lors de `ABSTAIN` ou `CONFLICT`, la liste
`claims` est toujours vide ; aucun texte de claim rejeté n'est exposé.

Le endpoint historique `POST /api/rag/ask` et ses DTO restent inchangés.

## Pipeline et confiance

1. Sans implémentation indépendante enregistrée de `IQualifiedEvidenceAssessor`
   **et** identité `Verification:TrustedSemanticPolicyId`, retourner `ABSTAIN`
   sans effectuer de récupération vectorielle ni d'appel génératif.
2. Sinon, effectuer la recherche serveur ; capturer les chunks courants en SQL,
   rejeter un résultat obsolète, invalide ou incohérent avec PostgreSQL.
3. Proposer un JSON strict par Ollama depuis les **chunks persistés autorisés**.
   Le modèle ne peut donner ni verdict ni source supplémentaire ; en cas de
   réponse mal formée, d'erreur ou d'absence de propositions, `ABSTAIN`.
4. Vérifier indépendamment les ancrages UTF-16 et chaque relation sémantique.
   `VerifiedVerdictService` revalide PostgreSQL après cette évaluation.
5. Effectuer une ultime revalidation PostgreSQL avant de construire le DTO HTTP.
   Si le snapshot est périmé, refuser la publication factuelle.
6. Ne sérialiser **jamais** `VerifiedEvidenceAnswer.Claims` directement. Le DTO
   public exclut toute assertion non `Supported` et ne renvoie ni prompt, ni
   question brute, ni diagnostic privé.

## TOCTOU et garantie réelle

Les deux revalidations réduisent la fenêtre de modification concurrente.
Elles n'offrent **pas** d'atomicité entre la lecture PostgreSQL et l'émission
HTTP : une écriture peut encore être validée juste après le dernier contrôle.
La réponse est donc cohérente à l'instant de la dernière revalidation réussie,
non garantie être la version la plus récente lors de sa réception. Une garantie
forte réclamerait un contrat transactionnel de lecture/décision/publication,
ou des versions immuables référencées et identifiables.

## Sécurité et déploiement

- Aucun évaluateur qualifié n'est livré. Par défaut, le endpoint **s'abstient** ;
  ses tests de `ANSWER`/`PARTIAL` utilisent des doubles synthétiques isolés.
- Aucun contenu de question, prompt, document, extrait ou sortie brute Ollama
  n'est ajouté aux logs et traces.
- Les documents sont considérés comme des données non fiables, pas des
  instructions système. Le verdict et le rendu sont décidés exclusivement en C#.
- Conserver le service sur l'interface loopback tant qu'il n'existe pas
  d'authentification et d'autorisation. Aucun changement de binding réseau.
- Aucun schéma EF, migration, suppression de volumes ou écriture en base de
  développement dans ce jalon. L'E2E HTTP utilise une base Testcontainers isolée.
- La qualité d'un futur évaluateur sémantique devra être démontrée séparément.
## Compatibilite Windows 11 et revue Codex

Les deux observations Codex sur les propositions Ollama sont traitees dans le code :
la generation utilise `KnowledgeHubMetrics.MeasureOllamaAsync("chat", ...)`,
`KnowledgeHubActivities.TraceAsync("ollama.chat", ...)` et la telemetrie native
bornee, sans exporter le contenu des documents ou des reponses. Un jeton lie
impose une limite temporelle durant la reception des en-tetes, la lecture du
corps et son analyse JSON (meme avec `ResponseHeadersRead`). Les tests unitaires
couvrent le timeout du corps, l'annulation par l'appelant et la telemetrie.

Sur Windows, le verificateur KH-005.4 ouvre les journaux Serilog avec
`FileShare.ReadWrite | FileShare.Delete` pour permettre leur lecture concurrente.
Le corpus oracle KH-007 utilise des empreintes SHA-256 sur les octets exacts :
`.gitattributes` impose `eol=lf` pour les trois fichiers JSON de reference.
Les donnees de reference ne doivent pas etre reecrites pour faire passer le test.

Aucune CI GitHub Actions n'est configuree pour ce jalon. Les validations de la PR
sont des preuves locales : compilation Release avec avertissements traites comme
erreurs, `dotnet format`, tests unitaires et d'integration, reference offline
KH-007 et verification synthetique KH-005.4 via Testcontainers. Le client MAUI
Windows est un lot distinct. Aucun evaluateur semantique qualifie n'est encore
livre : la reponse verifiee conserve son abstention par defaut.
