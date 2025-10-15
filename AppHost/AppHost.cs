using Aspire.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.Web>("web").WithExternalHttpEndpoints().WithHttpHealthCheck("/health");

builder.Build().Run();
