# KH-008.2 — Snapshots documentaires et revalidation

Statut : candidat pour préqualification isolée ; non intégré tant que l'application
transactionnelle et son rapport ne sont pas acceptés.

Base : `3422c8c71748aab0abdec73cca0fccfc500c1fd3` (KH-008.1, PR #13).

## Frontière de confiance

- `IEvidenceSnapshotProvider.CaptureAsync` reçoit exclusivement les résultats du
  retrieval côté serveur ; il refuse les IDs/duplications invalides et toute
  discordance avec les chunks relus directement dans PostgreSQL.
- Une unique requête SQL projette les chunks et contenus documentaires courants,
  sans embedding et sans construire les droits depuis un identifiant fourni par
  Ollama. Le périmètre admis est un sous-ensemble fermé de ce retrieval.
- Un `EvidenceRetrievalSnapshot` contient les octets de chunk persistés et les
  empreintes SHA-256 *documentaires* recalculées depuis le contenu canonique ;
  l'empreinte de chaque chunk reste celle calculée par `EvidenceSnapshot`.
- Les anciennes lignes dont `Documents.ContentSha256` est `NULL` sont acceptées
  uniquement si leur contenu documentaire actuel permet le recalcul.
- Un hash documentaire stocké non nul et incohérent bloque le snapshot.
- `RevalidateAsync` effectue une nouvelle requête PostgreSQL cohérente pour
  détecter suppression, remplacement de chunk, modification de contenu de chunk
  ou de document. Un changement de titre/métadonnées seules reste valide.
- Le statut `Fresh` atteste une égalité de contenu **au moment de la requête**.
  Aucune transaction n'est maintenue entre capture et revalidation : tout
  consommateur doit revalider immédiatement avant une décision. Une mutation
  postérieure à la revalidation reste un risque TOCTOU jusqu'à ce qu'un contrat
  atomique de décision/persistance soit construit.
- `Fresh` et `ValidAnchor` ne valent **jamais** `SUPPORTED`. La politique
  sémantique fail-closed appartient à KH-008.3.

## Invariants et exclusions

- Pas de modification de `RagService`, de `VectorSearchService`, de la route
  `/api/rag/ask`, ni de l'invalidation du cache existant dans cette phase.
- Pas de migration, d'opération sur la base de développement ou de recréation
  de volume Docker. Les tests utilisent `IsolatedPostgresFixture` (Testcontainers).
- Pas de logging de question, prompt, chunk, citation, contenu documentaire,
  vecteur ou empreinte sensible.
- Pas d'enregistrement DI ni d'utilisation dans l'API HTTP avant les étapes
  KH-008.3/KH-008.4 ; le contrat est disponible pour la future intégration.
- L'API actuelle reste non vérifiée et rétrocompatible ; ne pas présenter cette
  préqualification comme une amélioration déjà active des réponses RAG.
