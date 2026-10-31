# KH-005.2 — Traces distribuées (lot unique)

## Périmètre

- API : instrumentation OpenTelemetry 1.20.1 (ASP.NET Core et HttpClient) pour les requêtes HTTP entrantes et les appels sortants, notamment Ollama.
- Source métier : `KnowledgeHub` via `System.Diagnostics.ActivitySource`, sans couplage du coeur aux exporters.
- Spans métiers : `rag.ask`, `rag.retrieve`, `rag.context`, `rag.generate`, `vector_search.search`, `vector_search.rank`, `vector_index.load`, `vector_index.invalidate`, `ollama.chat`, `ollama.embed`, `document.index`, `document.import`, `document.update`, `document.index.repair`, `document.embed_batch`.
- Le contexte W3C `traceparent` est propagé vers les clients HTTP. Le propagateur ne transmet pas de `baggage` fourni par un appelant.
- Les erreurs renseignent uniquement le type d'exception et un état d'échec ; une annulation demandée est classée `cancelled` sans enregistrer l'exception.
- Les logs structurés contiennent aussi `TraceId` et `SpanId` lorsque disponibles. `X-Correlation-ID` reste une notion distincte du contexte W3C.

## Configuration (variables d'environnement .NET)

| Variable | Valeur par défaut | Rôle |
|---|---|---|
| `Tracing__Enabled` | `true` | Activer le collecteur de traces ; `false` désactive l'enregistrement OpenTelemetry |
| `Tracing__SampleRatio` | `1.0` | Taux de collecte entre 0 et 1, avec échantillonnage parent-based |
| `Tracing__OtlpEndpoint` | absente | Pas d'export OTLP tant que cette valeur n'est pas fournie |

Pour une instance **locale**, utiliser `Tracing__OtlpEndpoint=http://127.0.0.1:4317` avec un collecteur OTLP/gRPC lancé séparément. Un endpoint non local exige HTTPS. UserInfo, query et fragments sont refusés. Aucune installation ni lancement de collecteur n'est effectué par le paquet. Ne pas pointer vers un collecteur tiers sans validation des règles de confidentialité.

## Confidentialité

- Un processeur (`SafeTraceProcessor`) supprime avant export tous les attributs non explicitement autorisés et remplace le nom des spans HTTP par des libellés fixes.
- Les valeurs des prompts, questions, réponses, extraits, titres, chemins de documents, corps HTTP, URLs et paramètres de requête ne sont **pas** ajoutées par l'instrumentation métier.
- Les exceptions ne sont jamais stockées en `ActivityEvent` par ce lot ; `RecordException=false` sur les deux instrumentations HTTP.
- N'activer aucune instrumentation externe supplémentaire sans revoir les nouvelles catégories d'attributs et événements qu'elle émet.
- Les identifiants de trace ne sont pas une authentification et ne doivent pas être utilisés comme contrôle d'accès.

## Validation cible

1. SDK .NET `10.0.403`, restauration NuGet des quatre packages épinglés `1.20.1`, builds Release sans avertissement.
2. Tous les tests unitaires et intégration PostgreSQL/Testcontainers passent avec serveur de compilation désactivé.
3. Tests xUnit : hiérarchie `rag.ask`/`rag.retrieve`/`rag.generate`, échec sans message privé, annulation, purification des attributs HTTP, propagation `traceparent` via Ollama simulé.
4. Une requête réelle `POST /api/rag/ask` avec un collecteur OTLP **local** montre une trace complète et un sous-arbre HTTP client.
5. Vérifier que les données documentaires n'apparaissent ni dans les spans exportés ni dans les logs.

## Exclusions

Pas de métriques `Meter` (KH-005.3), pas de timings/tokens natifs Ollama (KH-006), pas de déploiement collector/Grafana/Tempo, pas de migration de base, pas de changement du verdict RAG ou du modèle.

## Correctif de qualification des tests (v2)

Le premier run (`run-7e7148ccfc57491c932ba0b3a17fabe4`) a construit les cinq
projets, puis échoué sur deux tests de télémétrie. Les listeners d'Activity sont
process-wide : les assertions de spans utilisent désormais un contexte de trace
unique pour écarter les activités concurrentes. Le test de propagation HTTP
utilise un serveur TCP loopback à requête unique et `SocketsHttpHandler` : un
`HttpMessageHandler` fictif contourne l'instrumentation réseau native de .NET 10.
Aucun changement du code métier de KH-005.2 v1 n'est introduit dans ce correctif.
