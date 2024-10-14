var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.GaReactApp_Server>("gareactapp-server");

builder.Build().Run();
