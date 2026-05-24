using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TaskManager.Identity.Domain.Entities;
using TaskManager.Identity.Domain.Interfaces;
using TaskManager.Identity.Infrastructure.Persistence;
using TaskManager.Identity.Infrastructure.Persistence.Repositories;
using TaskManager.Identity.Infrastructure.Services;

namespace TaskManager.Identity.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddIdentityInfrastructure(
        this IServiceCollection services,
        IConfiguration config)
    {
        // Database
        var connection = config["ConnectionStrings:IdentityDb"]
                         ?? config["IDENTITY_DB_CONNECTION"]
                         ?? throw new InvalidOperationException("IDENTITY_DB_CONNECTION is not configured");

        services.AddDbContext<IdentityDbContext>(opt =>
            opt.UseNpgsql(connection, npg => npg.MigrationsHistoryTable("__ef_migrations_history")));

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<IdentityDbContext>());

        // Identity
        services.AddIdentityCore<AppUser>(opt =>
            {
                opt.User.RequireUniqueEmail = true;
                opt.Password.RequireDigit = true;
                opt.Password.RequireUppercase = true;
                opt.Password.RequireLowercase = false;
                opt.Password.RequireNonAlphanumeric = false;
                opt.Password.RequiredLength = 8;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<IdentityDbContext>();

        // BCrypt cost 12, replaces default PBKDF2 hasher
        services.AddScoped<IPasswordHasher<AppUser>, BcryptPasswordHasher>();

        // JWT settings + token service
        services.Configure<JwtOptions>(opt =>
        {
            config.GetSection("Jwt").Bind(opt);
            var envSecret = config["JWT_SECRET"];
            if (!string.IsNullOrWhiteSpace(envSecret))
                opt.SecretKey = envSecret;
        });
        services.AddSingleton<ITokenService, TokenService>();

        // Repositories
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();

        return services;
    }
}
