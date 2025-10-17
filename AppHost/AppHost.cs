using System;
using Aspire.Hosting;
using Microsoft.Extensions.Configuration;

var builder = DistributedApplication.CreateBuilder(args);

var sqliteConfig =
    builder.Configuration.GetSection("Sqlite").Get<SqliteConfig>() ?? throw new ArgumentNullException("Sqlite");
var sqlite = builder.AddSqlite("sqlite", sqliteConfig.DirectoryPath, sqliteConfig.FileName);

builder.AddProject<Projects.Web>("web").WithExternalHttpEndpoints().WithReference(sqlite);

builder.Build().Run();

record SqliteConfig(string DirectoryPath, string FileName);
