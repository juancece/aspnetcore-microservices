using Common.Auth;
using Common.Observability;
using Discount.Grpc.Extensions;
using Discount.Grpc.Repositories;
using Discount.Grpc.Services;

var builder = WebApplication.CreateBuilder(args);

// Add Serilog for structured logging
builder.AddSerilog();

// Additional configuration is required to successfully run gRPC on macOS.
// For instructions on how to configure Kestrel and gRPC clients on macOS, visit https://go.microsoft.com/fwlink/?linkid=2099682

// Add services to the container.
builder.Services.AddScoped<IDiscountRepository, DiscountRepository>();
builder.Services.AddAutoMapper(typeof(Program));

// Authentication & Authorization for gRPC
builder.Services.AddJwtAuthentication(builder.Configuration);
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(PolicyConstants.ReadDiscount, policy =>
        policy.RequireAuthenticatedUser()
              .RequireClaim("scope", PolicyConstants.Scopes.DiscountRead));

    options.AddPolicy(PolicyConstants.WriteDiscount, policy =>
        policy.RequireAuthenticatedUser()
              .RequireClaim("scope", PolicyConstants.Scopes.DiscountWrite)
              .RequireRole(PolicyConstants.Roles.Admin, PolicyConstants.Roles.User));
});

// Observability - OpenTelemetry distributed tracing
builder.Services.AddObservability(builder.Configuration);

builder.Services.AddGrpc();

var app = builder.Build();

// Configure the HTTP request pipeline.

// Serilog request logging (before other middleware)
app.UseSerilogRequestLogging();

app.UseAuthentication();
app.UseAuthorization();

app.MigrateDatabase<Program>();
app.MapGrpcService<DiscountService>();
app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");

app.Run();

// Make the implicit Program class public for integration tests
public partial class Program { }
