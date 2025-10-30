using Microsoft.EntityFrameworkCore;
using HackerNewsTopApi.Domain;

namespace HackerNewsTopApi.Infrastructure.Data
{
    public static class DatabaseInitializer
    {
        public static async Task InitializeAsync(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<AppDbContext>>();

            try
            {
                logger.LogInformation("DB migration starting...");
                
                await context.Database.MigrateAsync();
                
                logger.LogInformation("DB migration finished with success.");

                await SeedDataAsync(context, logger);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Migration error.");
                throw;
            }
        }

        private static async Task SeedDataAsync(AppDbContext context, ILogger logger)
        {
            if (await context.Users.AnyAsync())
            {
                logger.LogInformation("Database already have this data saved. Seed ignored.");
                return;
            }

            logger.LogInformation("Inserting initial data...");

            var defaultUser = new User
            {
                Username = "admin",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin")
            };
            context.Users.Add(defaultUser);
            await context.SaveChangesAsync();

            logger.LogInformation("Initial data inserted.");
        }
    }
}