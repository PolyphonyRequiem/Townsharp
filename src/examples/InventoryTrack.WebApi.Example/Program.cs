using Townsharp.Infrastructure.Hosting;
using System.Diagnostics;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using System.Reflection;
using MediatR;
using InventoryTrack.WebApi.Example;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddTownsharpInfra();
builder.Services.AddSingleton<ConsoleClientManager>();
builder.Services.AddSingleton<InventoryTracker>();
builder.Services.AddHostedService<InventoryTrackWorker>();

builder.Services.AddMediatR(config =>
{
    config.RegisterServicesFromAssembly(typeof(Program).Assembly);
});

string ServiceName = Assembly.GetExecutingAssembly().GetName().Name ?? "Unknown Assembly";
ActivitySource ActivitySource = new ActivitySource(ServiceName);

builder.Services.AddOpenTelemetry()
    .WithTracing(tracerProviderBuilder =>
         tracerProviderBuilder
            .AddSource(ActivitySource.Name)
            .ConfigureResource(resource => resource
                .AddService(ServiceName))
            .AddHttpClientInstrumentation()
            .AddOtlpExporter());

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapGet("/server/{serverId}/user/{userMoniker}", async (int serverId, string userMoniker, IMediator mediator) =>
{
    var inventory = await mediator.Send(new GetInventoryCommand(serverId, int.Parse(userMoniker)));
    return Results.Ok(inventory);
})
.WithName("GetUserInServer")
.WithOpenApi();

app.Run();