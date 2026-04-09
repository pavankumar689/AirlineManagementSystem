using BookingService.Application.Interfaces;
using BookingService.Infrastructure.Data;
using BookingService.Infrastructure.HttpClients;
using BookingService.Infrastructure.Messaging;
using BookingService.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using NLog;
using NLog.Web;
using Scalar.AspNetCore;
using Shared.Events.ExceptionHandlers;

// Bootstrap NLog before anything else so startup errors are captured
var logger = LogManager.Setup()
    .LoadConfigurationFromFile("nlog.config")
    .GetCurrentClassLogger();

logger.Info("BookingService starting up...");

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Host.UseNLog();
builder.Services.AddHttpContextAccessor();

builder.Services.AddDbContext<BookingDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddHttpClient<IFlightServiceClient, FlightServiceClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["FlightServiceUrl"]!);
});

builder.Services.AddScoped<IBookingService, BookingServiceImpl>();
builder.Services.AddSingleton<RabbitMQPublisher>();
builder.Services.AddHostedService<BookingEventConsumer>();
builder.Services.AddHostedService<BookingCleanupService>();

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
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "BookingService API", Version = "v1" });
});

var app = builder.Build();
app.UseExceptionHandler();
app.UseCors("AllowAll");
app.MapScalarApiReference(options =>
{
    options.WithTitle("BookingService API")
           .WithTheme(ScalarTheme.Purple)
           .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient)
           .WithOpenApiRoutePattern("/swagger/{documentName}/swagger.json");
});

app.UseSwagger();
app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
    db.Database.EnsureCreated();
}

app.Run();
