using Catalog.API.Data;
using Catalog.API.Repositories;
using Common.Auth;
using Common.Observability;

var builder = WebApplication.CreateBuilder(args);

// Add Serilog for structured logging
builder.AddSerilog();

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Data Access
builder.Services.AddScoped<ICatalogContext, CatalogContext>();
builder.Services.AddScoped<IProductRepository, ProductRepository>();

// Authentication & Authorization
builder.Services.AddJwtAuthentication(builder.Configuration);
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(PolicyConstants.ReadCatalog, policy =>
        policy.RequireAuthenticatedUser()
              .RequireClaim("scope", PolicyConstants.Scopes.CatalogRead));

    options.AddPolicy(PolicyConstants.WriteCatalog, policy =>
        policy.RequireAuthenticatedUser()
              .RequireClaim("scope", PolicyConstants.Scopes.CatalogWrite)
              .RequireRole(PolicyConstants.Roles.Admin, PolicyConstants.Roles.User));
});

// Observability - OpenTelemetry distributed tracing
builder.Services.AddObservability(builder.Configuration);

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

app.Run();

// Make Program class accessible to integration tests
public partial class Program { }
