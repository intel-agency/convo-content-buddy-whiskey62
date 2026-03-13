using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Npgsql;
using Pgvector;
using Pgvector.EntityFrameworkCore;

namespace ConvoContentBuddy.Data;

/// <summary>
/// Design-time factory used by the EF Core tooling (<c>dotnet ef migrations</c>) to create
/// an <see cref="AppDbContext"/> without a running application host.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    /// <inheritdoc/>
    public AppDbContext CreateDbContext(string[] args)
    {
        const string connectionString =
            "Host=localhost;Port=5432;Database=convocontentbuddy;Username=postgres;Password=postgres";

        var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
        dataSourceBuilder.UseVector();
        var dataSource = dataSourceBuilder.Build();

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseNpgsql(dataSource, o => o.UseVector());

        return new AppDbContext(optionsBuilder.Options);
    }
}
