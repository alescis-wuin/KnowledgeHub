# KH-006 — Native Ollama usage observability

Base Git: `d332fd635b0a9e3992d53692b28a744fd327797d`.
Branch: `feature/kh-006-ollama-native-metrics`.

## Scope

Continue KH-005 tracing, metrics and synthetic E2E verification with **server-reported**
Ollama statistics. Keep the existing `/api/chat` and legacy `/api/embeddings`
requests, fixed models and returned application DTOs unchanged. No model download,
remote service, database migration, evaluator or user content collection is added.

The official Ollama usage schema exposes `total_duration`, `load_duration`,
`prompt_eval_duration`, `eval_duration` as **nanoseconds**, and
`prompt_eval_count`, optional `prompt_eval_cached_count`, `eval_count` as token
counts. The older `/api/embeddings` endpoint may omit these fields entirely.
Do not fabricate numbers when the API does not return them.

References:
- https://github.com/ollama/ollama/blob/main/docs/api/usage.mdx
- https://github.com/ollama/ollama/blob/main/docs/api.md

## Instruments

| Instrument | Type | Unit | Allowed labels |
| --- | --- | --- | --- |
| `kh.ollama.native.duration` | histogram double | ms | `operation` (`chat`/`embedding`), `phase` (`total`/`load`/`prompt_eval`/`eval`) |
| `kh.ollama.native.tokens` | histogram long | `{token}` | `operation` (`chat`/`embedding`), `kind` (`prompt`/`cached_prompt`/`output`) |
| `kh.ollama.native.output.tokens_per_second` | histogram double | tokens/s | `operation=chat` |

The existing `kh.ollama.duration` is **client-side elapsed time**, including
HTTP/JSON overhead. Native `total` is the server-reported duration and is not
substituted for the client duration. Generation throughput is computed only
with positive values for both `eval_count` and `eval_duration`:
`eval_count * 1e9 / eval_duration`, in tokens/s. It is not calculated for
embeddings. Prompt cache tokens are reported **separately** and not subtracted
from the reported prompt total.

Span attributes on `ollama.chat` and `ollama.embed` are numeric-only with the
`kh.ollama.` prefix: prompt/cached/output token counts, total/load/prompt
evaluation/generation durations in ms, and chat output throughput when valid.
They are added to `SafeTraceProcessor`'s exact allowlist, without wildcard
matching or expanding allowed string attributes.

## Compatibility and privacy

- Optional usage fields are read from JSON extension data as `JsonElement`:
  missing, negative, non-integral, non-numeric, null or values outside signed
  64-bit range are ignored. A valid answer is never invalidated by malformed
  optional usage fields; `0` is a legitimate reported value but never a default.
- Metadata is collected only after a successful usable chat result, or a
  successfully parsed embedding result. HTTP or content failure never creates
  native success statistics; existing error/cancellation metrics still apply.
- No user question, prompt, answer, document, title, raw response, model name,
  URL, exception text, `thinking`/reasoning field, token string or trace ID is
  a metric label or native trace attribute. Only fixed dimensions are used.
- No additional exporter or endpoint is activated. Existing opt-in OTLP settings
  and sampling are preserved; native metrics are aggregated, not per-trace.

## Acceptance on the user workstation

1. Restore NuGet and build all six existing .NET projects in Release with zero
   warnings/errors; run the full xUnit unit/integration suites with Testcontainers.
2. Unit tests use an in-memory HTTP handler to exercise real chat/legacy
   embedding deserialization: all supported fields, missing/negative/malformed
   values, invalid content/HTTP failures, fixed label vocabulary, absence of
   private marker text from metrics and traces, and SafeTraceProcessor removal.
3. Extend `KnowledgeHub.Observability.Verify`'s isolated PostgreSQL + API +
   synthetic Ollama + loopback OTLP/HTTP E2E with fixed realistic statistics.
   Require actual exported `kh.ollama.native.duration`, `.tokens` and
   `.output.tokens_per_second` in addition to the KH-005.4 trace hierarchy,
   correlation/privacy assertions, and synthetic benchmark report.
4. Keep Docker preflight, transactional backup and verified rollback. Execution
   starts only at the canonical Downloads directory; never replay an ID.

The existing synthetic p50/p95/CPU/memory report remains **synthetic** and
must not be interpreted as an energy or production-throughput benchmark.
This lot does not implement GPU wattage/energy estimation, cost-based model
routing, model evaluation or real LLM benchmarking.
