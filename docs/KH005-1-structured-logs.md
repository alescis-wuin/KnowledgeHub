# KH-005.1 — Structured application logs

## Scope

- API: existing Serilog integration remains in place. Console and rolling file
  sinks produce one JSON object per line (`logs/log-YYYYMMDD.jsonl`).
  Retention: seven rolling daily files; the `logs/` directory is gitignored.
- HTTP: one `HttpRequestCompleted` (`51001`) event per request, with a scoped
  `CorrelationId`. The response header is `X-Correlation-ID`.
  An incoming ID is accepted only if 8–64 ASCII alphanumeric, `-` or `_`.
  Invalid values are discarded; the server-generated trace identifier is used.
- Application: `DocumentCreated` (52001), `DocumentImported` (52002),
  `DocumentUpdated` (52003), `DocumentDeleted` (52004),
  `DocumentIndexRepaired` (52005).
- RAG: `RagSkipped` (53001), `RagCompleted` (53002), with duration and source
  count but not the user's question, retrieved passages or answer.
- CorpusImporter CLI: `Microsoft.Extensions.Logging` JSON console output;
  per-document `CorpusDocumentProcessed` (54002) and aggregate
  `CorpusImportCompleted` (54001) events.
- Errors: API logs exception **types**, never exception messages or stack traces;
  HTTP 500 responses use a generic error message. A 400 response retains its
  existing client-facing validation detail.

## Data safety

Do not log document titles, content, excerpts, prompts, embeddings, source
URLs, request query strings/bodies, HTTP headers, connection strings or
corpus filesystem paths. The HTTP event uses the **route template** (e.g.,
`api/documents/{id:guid}`), not the actual request path. Events contain stable
numeric identifiers and low-cardinality outcomes only. Document GUIDs and
external GUIDs are operational identifiers: control access to logs.

Existing third-party/framework logs, optional EF Core sensitive logging and
non-API processes require separate review; this milestone does not claim
universal redaction. Do not enable sensitive-data logging in EF Core.

## Tests / acceptance

1. Build API, CorpusImporter, Unit and Integration in Release with zero warnings.
2. Run all unit tests and isolated PostgreSQL Testcontainers integration tests.
3. Confirm correlation header propagation and rejection of unsafe IDs.
4. Confirm structured event IDs and absence of raw request URLs, queries and
   document payloads in the new request-completion events.
5. Inspect JSONL from a local API invocation; confirm `CorrelationId`,
   `HttpMethod`, `RoutePattern`, `StatusCode`, `ElapsedMs`, timestamp and level.

OpenTelemetry spans and meters belong to KH-005.2 and KH-005.3, not here.
