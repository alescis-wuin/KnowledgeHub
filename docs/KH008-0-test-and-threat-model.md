# KH-008.0 — Matrice de conformité, menaces et preuves attendues

Référence : `docs/KH008-0-evidence-verdict-contract.md` ; base `018057727a99868eb54ab8d7f6cd5b36954adcfc`.

## 1. Risques à couvrir dès le code KH008.1

| Risque | Précondition | Comportement à démontrer |
|---|---|---|
| Faux extrait | Le modèle cite un texte absent du chunk | `INVALID_EVIDENCE`, aucun texte factuel publié |
| Mauvaise provenance | Citation exacte mais `chunkId` ou `documentId` erroné | Refus, sans résolution par titre approximatif |
| Offset malveillant | Négatif, débordement entier, mauvaise longueur, segment tronqué | Rejet avant `Substring`, pas de 500 non maîtrisé |
| Unicode | Emoji/surrogate pairs, accents combinés, CRLF | Indices UTF-16 .NET exacts, zéro normalisation implicite |
| Replay version périmée | Contenu mis à jour après récupération | Hash/versions discordants, refus ou nouvelle récupération explicite |
| Métadonnées seules | Titre modifié sans contenu modifié | Ne pas confondre identité/version de contenu et titre courant |
| Document historique | `ContentSha256` nul | Valeur recalculée depuis contenu fiable ou `INSUFFICIENT_EVIDENCE` |
| Vrai extrait / mauvaise conclusion | La citation ne soutient pas la proposition | `UNKNOWN` par défaut ; jamais `SUPPORTED` sur présence textuelle seule |
| Contradiction | Deux passages s'opposent | `CONFLICT` si validé, sans sélection arbitraire d'un seul passage |
| Injection indirecte | Un chunk ordonne « ignorer les règles » | Texte documentaire traité comme donnée, aucune autorité sur la politique |
| Réponse incomplète | Le modèle omet un élément important de la question | Pas de `ANSWER` sans mécanisme de couverture vérifié |
| JSON hostile | Doublons, propriétés inconnues, nesting, >64 KiB, objets inattendus | Refus contrôlé, limites et annulation respectées |
| Dérive synthèse | Ollama ajoute un fait non vérifié après les contrôles | Rendu C# ou abstention ; interdiction de publier directement cette chaîne |
| Concurrence | Invalidation du cache pendant validation | Snapshot cohérent ou échec sûr, pas de référence à un chunk obsolète |
| Fuite observabilité | Questions, prompts, citations sentinelles | Absence dans JSONL, événements, spans, attributs OTLP, exception messages |

## 2. Tests exigés avant acceptation d'une implémentation

**Unitaires** : parseur JSON strict (types, doublons, limites), arithmétique des offsets, digest SHA-256, unicité et correspondance documentaire, Unicode/CRLF, résultat `UNKNOWN` du vérificateur absent, verdicts C# déterministes, annulation, limitation de longueur. Chaque test inclut des variantes contraires ; le code source de la preuve est une fixture, pas le LLM.

**Intégration** : PostgreSQL **Testcontainers isolé**, import de document, reconstruction des chunks et invalidation cache ; vérifier la cohérence de la citation avant/après modification de contenu et les documents `ContentSha256=null` (sans modifier la DB utilisateur). Utiliser un faux Ollama HTTP pour les retours malformés, hostiles, contradictoires, incomplets, timeout et synthèse divergente.

**E2E** : route vérifiée + contrat JSON/compatibilité de `/api/rag/ask`, traces W3C et métriques nouvelles à faible cardinalité ; injection de sentinelles privées et absence de fuite dans `SafeTraceProcessor`, événements OTel et Serilog framework. Vérifier qu'un résultat cosinus >0,7 n'est pas promu seul en preuve. Préserver les tests unitaires, intégration et auto-test d'évaluation KH007 ; ne pas déclarer `KH007_REFERENCE_RESULT=pass` comme qualité RAG réelle.

**Campagne qualité ultérieure KH009** : séparer oracle synthétique et prédictions de l'API sur corpus isolé, fixer les hashes des modèles, topK, prompts, versions et seeds pertinents, répéter les essais, évaluer couverture, abstentions, justesse/erreurs contradictoires et ressources. Les 400 documents historiques et le dump de développement ne sont pas des fixtures destructibles.

## 3. Matrice structurée jointe

`evaluation/kh008-contract-v1/matrix.json` est un **oracle de politique déclaratif** (aucun exécutable ni résultat d'inférence) ; il énumère les états d'ancrage et de vérification sémantique injectés en test. Ses statuts attendus peuvent servir aux tests C# de KH008.1–.3. Les scénarios `ENTAILS` et `CONTRADICTS` simulent un vérificateur qualifié ; ils ne constituent pas une preuve que celui-ci existe ou qu'il raisonne correctement.

## 4. Chaîne de preuves pour le prochain lot de code

- SHA Git exact du point de départ et diff contrôlé par `git apply --check` sur copie de base.
- Compilations Release avec avertissements bloquants pour les projets concernés, tests unitaires et Testcontainers réels, restauration NuGet avec audit ; Docker précontrôlé avant modification.
- Exécution du vérificateur observabilité OTLP avec sentinelles et de l'oracle KH007 (deux suites distinctes).
- Rapport `run-*.zip` contrôlé et revue des sorties/rollback ; `RUN_RESULT=pass` ne vaut pas `MILESTONE_RESULT=pass`.
- Aucune écriture automatique GitHub et aucun commit local par le paquet ; branche et PR gérées par l'utilisateur.

## 5. Limites et condition de sortie KH008.0

Ce jalon **documente** des exigences ; il n'élimine pas encore la possibilité de réponses fausses sur l'API legacy. Sa validation est le contrôle de la cohérence avec le `main` distant, de l'intégrité des fichiers livrés et du comportement des tests existants sur la machine cible. Aucune assertion sur l'entailment ou sur la qualité réelle du RAG ne peut être acceptée à cette étape.
