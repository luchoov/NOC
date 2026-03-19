// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace NOC.Shared.Infrastructure.Data;

public class NocDbContextFactory : IDesignTimeDbContextFactory<NocDbContext>
{
    public NocDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres")
            ?? "Host=localhost;Database=noc;Username=noc_user;Password=changeme_pg_password";

        var optionsBuilder = new DbContextOptionsBuilder<NocDbContext>();
        optionsBuilder.UseNpgsql(connectionString, npgsql =>
        {
            npgsql.MigrationsAssembly("NOC.Web");
        });

        return new NocDbContext(optionsBuilder.Options);
    }
}
