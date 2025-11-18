using Basket.API.GrpcServices;
using Basket.API.Repositories;
using Common.Auth;
using Common.Observability;
using Discount.Grpc.Protos;
using MassTransit;
using Microsoft.Extensions.Configuration;

var builder = WebApplication.CreateBuilder(args);

// Add Serilog for structured logging
builder.AddSerilog();

// Add services to the container.
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetValue<string>("CacheSettings:ConnectionString");
});

builder.Services.AddScoped<IBasketRepository, BasketRepository>();
builder.Services.AddAutoMapper(typeof(Program));
builder.Services.AddGrpcClient<DiscountProtoService.DiscountProtoServiceClient>
    (o => o.Address = new Uri(builder.Configuration["GrpcSettings:DiscountUrl"]));
builder.Services.AddScoped<IDiscountGrpcService, DiscountGrpcService>();
builder.Services.AddMassTransit(config =>
        {
            config.UsingRabbitMq((ctx, cfg) => {
                cfg.Host(builder.Configuration["EventBusSettings:HostAddress"]);
            });
        });

// Authentication & Authorization
builder.Services.AddJwtAuthentication(builder.Configuration);
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(PolicyConstants.ReadBasket, policy =>
        policy.RequireAuthenticatedUser()
              .RequireClaim("scope", PolicyConstants.Scopes.BasketRead));

    options.AddPolicy(PolicyConstants.WriteBasket, policy =>
        policy.RequireAuthenticatedUser()
              .RequireClaim("scope", PolicyConstants.Scopes.BasketWrite)
              .RequireRole(PolicyConstants.Roles.Admin, PolicyConstants.Roles.User));
});

// Observability - OpenTelemetry with MassTransit
builder.Services.AddObservabilityWithSource(builder.Configuration, "MassTransit");

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

app.Run();

// Make the implicit Program class public for integration tests
public partial class Program { }
