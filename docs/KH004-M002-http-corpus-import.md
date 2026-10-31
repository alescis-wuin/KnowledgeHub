# KH004-M002 — HTTP corpus import

## Architecture

`KnowledgeHub.CorpusImporter` is an **HTTP-only** client. It does not reference Entity Framework Core, Infrastructure, Domain, PostgreSQL or Ollama. The API is responsible for all document writes, chunking, embedding generation, transactional commits and vector cache invalidation. The importer calls the existing endpoints for categories, sources, tags and documents, plus the new `POST /api/documents/{id}/ensure-index` endpoint to repair an incomplete legacy document index.

The `CreateDocumentDto` contract now accepts optional `Summary` and `SearchKeywords`, preserved in the database for newly imported documents. Existing documents remain unchanged on a repeated import; an incomplete index is rebuilt from **the database content**, not from potentially old corpus contents. Per-document durability is inherited from `DocumentService` and `KnowledgeDocumentRepository`.

## Run locally

1. Start PostgreSQL and the embedding provider (Ollama) required by `KnowledgeHub.Api`.
2. Start the API, e.g. `dotnet run --project KnowledgeHub.Api --launch-profile https`.
3. In another terminal at the repository root, run:

```bash
export KNOWLEDGEHUB_API_URL='https://localhost:7141'
dotnet run --project KnowledgeHub.CorpusImporter -- path/to/corpus.json
```

For plain HTTP development use `http://localhost:5115`. The importer refuses **non-loopback HTTP** and refuses automatic HTTP redirects. It uses the operating system's HTTPS trust settings; never bypass certificate verification in code. `KNOWLEDGEHUB_API_URL` must be configured explicitly to avoid accidentally importing into the wrong database. The optional file argument defaults to `Data/KnowledgeHub-corpus-documentaire.json` relative to the current working directory.

Only use a development API bound to loopback on an isolated workstation for now: **the current API has no authentication or authorization**. Do not expose its write endpoints (including index repair) to an untrusted network. Adding authenticated writes and robust import retries is a KH004-M003 requirement before a production deployment.

## Failure and recovery

- The first non-success HTTP response stops the importer without hiding the error. Previously committed documents remain committed.
- Running the same corpus again skips fully indexed documents and repairs incomplete legacy document indexes.
- Metadata creation and the overall corpus are **not** covered by a single transaction; duplicate creation during concurrent imports is not yet coordinated.
- The client currently requests complete metadata/document listings and processes documents sequentially. Pagination, conflict handling, bounded batching, persistent import checkpoints, cancellation across network failures and true bulk throughput belong to KH004-M003.
- With one API process, all writes invalidate that API's vector cache. Multi-instance API cache coherence is still a separate consideration.

## Tests

`CorpusImporterIntegrationTests` exercises HTTP importation against the in-process ASP.NET Core host backed by an isolated PostgreSQL Testcontainers database, with deterministic embeddings and without Ollama. It verifies metadata and embeddings, immediate vector cache refresh, idempotent reimport, legacy repair, atomic failure/recovery and missing-document semantics. No GitHub Actions are added.
