using FlightService.Application.Interfaces;
using FlightService.Infrastructure.Data;
using FlightService.Infrastructure.Services;
using FlightService.Infrastructure.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using NLog;
using NLog.Web;
using Scalar.AspNetCore;
using Shared.Events.ExceptionHandlers;

var logger = LogManager.Setup()
    .LoadConfigurationFromFile("nlog.config")
    .GetCurrentClassLogger();

logger.Info("FlightService starting up...");

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Host.UseNLog();
builder.Services.AddHttpContextAccessor();

builder.Services.AddDbContext<FlightDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IAirportService, AirportServiceImpl>();
builder.Services.AddScoped<IFlightService, FlightServiceImpl>();
builder.Services.AddScoped<IScheduleService, ScheduleServiceImpl>();
builder.Services.AddSingleton<RabbitMQPublisher>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
});

builder.Services.AddControllers();
builder.Services.AddExceptionHandler<ValidationExceptionHandler>();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "FlightService API", Version = "v1" });
});

var app = builder.Build();
app.UseExceptionHandler();
app.UseCors("AllowAll");
app.MapScalarApiReference(options =>
{
    options.WithTitle("FlightService API")
           .WithTheme(ScalarTheme.Purple)
           .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient)
           .WithOpenApiRoutePattern("/swagger/{documentName}/swagger.json");
});

app.UseSwagger();
app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<FlightDbContext>();
    db.Database.EnsureCreated();
    FlightService.API.DataSeeder.Seed(db);
}

app.Run();
