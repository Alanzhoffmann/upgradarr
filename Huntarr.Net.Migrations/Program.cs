using Huntarr.Net.Api;
using Microsoft.EntityFrameworkCore;

if (!Directory.Exists("/config"))
{
    Directory.CreateDirectory("/config");
}

var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
optionsBuilder.UseSqlite("Data Source=/config/app.db;Cache=Shared");

await using var dbContext = new AppDbContext(optionsBuilder.Options);
await dbContext.Database.MigrateAsync();

Console.WriteLine("Migrations applied successfully.");
