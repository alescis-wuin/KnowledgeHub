# KH-005.1.1 — Ollama chat output safety

## Scope

- Remove raw `/api/chat` response dumps from standard output. Responses may include confidential prompts, answer text, or private reasoning.
- Deserialize the JSON response without first converting it into a string for logging.
- Accept only non-empty, non-whitespace `message.content` as the answer.
- Never return `message.thinking` as an answer, including when `content` is absent or whitespace-only. The response DTO intentionally has no `thinking` property.
- Convert malformed JSON to a fixed `InvalidDataException` message without attaching or quoting the original parser error.
- Reject null, absent or empty answer content with a distinct, fixed error message. Preserve HTTP failures and cancellation semantics.

## Invariants

- No change to model selection, prompts, HTTP route, or request parameter values.
- No database migration, API contract change, or new NuGet dependency.
- The API error middleware continues to return a generic HTTP 500 for these failures.
- No raw model payload, prompt, retrieved document passage or reasoning in newly emitted logs.
- This milestone does not claim to redact logs emitted by Ollama itself, external HTTP diagnostics or third-party middleware.

## Required validation

- Build Infrastructure, API, Unit and Integration Release with warnings treated as errors.
- Execute the complete existing test suites and the new no-network fake-Ollama unit tests.
- Verify success, malformed JSON, missing answer, thinking-only response, and HTTP error behavior.
- Inspect the first local successful RAG trace after deployment to ensure no `OLLAMA CHAT RAW RESPONSE` marker or model JSON appears in stdout.
- Rollback must restore exactly the changed source and remove added test/documentation files if validation fails.
