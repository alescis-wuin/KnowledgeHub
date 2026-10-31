# KH-005.4 — Observability end-to-end qualification

## Scope and execution

- Run `KnowledgeHub.Observability.Verify` against a dedicated Testcontainers PostgreSQL database and `WebApplicationFactory<Program>`; **never** reset the developer database.
- A loopback `HttpListener` with a random port serves deterministic `/api/chat` responses and accepts real OTLP/HTTP protobuf `POST /v1/traces` and `POST /v1/metrics` requests. It does **not** store question/context/answer request bodies in evidence.
- The API receives `POST /api/rag/ask` with W3C `traceparent` and `X-Correlation-ID`. Real RAG/context/Ollama chat services and ASP.NET middleware execute; only the vector retrieval and Ollama HTTP backend are synthetic. The request is handled via ASP.NET TestServer, not a production Kestrel socket.
- Validate a complete span tree (server → rag.ask → rag.retrieve/context/generate → ollama.chat → HTTP client) from serialized OTLP protobuf, matching binary trace IDs and parent span IDs.
- Validate one structured HTTP completion log containing the same W3C TraceId and the unique CorrelationId. No raw log lines, OTLP bodies, prompts or private texts are written to shareable reports.
- Validate exported `kh.rag.requests` and `kh.ollama.requests`; check that synthetic question, context, document title and answer tokens are absent from **all** OTLP trace/metric bodies and the matching structured log.
- No `trace_id` label is added to OpenTelemetry metrics: these are intentionally low-cardinality aggregate measurements. Their correlation to the request is through the isolated test window, not an individual metric label.

## OTLP protocol configuration

Existing `Tracing:OtlpEndpoint` and `Metrics:OtlpEndpoint` remain optional; exports remain off by default. gRPC is the default protocol, preserving the previous behavior.

For a local OTLP/HTTP protobuf collector, set:

| Variable | Value (example) |
| --- | --- |
| `Tracing__OtlpProtocol` | `http/protobuf` |
| `Tracing__OtlpEndpoint` | `http://127.0.0.1:4318/v1/traces` |
| `Metrics__OtlpProtocol` | `http/protobuf` |
| `Metrics__OtlpEndpoint` | `http://127.0.0.1:4318/v1/metrics` |
| `Metrics__ExportIntervalMilliseconds` | `1000` for short-lived local tests; `60000` default |

The exporter rejects wrong signal-specific paths for HTTP/protobuf, unsupported transports and interval values outside 1000..60000 milliseconds. Existing loopback HTTP / remote HTTPS, no-credentials/query/fragment endpoint checks remain unchanged. Do not export to a third-party collector without privacy and retention review.

## Measurement protocol

The executable emits `kh0054-observability.json` and `kh0054-observability.md` **inside the package runner's evidence directory**, both included in the ZIP report. It benchmarks 5 warmups and 25 synthetic HTTP requests per arm: tracing/metrics disabled and tracing/metrics enabled. It records request latency p50/p95, process CPU time, peak process working set, and managed allocations. **No numerical gain or threshold is assumed.**

These figures include the test process, local fake Ollama/collector and GC noise. They do not measure LLM inference, actual application throughput under load, or energy consumption. The test must not send data to external endpoints or require a developer database reset. `DOCKER_READY` and every full Release build/unit/integration suite remain mandatory. No automatic Git operations.

## Acceptance and follow-up

1. `E2E_OBSERVABILITY=pass` with both real OTLP HTTP signal requests and consistent span hierarchy.
2. Unique request's structured log contains `TraceId` and `CorrelationId` and does not reveal the test sentinels.
3. No test sentinel appears in any captured OTLP body; only sanitized observations are included in the report.
4. JSON and Markdown benchmark records exist with environment, samples, CPU, RSS and p50/p95; they are **measured** on the target environment, not predicted.
5. All .NET unit/integration tests and builds pass; results are eligible for human review before PR, not accepted in advance.

An optional later smoke test against a real local Ollama model may be added with explicit control of datasets and logging; it is not required for the isolated KH-005.4 qualification.

## Corrective qualification v2 — early minimal-host configuration

The initial v1 run (`run-b2945cef39ba44c1aed8107eb4533ec9`) passed all
three NuGet restores, all six Release builds, the full unit and PostgreSQL
integration suites, and issued HTTP 200 from the synthetic RAG endpoint. It
failed the mandatory `E2E_OBSERVABILITY` check after 25 seconds because the
expected OTLP trace/metric export conditions were not met; the transactional
rollback passed. The v1 package ID must never be replayed.

The verifier previously relied solely on `WebApplicationFactory`
`ConfigureAppConfiguration` to enable exporters. Under the .NET 10 minimal
hosting model, test configuration applied through this callback is not
available when `Program.cs` reads `builder.Configuration` *before* calling
`Build()`. The v2 verifier sets process-local, loopback-only telemetry settings
before constructing each synthetic host and restores the previous environment
values on disposal. It still uses the test-host configuration callbacks as a
second check; neither mechanism changes production defaults.

If OTLP export does not satisfy the unchanged seven-span and two-metric
requirements, the verifier now reports safe counters: number of OTLP HTTP
trace/metric POSTs, matching spans and matching required metric names. No raw
OTLP payloads, user prompts or log content are added to the report. All six
Release builds and both test suites remain mandatory; no new database effects
or production collector are introduced.
