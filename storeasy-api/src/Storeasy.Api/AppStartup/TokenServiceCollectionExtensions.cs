using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Storeasy.Api.Utils;
using Storeasy.Application.Utils.Constants;

namespace Storeasy.Api.AppStartup;

public static partial class StartupExtensions
{
    /// <summary>
    /// Adds the storeasy token authentication (personal access, CI and upload tokens) next to the JWT bearer of the web
    /// sessions: a policy scheme becomes the default and sends a <c>Bearer</c> value with a storeasy token prefix to
    /// <see cref="StoreasyTokenAuthenticationHandler"/>, anything else to JWT bearer. Call after <c>AddAuthorization</c>.
    /// </summary>
    /// <param name="services"></param>
    /// <returns></returns>
    public static IServiceCollection AddStoreasyTokenAuthentication(this IServiceCollection services)
    {
        services.AddAuthentication()
                .AddScheme<AuthenticationSchemeOptions, StoreasyTokenAuthenticationHandler>(TokenClaimNames.AuthenticationScheme, null)
                .AddPolicyScheme(StoreasyTokenAuthenticationHandler.SelectorScheme, null, options =>
                {
                    options.ForwardDefaultSelector = context => StoreasyTokenAuthenticationHandler.HasStoreasyToken(context)
                        ? TokenClaimNames.AuthenticationScheme
                        : JwtBearerDefaults.AuthenticationScheme;
                });

        services.Configure<AuthenticationOptions>(options =>
        {
            options.DefaultScheme = StoreasyTokenAuthenticationHandler.SelectorScheme;
            options.DefaultAuthenticateScheme = StoreasyTokenAuthenticationHandler.SelectorScheme;
            options.DefaultChallengeScheme = StoreasyTokenAuthenticationHandler.SelectorScheme;
        });

        return services;
    }
}
