# KH-005.3 — Application metrics

Base: `174bc266fa597ddfb029fc816ad2a279855bcc0c`, branch `feature/kh-005-metrics`.

## Scope

- Dedicated `System.Diagnostics.Metrics.Meter` (`KnowledgeHub.Metrics`, version `1.0.0`), independent of trace sampling.
- RAG request counter and total duration by **fixed** outcome: `ok`, `no_context`, `error`, `cancelled`.
- Stage durations for `retrieve`, `context`, `generate`; all recorded in milliseconds, including error/cancel outcomes where a cancellation token is available.
- Vector retrieval request counters, duration and number of results. No scores, document IDs or query text in labels.
- Vector index cache hit/miss counters, load latency (success/error/cancel), entries in completed loads, and successful invalidation operations.
- Ollama chat and embedding request counters/durations, by fixed operation and outcome. Detailed model tokens/timings are intentionally reserved for KH-006.
- Pure application metrics: no automatic URL or request-path HTTP metrics; no user question, documents, answer, prompt, exception message, headers, source URLs, model names, high-cardinality identifiers, or filesystem paths in attributes.

## Metrics inventory

| Instrument | Type | Unit | Attributes |
| --- | --- | --- | --- |
| `kh.rag.requests` | Counter<long> | `{request}` | `outcome` |
| `kh.rag.duration` | Histogram<double> | ms | `outcome` |
| `kh.rag.stage.duration` | Histogram<double> | ms | `stage`, `outcome` |
| `kh.retrieval.requests` | Counter<long> | `{request}` | `outcome` |
| `kh.retrieval.duration` | Histogram<double> | ms | `outcome` |
| `kh.retrieval.results` | Histogram<long> | `{result}` | none |
| `kh.vector_cache.lookups` | Counter<long> | `{lookup}` | `result` |
| `kh.vector_cache.load.duration` | Histogram<double> | ms | `outcome` |
| `kh.vector_cache.entries` | Histogram<long> | `{entry}` | none |
| `kh.vector_cache.invalidations` | Counter<long> | `{operation}` | none |
| `kh.ollama.requests` | Counter<long> | `{request}` | `operation`, `outcome` |
| `kh.ollama.duration` | Histogram<double> | ms | `operation`, `outcome` |

`no_context` is a normal RAG abstention, not a successful answer generation. Cache load duration measures the cold SQL load, excluding lock-wait time; load errors are recorded even without entries. Cache invalidations count completed calls to remove the cache key, not changes to DB state.

## Configuration and privacy

- `Metrics:Enabled` defaults to true. Setting it to `false` skips OpenTelemetry metrics provider registration; in-process meter calls may still execute for other listeners.
- No metrics destination exists by default. `Metrics:OtlpEndpoint` explicitly opts into OTLP export. Only HTTPS or loopback HTTP without URL credentials, query or fragment is allowed. The same OpenTelemetry SDK and exporter packages already installed for KH-005.2 are reused; no new dependency or container is added.
- Do **not** configure a public endpoint without reviewing network access, certificates and the collector's retention/access controls. This milestone does not implement a dashboard or enable metrics export implicitly.
- Tracing and metrics can be independently enabled/disabled; W3C propagation behavior is not changed.

## Acceptance

1. Restore unit/integration projects and build API, infrastructure, importer, unit and integration in Release with zero warnings/errors.
2. Run all unit and PostgreSQL Testcontainers integration tests; Docker preflight is required **before** any file change.
3. `MeterListener` tests prove RAG success and no-context, stages, retrieval counts, cache hit/miss/invalidation, Ollama cancellation and failure, and no private content in tags.
4. Configuration tests cover OTLP opt-in, rejection of external HTTP/credentials/query/fragment, and independent disabled mode.
5. On the target system only, optionally run a local OTLP collector to verify actual export and temporal aggregation; default operation requires no collector.

No migrations, database reset, external service start, Git commit, push, or remote write.
