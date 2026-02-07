// License Issuer Service — manages license issuance on the Midnight blockchain.
// This component will expose an API for creating licenses and submitting
// transactions to the Midnight network.
//
// See: docs/architecture.md for the bridge point design.

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/", () => "Chainborn License Issuer — not yet implemented");

app.Run();
