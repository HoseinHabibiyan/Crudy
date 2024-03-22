using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;

namespace MockApi.Identity;

public static class JwtConfiguration
{
    public static IServiceCollection AddJwtBearerAuthentication(this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddAuthorization(setup =>
            {
                var policyBuilder = new AuthorizationPolicyBuilder(JwtBearerDefaults.AuthenticationScheme);
                policyBuilder = policyBuilder.RequireAuthenticatedUser();
                setup.DefaultPolicy = policyBuilder.Build();
            })
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                var jwtSecret = configuration["Jwt:ClientSecret"];

                options.SaveToken = true;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = false,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    RequireExpirationTime = false,
                    ValidateLifetime = false,
                    ClockSkew = TimeSpan.FromMinutes(5)
                };
            });

        return services;
    }
}