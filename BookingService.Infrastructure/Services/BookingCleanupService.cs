using BookingService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BookingService.Infrastructure.Services;

/// <summary>
/// Background service that runs every 5 minutes and auto-cancels any
/// Pending bookings older than 15 minutes whose payment never completed.
/// This prevents permanently blocked seats caused by failed/abandoned payments.
/// </summary>
public class BookingCleanupService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<BookingCleanupService> _logger;
    private static readonly TimeSpan RunInterval  = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan PendingExpiry = TimeSpan.FromMinutes(15);

    public BookingCleanupService(
        IServiceProvider services,
        ILogger<BookingCleanupService> logger)
    {
        _services = services;
        _logger   = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("BookingCleanupService started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupExpiredBookingsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during booking cleanup.");
            }

            await Task.Delay(RunInterval, stoppingToken);
        }
    }

    private async Task CleanupExpiredBookingsAsync()
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();

        var cutoff = DateTime.UtcNow.Subtract(PendingExpiry);

        // Find all stale Pending bookings
        var stale = await db.Bookings
            .Where(b => b.Status == "Pending" && b.CreatedAt < cutoff)
            .ToListAsync();

        if (!stale.Any())
            return;

        foreach (var booking in stale)
            booking.Status = "Cancelled";

        await db.SaveChangesAsync();

        _logger.LogInformation(
            "Cleanup: auto-cancelled {Count} stale pending booking(s) older than {Minutes} minutes.",
            stale.Count, PendingExpiry.TotalMinutes);
    }
}
