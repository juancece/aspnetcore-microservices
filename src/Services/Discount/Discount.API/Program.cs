using Common.Auth;
using Common.Observability;
using Discount.API.Repositories;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Discount.API.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add Serilog for structured logging
builder.AddSerilog();

// Add services to the container.
builder.Services.AddScoped<IDiscountRepository, DiscountRepository>();

// Authentication & Authorization
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

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.

// Serilog request logging (before other middleware)
app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MigrateDatabase<Program>();
app.Run();

// Make the implicit Program class public for integration tests
public partial class Program { }
