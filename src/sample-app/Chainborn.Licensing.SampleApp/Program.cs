using Chainborn.Licensing.Validator;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddLicenseValidation(options =>
{
    options.PolicyDirectory = builder.Configuration.GetValue<string>("Licensing:PolicyDirectory") ?? "/etc/chainborn/policies";
    options.CacheDirectory = builder.Configuration.GetValue<string>("Licensing:CacheDirectory") ?? "/var/chainborn/cache";
});

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "healthy", licensing = "configured" }));

app.Run();
