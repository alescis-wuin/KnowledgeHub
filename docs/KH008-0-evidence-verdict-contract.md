# KH-008.0 — Contrat de preuve et verdict documentaire

Statut : **spécification initiale vérifiée contre `main`**, et non implémentation.
Base Git : `018057727a99868eb54ab8d7f6cd5b36954adcfc` (PR #11).
Périmètre : KnowledgeHub uniquement, .NET 10, API locale, EF Core/PostgreSQL, Ollama.

## 1. État réel et points d'intégration

| Source actuelle | Contrat observable | Conséquence KH-008 |
|---|---|---|
| `Infrastructure/AI/RagService.cs` | `BuildAsync(question, 2)`, génération libre si contexte non vide | Le nombre de sources ne constitue pas un seuil d'évidence ; introduire un chemin vérifié distinct |
| `Infrastructure/AI/ContextBuilderService.cs` | Texte `SOURCE n`, score, titre, contenu | Ajouter des identités stables dans le contexte proposé au LLM et conserver la table d'identités côté C# |
| `Application/DTOs/VectorSearchResultDto.cs` | `ChunkId`, `DocumentId`, `Title`, `Content`, `Score` | IDs déjà disponibles en interne ; hash de chunk/version documentaire encore absents |
| `Infrastructure/AI/VectorSearchService.cs` | Cosinus en mémoire, groupement par `DocumentId`, `topK` | Un seul chunk/document dans les résultats ; pas de preuve de couverture ni de contradiction exhaustive |
| `Infrastructure/AI/VectorIndexCache.cs` | Cache mémoire d'entrées sans hash/version | Snapshot et invalidation à lier aux citations, vérifier l'absence de changement concurrent |
| `Domain/Entities/KnowledgeDocument.cs` | `ContentSha256` = SHA-256 UTF-8 du contenu `Trim()` | Les anciens documents peuvent avoir une empreinte nulle ; calcul fiable seulement depuis le contenu autoritatif relu |
| `Domain/Entities/DocumentChunk.cs` | `Content` stocké après `Trim()`, `Id`, `DocumentId` | Définir la citation par rapport au *chunk effectivement persisté*, sans normalisation ultérieure |
| `Application/DTOs/AskQuestionResponseDto.cs` | `Question`, `Answer`, `Sources` | Pas de verdict ni d'identité des passages ; maintenir le contrat hérité pendant la transition |
| `Application/DTOs/SourceReferenceDto.cs` | titre, score, pourcentage, extrait tronqué | L'extrait 250 caractères n'est pas une preuve exploitable et ne porte aucun ID |
| `Application/Interfaces/IChatCompletionService.cs` | génération d'une chaîne à partir de question/contexte | Séparer proposition structurée et présentation ; ne pas promouvoir cette chaîne en preuve |
| `Api/Controllers/RagController.cs` | `POST /api/rag/ask` | Ajouter ultérieurement un chemin vérifié rétrocompatible avant le basculement |

Les snapshots du dossier de reprise ont le même *Git blob SHA-1* que ces fichiers sur la base citée. Les noms de nouveaux DTO/interfaces ci-dessous sont une proposition ; ils **n'existent pas** encore.

## 2. Modèle de confiance

1. Le texte des documents, les réponses Ollama et tous les champs proposés par le modèle sont **non fiables** ; ils ne peuvent ni décider du statut, ni modifier les sources autorisées.
2. Les sources autorisées proviennent d'un snapshot de récupération construit par C# pour *cette* requête, pas d'une recherche par ID arbitraire suggéré par le modèle.
3. C# recalcule les empreintes et contrôle `documentId`, `chunkId`, offset, longueur et texte exact. La revendication d'empreinte provenant du modèle n'est jamais l'autorité.
4. Les positions sont définies sur `string` .NET, en **unités UTF-16**, intervalle demi-ouvert `[startUtf16, startUtf16 + lengthUtf16)`. `quote` doit être identique à `chunk.Content.Substring(...)`, sans `Trim()`, sans conversion Unicode, sans correction CRLF.
5. La version/empreinte du chunk est `SHA256(UTF8(chunk.Content))` en hexadécimal minuscule, calculée par le serveur. `KnowledgeDocument.ContentSha256` est une autre empreinte, calculée sur le contenu documentaire canonique `Trim()` ; ne pas les substituer.
6. Une citation textuellement exacte **ne prouve pas** l'implication sémantique de l'assertion. Toute relation non évaluée ou incertaine retourne `INSUFFICIENT_EVIDENCE`.
7. En cas de modification ou suppression de document entre récupération et décision, revalider le snapshot ou ne rien approuver. `ExternalId` peut être nul ; `DocumentId` et `ChunkId` restent requis.
8. La non-présence d'une preuve ne justifie **jamais** une négation factuelle ; `ABSTAIN` ne signifie pas `FALSE`.

## 3. Contrat de proposition (non exposé directement comme réponse HTTP)

Proposition de DTO : `ClaimProposalBatch`, `ClaimProposal`, `EvidencePointer`.

```json
{
  "schemaVersion": 1,
  "claims": [{
    "id": "c1",
    "statement": "Une proposition factuelle atomique.",
    "evidence": [{
      "documentId": "11111111-1111-4111-8111-111111111111",
      "chunkId": "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa",
      "startUtf16": 0,
      "lengthUtf16": 12,
      "quote": "Extrait exact",
      "chunkSha256": "empreinte-proposee-a-verifier-par-le-serveur"
    }]
  }]
}
```

Ce JSON est **illustratif et volontairement non valide** pour une citation réelle (empreinte et contenu fictifs). Aucun exemple ne doit être traité comme une preuve observée.

Contraintes proposées pour la première implémentation : `schemaVersion` exact, propriétés inconnues et doublons rejetés ; au plus 12 propositions, 3 preuves par proposition, 600 unités UTF-16 par citation, taille totale JSON bornée à 64 KiB ; IDs non vides et uniques ; chaîne d'assertion non vide ; pas de nombres négatifs, dépassement arithmétique ni doublon de sources. L'absence d'un champ obligatoire entraîne un refus contrôlé. Ne pas ignorer silencieusement des propositions invalides pour produire un verdict global `ANSWER`.

## 4. Deux décisions distinctes

**Validation structurelle (`EvidenceAnchorVerifier` proposé)**

- `VALID_ANCHOR` : le chunk appartient au document attendu, appartient au snapshot autorisé et sa version est cohérente ; offset/longueur/texte exacts.
- `INVALID_ANCHOR` : identité absente, mauvaise version, citation fabriquée, index incohérent, champ manquant ou contenu modifié.

**Validation de sens (`ISemanticEvidenceVerifier` proposé)**

- `ENTAILS` : relation d'implication explicitement reconnue par un vérificateur indépendant et identifié, avec niveau de confiance et politique préalablement établis.
- `CONTRADICTS` : contradiction identifiée sur le même fait, sous les mêmes réserves.
- `CONFLICT` : preuves admissibles incompatibles sans règle d'arbitrage sûre.
- `UNKNOWN` : incertitude, modèle indisponible ou validation sémantique non configurée.

Un LLM d'entailment reste probabiliste. **Sans vérification sémantique qualifiée**, la politique par défaut est `UNKNOWN` : la présence d'une citation exacte ne peut pas produire `SUPPORTED`.

La fonction C# de décision reste pure/déterministe à partir des ancrages et des **résultats qualifiés** du vérificateur : statut par proposition `SUPPORTED`, `INSUFFICIENT_EVIDENCE`, `INVALID_EVIDENCE`, `CONTRADICTED`, `CONFLICTING_EVIDENCE`. Le mot `SUPPORTED` signifie *soutenu selon la politique de vérification*, pas démonstration absolue de vérité.

## 5. Verdict global et réponse finale

- `ANSWER` : toutes les propositions publiées sont `SUPPORTED` et la couverture des points demandés a été validée. Cette couverture ne peut pas être déduite uniquement du nombre de claims générés par le LLM.
- `PARTIAL` : au moins un point vérifiable a été approuvé, et des points restent insuffisants ; les points non approuvés ne sont pas présentés comme des faits.
- `ABSTAIN` : aucune proposition soutenue ou structure invalide, JSON non fiable, source périmée, contrôle de couverture inconnu.
- `CONFLICT` : contradiction documentaire non résolue ; exposer les éléments contradictoires et leur provenance sans trancher artificiellement.

Le statut global est un champ construit par C#, non un champ libre du modèle. Pour la sortie finale, privilégier **un assembleur C# déterministe** des propositions acceptées avec leurs citations et une formulation explicite des limites. Ollama peut proposer un *ordre de présentation* ou un identifiant de transition appartenant à une liste fermée, mais ne doit pas pouvoir insérer de faits, d'extraits ou de verdicts nouveaux. Une synthèse libre qui reformule les assertions ne peut pas être garantie sûre par simple comparaison de sous-chaînes : soit la revalider sémantiquement avec abstention possible, soit ne pas la publier et utiliser le rendu C#.

Transition HTTP proposée : `POST /api/rag/ask/verified`, réponse structurée avec `verdict`, `claims`, citations (`documentId`, `chunkId`, `startUtf16`, `lengthUtf16`, `quote`, empreintes serveur) et texte déterministe. `POST /api/rag/ask` et les DTO existants restent **inchangés** lors de l'intégration initiale ; basculement du comportement par défaut seulement après tests E2E et revue de compatibilité. L'API doit rester sur loopback tant que l'authentification n'est pas en place.

## 6. Invariants non négociables

- Pas de migration EF ni modification de la base de développement dans KH008.0 ; ne jamais supprimer ses volumes.
- Pas de réintroduction de code spécifique FiQA ni de pgvector sans décision séparée.
- Pas de logs contenant question, prompt, proposition, extrait, identité confidentielle brute ou document ; métriques et traces seulement sur des types/compteurs bornés.
- Aucun pipeline ne remplace `ContentSha256` existant par un hash de chunk.
- Les tests synthétiques de contrat ne mesurent ni performance du retrieval réel, ni vérité générale des réponses.

## 7. Ordre d'intégration recommandé

1. **KH008.0 (ce lot)** : spécification, frontière de confiance, matrice adversariale réutilisable et plan de tests ; aucun code produit.
2. **KH008.1** : parseur JSON strict, DTOs, limites, vérificateur d'ancrage pur C#, tests unitaires sans Ollama/SQL.
3. **KH008.2** : récupération versionnée, revalidation des changements concurrents, API interne de vérification ; tests Testcontainers sans toucher la DB de dev.
4. **KH008.3** : décisions de sens/couverture, politique `UNKNOWN` fail-closed, rendu C# contraint ; tests de contradiction, abstention et injection.
5. **KH008.4** : endpoint vérifié, télémétrie expurgée, E2E Ollama simulé, campagne réelle ultérieure KH009 dans une base isolée.

KH008.1–.4 ne sont **pas** réalisés par ce paquet. Leur acceptation exige des rapports nouveaux et distincts de l'oracle KH007.
