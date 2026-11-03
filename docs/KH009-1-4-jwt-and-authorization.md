# KH-009.1.4 — JWT Bearer et autorisation Workspaces

Statut : **implémentation préparée / tests Windows à effectuer**. Aucun endpoint Workspaces n'est encore exposé en production.

## Périmètre

- Schéma JWT Bearer .NET 10 avec signature validée via JWKS OIDC HTTPS, `iss`, `aud`, `exp`, `nbf`, type de jeton `at+jwt`, clé signée, algorithme asymétrique et scope API requis.
- `MapInboundClaims=false` pour conserver `iss`/`sub`, puis validation par le résolveur de KH-009.1.1.
- `IWorkspaceAccessStore` : seule source fiable des rôles et de l'appartenance. Un adaptateur **DenyAll** est enregistré jusqu'à KH-009.1.5 ; ne pas lire les rôles JWT pour accorder un droit.
- Neuf capacités distinctes sur des politiques explicites. L'accès exige `workspaceId` dans la route, adhésion ACTIVE et espace non archivé (sauf Restore réservé à OWNER).
- 401 sur authentification invalide (challenge Bearer), 404 sur absence d'adhésion ou espace archivé, 403 sur rôle insuffisant. Erreurs sans contenu privé.
- Les anciennes routes `/api/*` ne reçoivent pas d'attribut `Authorize` et restent compatibles dans ce lot. Elles devront être limitées avant d'héberger du contenu tenantisé (KH-009.3).

## Configuration (à définir hors Git, uniquement après choix du fournisseur)

```json
{
  "Authentication": {
    "TrustedIssuers": ["https://issuer.example.org/tenant"],
    "Jwt": {
      "Authority": "https://issuer.example.org/tenant",
      "Issuer": "https://issuer.example.org/tenant",
      "Audience": "knowledgehub.api",
      "RequiredScope": "knowledgehub.read"
    }
  }
}
```

Les valeurs ci-dessus sont des **placeholders**. Aucun vrai fournisseur n'est configuré par le paquet. La chaîne exacte `Issuer` doit correspondre à l'issuer du document discovery OIDC, sans normalisation d'URL. Ce premier profil accepte exclusivement `at+jwt` / `application/at+jwt` et RS256, PS256, ES256. Les fournisseurs qui utilisent `typ: JWT` devront faire l'objet d'une décision explicite et de tests supplémentaires avant adaptation ; ne jamais accepter un ID token par commodité.

Sans configuration, le schéma Bearer ne peut valider aucun jeton. Une configuration partielle échoue au démarrage. La valeur de `TrustedIssuers` doit être un tableau contenant exactement cet issuer.

## Validation automatisée

- Tests unitaires : matrice des rôles, absence de principal, identifiant de route falsifié, révocation/suspension, archive, configurations incomplètes.
- Tests HTTP : jetons RS256 réellement signés dans le processus de test (clé générée pour l'essai, jamais exposée au serveur en production) ; rejets de signature, issuer, audience, durée, scope, type de token ; 401/403/404/200 et compatibilité de `/api/diagnostics/database`.
- `Kh009SecurityProbeController` appartient uniquement à l'assembly **Tests.Integration** et est ajouté comme MVC ApplicationPart dans le host de test ; il ne doit jamais être activé sur l'API de production.
- Le client MAUI ne doit pas encore être basculé sur ces routes : aucune source de jeton réelle n'est disponible.

## Limites à lever ensuite

1. KH-009.1.5 : migration EF additive, puis implémentation DB de `IWorkspaceAccessStore` avec résolution `AppUser` par `(issuer, subject)`, appartenance ACTIVE et archive ; jusqu'alors autorisation tenantisée fermée.
2. KH-009.2 : création/gestion des espaces, transactions pour dernier OWNER, gestion des permissions fines et des invitations.
3. KH-009.3/4 : verrouillage des anciennes routes globales et isolation documentaire, RAG et cache avant exposition de données privées.
4. Qualifier un fournisseur OIDC réel, la politique de révocation, rotation des clés et récupération des jetons dans MAUI Windows.

Aucune migration, nouvelle table, commit, push, changement MAUI ou secret JWT n'est inclus dans ce lot.
