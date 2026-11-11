using Microsoft.EntityFrameworkCore;

namespace PayLite.WalletApi;

public static class AdminSeeder
{
    public static async Task SeedAdminAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<WalletDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

        var adminAlreadyExists = await context.Users.AnyAsync(u => u.Role == Roles.Admin);
        if (adminAlreadyExists)
        {
            logger.LogInformation("An Admin user already exists. Skipping admin seeding.");
            return;
        }

        var seedUsername = configuration["Seed:AdminUsername"];
        var seedPassword = configuration["Seed:AdminPassword"];

        if (string.IsNullOrWhiteSpace(seedUsername) || string.IsNullOrWhiteSpace(seedPassword))
        {
            logger.LogWarning(
                "No Admin user exists and Seed:AdminUsername/Seed:AdminPassword are not configured. Skipping admin seeding.");
            return;
        }


        var usernameTaken = await context.Users
            .AnyAsync(u => u.Username == seedUsername); //replaced with collation

        if (usernameTaken)
        {
            logger.LogWarning(
                "No Admin user exists, but username {Username} is already taken by a non-admin account. Skipping admin seeding.",
                seedUsername);
            return;
        }

        context.Users.Add(new User
        {
            Username = seedUsername,
            PasswordHash = passwordHasher.Hash(seedPassword),
            Role = Roles.Admin
        });
        logger.LogInformation("Seeded initial Admin user {Username} at startup.", seedUsername);

        await context.SaveChangesAsync();
    }
}