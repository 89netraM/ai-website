var builder = DistributedApplication.CreateBuilder(args);

builder
    .AddProject<Projects.AiWebsite_ApiService>("apiservice")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health");

builder.Build().Run();
