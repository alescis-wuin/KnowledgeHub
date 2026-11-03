using System.Security.Claims;
using KnowledgeHub.Domain.ValueObjects;

namespace KnowledgeHub.Application.Identity;

/// <summary>
/// Reads identity ONLY from a ClaimsPrincipal created by a successfully
/// validated access-token authentication handler. Does not validate JWTs.
/// </summary>
public interface IAuthenticatedUserIdentityResolver
{
    OidcIdentityKey? Resolve(ClaimsPrincipal? principal);
}
