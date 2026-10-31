# KH-004-M003.1/M003.2 — Stable identity and incremental import

## Identity rules

- `CorpusDocument.Id` (non-empty GUID) becomes `KnowledgeDocument.ExternalId` (nullable GUID). The database PK remains a separate, immutable GUID.
- `ExternalId` is unique among non-null values. Two documents may have the same title if their external IDs differ.
- `Guid.Empty` preserves the previous *legacy* title-based creation/skip/repair behavior. It never claims or overwrites a document with an explicit external identity. Rename and incremental updates require a stable GUID.
- A duplicate non-empty GUID in the same corpus fails validation **before any HTTP write**. Invalid or whitespace-only documents are skipped as in M002.
- Existing documents have `ExternalId=NULL` after migration. No title-based automatic attribution is performed: claiming an existing legacy document would be an explicit follow-up migration, not an implicit overwrite.

## Synchronization

`POST /api/documents/import` accepts `CreateDocumentDto` with `ExternalId` and returns `ImportDocumentResultDto` with a typed outcome:

- `Created`: external ID not found; inserts document/chunks/embeddings atomically.
- `UpdatedContent`: content differs; prepares all embeddings before the DB transaction, replaces chunks/embeddings atomically, updates metadata and invalidates cache.
- `UpdatedMetadata`: title/category/source/tags/summary/keywords differ, content identical; keeps chunk and embedding IDs; refreshes the vector cache (titles may have changed).
- `RepairedIndex`: unchanged content but incomplete legacy index; regenerates its chunks and embeddings.
- `Unchanged`: same normalized content and metadata, valid index; no write or embedding.

The document's content SHA-256 is calculated from the UTF-8 encoding of its trimmed content. The comparison also checks the content string, protecting against a stale or corrupted hash. `ContentSha256` is nullable for pre-existing records; those use a freshly calculated hash when needed. A content-only change has the same transactional behavior as existing `DocumentService.UpdateAsync`.

## Safety and limits

- No automatic `dotnet ef database update` or database reset. Apply migrations to a backed-up **development** database only after review. Testcontainers integration tests migrate their isolated databases.
- Existing public CRUD contract remains valid. The import endpoint is not authenticated; run API/importer **locally only** until KH-004-M003.5 introduces authorization.
- Concurrent imports for the same new external ID are rejected by the unique index; a friendly conflict/retry workflow is deferred to M003.3. Do not interpret a concurrent 500 response as an overwrite.
- Importer still performs a full document listing for legacy-ID entries. Pagination, checkpointing, retries and large-corpus optimization are deferred to M003.3/M003.4.
- SHA-256 proves equality of normalized content, not authorization, semantic equivalence or uniqueness of a document.

Validation required on the target machine: the authoring environment lacks the .NET 10 SDK, NuGet restore and Docker.
