using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace EI.Data.TestHelpers.Repository;

// [TestClass, TestCategory("Integration")]
public static class AssemblySetupHelpers
{
    public static (IConfiguration Configuration, TContext DbContext) Setup<TContext>(string connectionStringName)
        where TContext : DbContext
    {
        var cfg = new ConfigurationManager();

        var connectionString = cfg.GetConnectionString(connectionStringName) ??
                               cfg.GetValue<string>($"ConnectionStrings__{connectionStringName}") ??
                               Environment.GetEnvironmentVariable($"ConnectionStrings__{connectionStringName}") ??
                               $"Server=(localdb)\\EI;Initial Catalog={connectionStringName}Tests;";

        var inMemorySettings = new Dictionary<string, string>
                               {
                                   {
                                       $"ConnectionStrings:{connectionStringName}",
                                       connectionString
                                   }
                               };

        var configuration = new ConfigurationBuilder()
                            .AddInMemoryCollection(inMemorySettings!)
                            .Build();

        var ctor = typeof(TContext).GetConstructor([typeof(IConfiguration)])
                   ?? throw new InvalidOperationException($"No constructor found for context that takes {nameof(IConfiguration)}");

        var context = (ctor.Invoke([configuration]) as TContext)
            ?? throw new InvalidOperationException($"Could not construct new object of type {typeof(TContext).Name}");

        return (configuration, context);
    }

    // [AssemblyInitialize, TestCategory("Integration")]
    public static void AssemblyInit<TContext>(string connectionStringName)
        where TContext : DbContext
    {
        DbContext? dbContext = null;
        try
        {
            (_, dbContext) = Setup<TContext>(connectionStringName);
            dbContext.Database.Migrate();
        }
        finally
        {
            dbContext?.Dispose();
        }
    }

    // [AssemblyCleanup, TestCategory("Integration")]
    public static void AssemblyCleanup<TContext>(string connectionStringName)
        where TContext : DbContext
    {
        DbContext? dbContext = null;
        try
        {
            (_, dbContext) = Setup<TContext>(connectionStringName);
            dbContext.Database.EnsureDeleted();
        }
        catch (Exception)
        {
            // Ignore exceptions during cleanup
        }
        finally
        {
            dbContext?.Dispose();
        }
    }
}
