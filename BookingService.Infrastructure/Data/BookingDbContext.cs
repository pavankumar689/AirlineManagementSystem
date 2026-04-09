using BookingService.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BookingService.Infrastructure.Data;

public class BookingDbContext : DbContext
{
    public BookingDbContext(DbContextOptions<BookingDbContext> options) : base(options) { }

    public DbSet<Booking> Bookings => Set<Booking>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Payment is owned by PaymentService — ignore the navigation property
        // so EF Core does not try to create or join a Payment table in this DB
        modelBuilder.Entity<Booking>().Ignore(b => b.Payment);
    }
}