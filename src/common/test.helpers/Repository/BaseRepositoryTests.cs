using System.Reflection;
using EI.API.Service.Data.Helpers.Model;
using EI.API.Service.Data.Helpers.Platform;
using EI.API.Service.Data.Helpers.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;

namespace EI.Data.TestHelpers.Repository;

public abstract class BaseRepositoryTests<TContext, TRepo, TEntity>
    where TContext : DbContext
    where TEntity : class, IDatabaseEntity
    where TRepo : class, IReadRepository<TEntity>
{
    protected IConfiguration _configuration = default!;
    protected TContext _context = default!;
    protected TRepo _repository = default!;

    protected abstract string ConnectionStringName { get; }

    [TestInitialize]
    public void Setup()
    {
        (_configuration, _context) = AssemblySetupHelpers.Setup<TContext>(ConnectionStringName);

        AdditionalTestSetup(_context);

        _repository = BuildRepo(_context);
    }

    [TestCleanup]
    public virtual async Task Cleanup()
    {
        await _context.DisposeAsync();

        await _repository.DisposeAsync();
    }

    protected virtual TRepo BuildRepo(TContext context)
    {
        // special rules for platform - all others need the ClientId
        var requiresClientId = !typeof(TContext).FullName!.Contains(".Platform.Core.");

        var ctor = requiresClientId
                       ? typeof(TRepo).GetConstructor([typeof(IDatabaseClientFactory), typeof(Guid)])
                       : typeof(TRepo).GetConstructor([typeof(IDatabaseClientFactory)]);

        Assert.IsNotNull(ctor, "Could not find standard repository constructor");

        var mockFactory = new Mock<IDatabaseClientFactory>();
        mockFactory.Setup(f => f.GetDbContext<TContext>(It.IsAny<Guid>(), It.IsAny<string>())).ReturnsAsync(context);
        var factory = mockFactory.Object;

        var repo = ctor.Invoke(
                               requiresClientId
                                   ? [factory, Guid.NewGuid()]
                                   : [factory]
                              ) as TRepo;

        Assert.IsNotNull(repo, $"Could not construct type {typeof(TRepo).Name}");

        return repo;
    }

    protected abstract TEntity BuildModel(string label);

    protected virtual void AdditionalTestSetup(TContext context)
    {
        // Override if needed
        context.SaveChanges();
    }

    protected virtual DbSet<TEntity> DbSet => _context.Set<TEntity>();

    protected virtual TContext MakeContext()
        => AssemblySetupHelpers.Setup<TContext>(ConnectionStringName).DbContext;
}