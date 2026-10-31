# KH-007 — Corpus de reference et evaluation reproductible

## Périmètre

Ce jalon fournit un **référentiel de qualification synthétique et versionné**, et un
outil C# autonome sans appels réseau ni écriture sur la base de développement.
L'objectif est de rendre mesurables la sélection documentaire, l'abstention et
les **citations textuelles exactes** avant KH-008 (verdict déterministe dans le RAG)
et KH-009 (campagnes de comparaison réelles).

L'ancienne infrastructure spécifique aux datasets et FiQA **n'est pas réintroduite**.
Le référentiel est conservé dans `evaluation/kh007-reference-v1/`, hors
`KnowledgeHub.Application` et hors `KnowledgeHub.Infrastructure`.

## Données

- `corpus.json` : dix documents artificiels, format de `CorpusRoot` accepté par
  `KnowledgeHub.CorpusImporter`, GUID externes stables. Aucune donnée métier privée.
- `cases.json` : seize questions, treize documentaires et trois abstentions,
  plusieurs questions multisources, pertinence binaire et extraits attendus.
- `oracle-predictions.json` : résultats de référence **fabriqués directement
  depuis le gold** ; jamais interprétés comme une performance RAG mesurée.
- `manifest.json` : SHA-256 vérifiés des trois fichiers ; toute modification
  accidentelle du référentiel interrompt la validation.

Pour une évaluation sur une API réelle, importer explicitement `corpus.json`
**seulement dans un environnement de test isolé**, puis convertir les résultats
de recherche et les justificatifs dans le format `predictions` ci-dessous.
L'identifiant à comparer est **le GUID externe du corpus**, pas la clé primaire
interne de PostgreSQL : une association contrôlée est nécessaire.
Le paquet ne réalise pas cet import, ne démarre pas de modèle et ne produit
aucune prédiction à partir d'un LLM.

## Exécution hors ligne

Depuis la racine du dépôt, sur une machine avec .NET 10 :

```sh
dotnet run --project KnowledgeHub.Evaluation/KnowledgeHub.Evaluation.csproj \
  -c Release -- --self-test \
  --baseline evaluation/kh007-reference-v1 --report reports/kh007-self-test
```

Pour mesurer des résultats fournis par l'extérieur :

```sh
dotnet run --project KnowledgeHub.Evaluation/KnowledgeHub.Evaluation.csproj \
  -c Release -- --evaluate \
  --baseline evaluation/kh007-reference-v1 \
  --report reports/kh007-evaluation \
  --predictions /chemin/vers/predictions.json
```

Chaque prédiction suit ce schéma (`case_id` ici nommé `id`) :

```json
{
  "schema_version": 1,
  "corpus_id": "kh007-synthetic-v1",
  "predictions": [
    {
      "id": "KH007-Q01",
      "verdict": "answer",
      "ranked_document_ids": ["00000000-0000-0000-0000-000000000101"],
      "evidence": [
        {
          "document_id": "00000000-0000-0000-0000-000000000101",
          "quote": "CancellationToken permet de demander une annulation cooperative."
        }
      ]
    }
  ]
}
```

Le fichier complet doit comporter une prédiction **par cas**, sans doublon,
et des identifiants externes présents dans le corpus. Un verdict `abstain`
ne peut fournir aucune preuve. Les autres cas figurent dans le fichier oracle,
qui sert d'exemple de structure uniquement.

## Métriques et interprétation

- Precision@5 : nombres de documents pertinents parmi les cinq positions
  possibles, **dénominateur fixe égal à 5**. Si un système rend un seul résultat
  pertinent, Precision@5 vaut 0,2, pas 1.
- Recall@5 : part des documents pertinents du cas présents dans les cinq premiers.
- MRR@5 : inverse du rang du premier document pertinent.
- nDCG@5 : qualité des rangs, avec pertinence binaire.
- Abstention accuracy : fraction des cas sans réponse où le système s'abstient.
- Verdict accuracy : accord `answer` / `abstain` sur l'ensemble des cas.
- Evidence precision et recall : correspondance exacte des paires `(GUID, extrait)`
  attendues dans les cas de référence.
- Grounded verdict rate : bonne décision, présence de tous les documents
  pertinents dans le top 5 et correspondance de toutes les citations attendues.

Les mesures de récupération sont moyennées **sur les cas documentaires seulement**.
Les citations sont comparées textuellement, sans classification sémantique.
Cela **ne prouve pas** l'exactitude des affirmations d'une réponse générée :
le verdict déterministe sur chaque proposition appartient au jalon KH-008.

## Garde-fous

Le contrôle `--self-test` vérifie les empreintes des données, les références,
la présence des extraits dans les sources, la perfection de l'oracle et les
régressions voulues (documents manquants, citation invalide, fausse réponse,
doublon, document inconnu et prédiction manquante).

Les rapports `kh007-reference-evaluation.json` et `.md` contiennent uniquement
les IDs de cas, verdicts, compteurs, agrégats et mesures. Aucun corps de document,
question, réponse ou citation n'est écrit dans ces deux rapports.

Les 189 tests préexistants, les compilations Release et le contrôle E2E
observabilité sont conservés dans le paquet. Les validations réelles exigent
l'exécution sur la machine .NET de destination ; ce document ne présente aucun
score de modèle non mesuré.
