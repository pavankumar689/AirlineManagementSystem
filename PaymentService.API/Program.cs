using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using NLog;
using NLog.Web;
using PaymentService.Application.Interfaces;
using PaymentService.Infrastructure.Data;
using PaymentService.Infrastructure.Messaging;
using PaymentService.Infrastructure.Services;
using Scalar.AspNetCore;
using Shared.Events.ExceptionHandlers;

var logger = LogManager.Setup()
    .LoadConfigurationFromFile("nlog.config")
    .GetCurrentClassLogger();

logger.Info("PaymentService starting up...");

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Host.UseNLog();
builder.Services.AddHttpContextAccessor();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
});

builder.Services.AddDbContext<PaymentDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IPaymentService, PaymentServiceImpl>();
builder.Services.AddScoped<RabbitMQPublisher>();
builder.Services.AddHostedService<PaymentEventConsumer>();
builder.Services.AddScoped<RazorpayService>();

builder.Services.AddControllers();
builder.Services.AddExceptionHandler<ValidationExceptionHandler>();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "PaymentService API", Version = "v1" });
});

var app = builder.Build();
app.UseExceptionHandler();
app.UseCors("AllowAll");
app.MapScalarApiReference(options =>
{
    options.WithTitle("PaymentService API")
           .WithTheme(ScalarTheme.Purple)
           .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient)
           .WithOpenApiRoutePattern("/swagger/{documentName}/swagger.json");
});

app.UseSwagger();
app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
    db.Database.EnsureCreated();
}

app.Run();
