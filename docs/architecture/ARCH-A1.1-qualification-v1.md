# ARCH-A0.1 / ARCH-A1.1 — Prévol et qualification locale (candidat)

## Référence de départ

- Dépôt : `alescis-wuin/KnowledgeHub`, `main@77559ae509024638407aec58e1883e135af5ba7a`.
- Arbre Git : `781ef402c11287bda86a47f33c1b01bccd517d7c` ; PR #19 fusionnée le 2026-11-03.
- Jalons : `ARCH-A0.1` inventaire, `ARCH-A0.2` contrats, `ARCH-A1.1` dispositif de validation, ensuite `ARCH-A1.2` puis `ARCH-G1`.
- Le gel historique `1.0.0-rc.1` est approuvé. La révision `1.1.0-rc.1` est **candidate à une approbation explicite**, même si les fichiers ont été fusionnés. Les projets SDK/Verification ne sont pas implémentés par ce paquet.

## Périmètre de ce lot

Le script `scripts/architecture/kh_a1_1_validate.py` fournit un **prévol non destructif** de la copie Git et un parcours de tests local volontairement déclenché. Il refuse un commit, un arbre, une branche ou un delta non attendus. Il ne change ni les branches existantes ni la base de données personnelle.

### Prévol A0.1

- Vérifie HEAD et tree du squash PR #19, l'inventaire des fichiers et le périmètre exact des trois nouveaux fichiers.
- Recense les worktrees Git présents sur **cet hôte Linux** en lecture seule ; ne modifie ni ne nettoie leurs fichiers.
- Les dépôts et worktrees **MAUI Windows, Avalonia PoC Windows et KH0012-Lab** restent `not_observed` tant que leurs propriétaires n'ont pas fourni de preuves. Cela empêche la clôture globale A0.1 sur la seule base de ce prévol.
- Aucun artefact privé ni contenu des fichiers ne figure dans le rapport ZIP partagé.

### Validation A1.1 (option volontaire `--mode full`)

| Groupe | Validation effective |
| --- | --- |
| SDK | `dotnet --version`, band `10.0.4xx` à partir de `10.0.403` selon `global.json` |
| Solution | `dotnet restore`, build Release avec avertissements bloquants des projets backend **hors MAUI** |
| Hors solution | Restore/build séparés d'`Evaluation` et `Observability.Verify` : **neuf projets .NET Linux couverts**, MAUI explicitement réservée à Windows |
| Qualité | `dotnet format whitespace` sur chaque projet backend Linux (`--verify-no-changes --no-restore`) |
| Unitaires | `dotnet test` Release + TRX, tous les résultats doivent être `Passed`, sans nombre figé |
| Intégration | PostgreSQL jetable via Testcontainers, sur **opt-in** `--allow-container-tests`, TRX obligatoire |
| Évaluation | CLI réel `--self-test --baseline <répertoire> --report <répertoire>` : oracle synthétique, pas une mesure de RAG réel |
| Observabilité | CLI réel avec **unique chemin de rapport absolu**, vérification E2E OTLP sur hôte compatible, opt-in conteneurs |
| Intégrité | Vérification de HEAD/tree et du delta attendu après les tests ; rapports et logs hors du dépôt |

**Les tests ne sont pas exécutés par le constructeur documentaire v3**. Leur statut effectif n'est connu qu'après le parcours `--mode full` chez l'opérateur. Un refus, une indisponibilité Docker/NuGet/SDK, un TRX manquant ou un test ignoré bloque A1.1, sans faux `PASS`. Les logs/TRX bruts restent dans le dossier privé créé par le script ; le ZIP de retour contient seulement un résumé expurgé.

## Utilisation depuis le worktree de qualification

La branche de travail doit être `feature/arch-a1-1-validation` sur le commit de référence. Les trois fichiers doivent être installés par le paquet v3 et **ne doivent pas encore être commités** ; seul ce delta exact est autorisé.

Prévol :

```bash
python3 -I scripts/architecture/kh_a1_1_validate.py \
  --root /developpement/seynax/KnowledgeHub-ARCH-A1.1-Validation \
  --output-parent "$HOME/Téléchargements/KnowledgeHub-ARCH-A1.1-reports" \
  --mode preflight
```

Qualification volontaire, avec Testcontainers :

```bash
python3 -I scripts/architecture/kh_a1_1_validate.py \
  --root /developpement/seynax/KnowledgeHub-ARCH-A1.1-Validation \
  --output-parent "$HOME/Téléchargements/KnowledgeHub-ARCH-A1.1-reports" \
  --mode full --allow-container-tests
```

La commande affiche `RETURN_REPORT` vers un ZIP indépendant et un `RETURN_REPORT_SHA256`. En cas d'échec partiel, conserver le dossier privé pour l'analyse. **Ne pas relancer un paquet v3 consommé** ; le script de qualification, lui, peut être exécuté à nouveau avec un nouvel ID de rapport, après diagnostic.

## Conditions restant à clore

- **A0.1 :** inventaire/protection réels des worktrees Windows, Avalonia et laboratoire privé, sans récupération ou diffusion de leurs fichiers.
- **A0.2 :** approbation explicite de `1.1.0-rc.1`, distincte de la fusion Git ; garder `prepared_base_sha=null` tant que G1 n'est pas accepté.
- **A1.1 :** résultats d'exécution du dispositif sur SDK/Docker qualifiés ; examen des tests, revue COR-001/COR-002, contrôle EF supplémentaire si pertinent ; l'absence de résultat n'est pas un succès.
- **A1.2/G1 :** aucune nouvelle assembly, migration, DI ou modification des interfaces n'est incluse ; le gate G1 reste fermé.

## Limitations et protection

Le fichier `KnowledgeHub.slnx` contient le projet **MAUI Android** : il est volontairement exclu des restaurations/builds Linux pour ne pas imposer les workloads Android. Les commandes `dotnet restore/build/test` sont opt-in et peuvent créer des répertoires ignorés `bin/obj`, accéder aux dépôts NuGet et créer des **conteneurs temporaires** via Testcontainers ; le script ne suppose pas une absence d'effets sur ces services. Il n'appelle ni `git reset`, ni `git clean`, ni migrations de données, ni `docker compose down`, ni déploiement, ni commit/push. Les mesures de temps et de tests sont rapportées uniquement si réellement observées ; les tests d'intégration n'utilisent pas une base personnelle par conception, mais leur configuration réelle doit encore être examinée.
