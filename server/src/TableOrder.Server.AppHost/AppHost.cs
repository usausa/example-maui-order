var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.TableOrder_Server_Web>("server")
    .WithHttpHealthCheck("/health");

builder.Build().Run();
