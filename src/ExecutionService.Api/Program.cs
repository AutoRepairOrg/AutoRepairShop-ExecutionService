using ExecutionService.Application.Events;
using ExecutionService.Application.Interfaces;
using ExecutionService.Application.Services;
using ExecutionService.Domain.Interfaces;
using ExecutionService.Infrastructure.Messaging;
using ExecutionService.Infrastructure.Persistence;
using MongoDB.Driver;

var builder = WebApplication.CreateBuilder(args);

// ── Database (MongoDB) ────────────────────────────────────────────────────────
var mongoConnectionString = builder.Configuration.GetConnectionString("ExecutionDatabase")
    ?? "mongodb://localhost:27017/ExecutionServiceDb";

MongoMappings.Register();

builder.Services.AddSingleton<IMongoClient>(_ => new MongoClient(mongoConnectionString));
builder.Services.AddSingleton(sp =>
{
    var client = sp.GetRequiredService<IMongoClient>();
    var databaseName = new MongoUrl(mongoConnectionString).DatabaseName ?? "ExecutionServiceDb";
    return client.GetDatabase(databaseName);
});

// ── Mensageria: RabbitMQ em produção, Stub em desenvolvimento ─────────────────
var useStubMessaging = builder.Configuration.GetValue<bool>("Messaging:UseStub");

if (useStubMessaging)
{
    builder.Services.AddScoped<IEventPublisher, StubEventPublisher>();
}
else
{
    builder.Services.AddSingleton<RabbitMQ.Client.IConnection>(_ =>
    {
        var factory = new RabbitMQ.Client.ConnectionFactory
        {
            HostName = builder.Configuration["RabbitMQ:Host"] ?? "localhost",
            Port     = int.Parse(builder.Configuration["RabbitMQ:Port"] ?? "5672"),
            UserName = builder.Configuration["RabbitMQ:User"] ?? "guest",
            Password = builder.Configuration["RabbitMQ:Password"] ?? "guest",
        };
        return factory.CreateConnectionAsync().GetAwaiter().GetResult();
    });
    builder.Services.AddScoped<IEventPublisher, RabbitMqEventPublisher>();
    builder.Services.AddHostedService<RabbitMqConsumerService>();
}

// ── DI ────────────────────────────────────────────────────────────────────────
builder.Services.AddScoped<IExecutionRepository, MongoExecutionRepository>();
builder.Services.AddScoped<ExecutionAppService>();
builder.Services.AddScoped<OsExecutionStartedHandler>();
builder.Services.AddScoped<OsCancelledHandler>();

// ── API ───────────────────────────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "ExecutionService API",
        Version = "v1",
        Description = "Microsserviço de execução na oficina — 12SOAT Fase 4"
    });
});

builder.Services.AddHealthChecks()
    .AddMongoDb(mongoConnectionString, name: "mongodb");

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var database = scope.ServiceProvider.GetRequiredService<IMongoDatabase>();
    await MongoExecutionRepository.EnsureIndexesAsync(database);
}

app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
