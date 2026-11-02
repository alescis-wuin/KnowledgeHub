# KH-009.1.1 - Identite applicative OIDC (sans authentification HTTP)

**Base locale attendue :** branche `feature/kh-009-workspaces-backend`, commit KH-009.0 `docs(kh009): establish workspace API and security contract`, parent Git `53f032f2861ad823f78338b5ac0058e275afe059`.

## Livrable de ce sous-jalon

- `OidcIdentityKey` : couple `(Issuer, Subject)` exact et sensible a la casse. Un changement de chemin, de casse ou de slash final change la cle. Aucune equivalence par `email`, `name`, `role` ou `ClaimTypes.NameIdentifier`.
- `AppUser` : `Guid Id`, `Issuer`, `Subject`, `DisplayName`, `EmailNormalized`, `EmailVerified`, `CreatedAtUtc`. Profil actualisable sans mutation de l'identite OIDC. Le champ e-mail reste une **metadonnee**, jamais une cle de connexion.
- `IAuthenticatedUserIdentityResolver` et `OidcPrincipalIdentityResolver` : resolution defensive des seules claims brutes `iss` et `sub` d'un principal authentifie avec un seul jeu de claims. Liste d'issuers de confiance a correspondance exacte ; aucune valeur = aucun utilisateur resolu.
- Enregistrement DI dans `AddApplicationServices` : liste de configuration `Authentication:TrustedIssuers`. Pas de fournisseur externe requis pour compiler ou tester.
- Tests xUnit de cas positifs et hostiles, sans serveur OIDC ni base utilisateur.

## Frontiere de securite **non franchie** par ce lot

Un `ClaimsPrincipal` n'est pas un jeton et `IsAuthenticated` ne prouve pas que la signature JWT a ete validee. Le resolver ne doit jamais etre appele sur des claims construites a partir d'en-tetes HTTP, de parametres ou de formulaires. Les tests unitaires fabriquent volontairement des principals pour tester les regles de resolution, **pas** le processus d'authentification.

**KH-009.1.4 DOIT :** installer un handler JWT Bearer qui valide signature et cle de confiance, `iss`, `aud`, `exp`, `nbf` et type de jeton access-token ; activer `MapInboundClaims=false` afin de conserver `iss` / `sub`. Appliquer `UseAuthentication` avant `UseAuthorization`, une politique par defaut exigeant un utilisateur authentifie sur l'API v1, et renvoyer 401/403 selon le contrat. L'issuer valide par le handler doit correspondre a la liste de confiance du resolver. Aucune cle privee, jeton, mot de passe ou secret dans Git.

## Format de configuration prevu (non ajoute dans les appsettings versionnes)

```json
{
  "Authentication": {
    "TrustedIssuers": [
      "https://idp.example.test/realms/knowledgehub"
    ]
  }
}
```

Cet exemple est **fictif**. Tant qu'aucun fournisseur OIDC ni schéma Bearer n'est configure, les anciens endpoints HTTP conservent leur comportement et aucune authentification Workspaces n'est activee. `http://localhost` peut etre accepte uniquement s'il figure **explicitement** dans la liste de confiance, pour un fournisseur local de developpement. En production, utiliser des issuers HTTPS.

## Hors perimetre de KH-009.1.1

- Aucune entite EF Core mappee ni migration ; l'index unique `(Issuer, Subject)` sera impose dans KH-009.1.3 / 009.1.5.
- Aucune creation ou synchronisation automatique de `AppUser` depuis un principal. Ce sera une operation serveur qui devra verifier la correspondance `(Issuer, Subject)` et sa contrainte unique sous concurrence.
- Aucun controle d'appartenance a un Workspace ni route v1 ; aucun endpoint n'est declare securise par ce lot.
- Aucun `UseAuthentication`, `AddJwtBearer`, code MSAL ou `WebAuthenticator` dans MAUI.
- Aucune extraction de metadonnees de profil depuis des claims non verifiees et aucun privilege depuis un `role` fourni par le client.

## Definition of Done

1. Nouveaux fichiers et unique enregistrement DI sur la branche backend ; MAUI intact.
2. Validation SHA-256 du payload et Git preflight; aucune reecriture de fichiers divergents.
3. Compilation Release stricte (warnings as errors), tests unitaires existants et KH-009.1.1, tests PostgreSQL Testcontainers isoles.
4. Aucun commit, push, migration, API executee ou modification PostgreSQL par le script.
5. Le resultat n'est pas une preuve de securite OIDC runtime : tests HTTP 401/403/404 demeurent a construire aux jalons suivants.

References :
- https://openid.net/specs/openid-connect-core-1_0.html
- https://learn.microsoft.com/aspnet/core/security/authentication/configure-jwt-bearer-authentication?view=aspnetcore-10.0
