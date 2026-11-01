# KH-008.3 — Politique C# de verdict documentaire

Base Git : `2d388faf46cdc8244bb8595631bba6f4e31b11d2` (PR #14).

## Frontière de confiance

1. Le LLM de génération ne peut proposer que des assertions et citations non fiables.
2. `EvidenceAnchorVerifier` garantit seulement une ancre textuelle dans la liste de chunks autorisés.
3. `IQualifiedEvidenceAssessor` est un **point d'intégration pour un vérificateur indépendant**. KH008.3 ne fournit **aucune implémentation qualifiée** ; le résultat par défaut reste `UNKNOWN`, donc `ABSTAIN`.
4. Le serveur doit fournir une identité de politique connue à l'avance (`trustedPolicyId`) ; un `PolicyId` inattendu, une liste de jugements incomplète, dupliquée ou étrangère au lot est rejetée.
5. La vérification de couverture est liée au même évaluateur indépendant et doit porter sur les points demandés par la question. `UNKNOWN` bloque toute publication factuelle.
6. `VerifiedVerdictService` demande une nouvelle revalidation PostgreSQL **après** l'évaluation sémantique, avant le verdict. Ceci réduit le risque de concurrence sans l'éliminer : une modification après la revalidation est toujours possible. Un contrat de publication atomique relèvera du jalon suivant.
7. Une citation exacte ne vaut jamais `SUPPORTED` sans décision `ENTAILS` qualifiée pour **chacune** des preuves de la proposition. Une contradiction et une implication simultanées forment `CONFLICT`; une preuve inconnue empêche `SUPPORTED`.

## Règles de décision

| Situation | Verdict |
|---|---|
| Requête invalide, document périmé, ancre fabriquée | `ABSTAIN` |
| Vérificateur manquant, non qualifié ou mauvais identifiant de politique | `ABSTAIN` |
| Couverture inconnue | `ABSTAIN` |
| Tous les points approuvés, couverture complète | `ANSWER` |
| Au moins un point approuvé, sans couverture intégrale | `PARTIAL` |
| Conflit qualifié ou preuves simultanément impliquantes et contradictoires | `CONFLICT` |
| Aucune assertion approuvée | `ABSTAIN` |

Le texte final est assemblé par `VerifiedVerdictPolicy` depuis les seules assertions `SUPPORTED` et leurs extraits originaux contrôlés. Il n'existe pas de phase de reformulation libre par Ollama dans ce jalon. Les assertions non approuvées n'apparaissent jamais dans ce texte.

## Limites

- L'interface d'évaluation indépendante ne prouve pas la justesse de sa future implémentation ; la qualité réelle d'entailment devra être mesurée sur corpus distinct.
- Un faux évaluateur est utilisé uniquement dans les tests pour simuler `ENTAILS`, `CONTRADICTS` et couverture ; ces résultats ne prouvent pas la vérité documentaire.
- Aucun nouvel endpoint, aucune DI de production, aucune migration EF, aucune écriture dans la base de développement.
- Aucun log de question, assertion, citation, document, modèle ou extrait.
- Le chemin HTTP hérité `/api/rag/ask` reste inchangé, non qualifié et ne doit pas être annoncé comme vérifié.
