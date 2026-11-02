using KnowledgeHub.Application.Authorization;
using KnowledgeHub.Application.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.IdentityModel.Tokens;

namespace KnowledgeHub.Api.Security;

public static class Kh009AuthenticationExtensions
{
    public static IServiceCollection AddKh009Security(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        // WebApplicationFactory can supply configuration after Program registers
        // services. Resolve IConfiguration when the named Bearer options are built,
        // never capture a potentially empty snapshot at registration time.
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IConfiguration>((options, configuration) =>
            {
                var settings = Kh009JwtSettings.Read(configuration);
                options.MapInboundClaims = false;
                options.SaveToken = false;
                options.IncludeErrorDetails = false;
                options.RequireHttpsMetadata = true;
                if (settings is not null)
                    options.Authority = settings.Authority;

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = settings?.Issuer,
                    ValidateAudience = true,
                    ValidAudience = settings?.Audience,
                    ValidateLifetime = true,
                    RequireExpirationTime = true,
                    RequireSignedTokens = true,
                    ValidateIssuerSigningKey = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    ValidTypes = ["at+jwt", "application/at+jwt"],
                    ValidAlgorithms = [
                        SecurityAlgorithms.RsaSha256,
                        SecurityAlgorithms.RsaSsaPssSha256,
                        SecurityAlgorithms.EcdsaSha256
                    ]
                };

                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = context =>
                    {
                        var resolver = context.HttpContext.RequestServices
                            .GetRequiredService<IAuthenticatedUserIdentityResolver>();
                        var hasApiScope = settings is not null &&
                            context.Principal is not null &&
                            context.Principal.Claims
                                .Where(claim => claim.Type is "scope" or "scp")
                                .SelectMany(claim => claim.Value.Split(
                                    ' ', StringSplitOptions.RemoveEmptyEntries))
                                .Contains(settings.RequiredScope, StringComparer.Ordinal);
                        if (!hasApiScope || resolver.Resolve(context.Principal) is null)
                            context.Fail("Invalid API access token.");
                        return Task.CompletedTask;
                    }
                };
            });

        services.AddAuthorization(WorkspaceAuthorizationPolicies.AddPolicies);
        services.AddScoped<IWorkspaceAccessStore, DenyAllWorkspaceAccessStore>();
        services.AddScoped<IAuthorizationHandler, WorkspaceAuthorizationHandler>();
        services.AddSingleton<IAuthorizationMiddlewareResultHandler, WorkspaceAuthorizationResultHandler>();
        return services;
    }
}
