using FlightService.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FlightService.Infrastructure.Data;

public class FlightDbContext : DbContext
{
    public FlightDbContext(DbContextOptions<FlightDbContext> options) : base(options) { }

    public DbSet<Airport> Airports => Set<Airport>();
    public DbSet<Flight> Flights => Set<Flight>();
    public DbSet<Schedule> Schedules => Set<Schedule>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Fix: Tell SQL Server NOT to cascade delete on these two relationships
        modelBuilder.Entity<Flight>()
            .HasOne(f => f.OriginAirport)
            .WithMany()
            .HasForeignKey(f => f.OriginAirportId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<Flight>()
            .HasOne(f => f.DestinationAirport)
            .WithMany()
            .HasForeignKey(f => f.DestinationAirportId)
            .OnDelete(DeleteBehavior.NoAction);

        // Seed airports
        modelBuilder.Entity<Airport>().HasData(
            new Airport { Id = 1, Name = "Indira Gandhi International Airport", Code = "DEL", City = "Delhi", Country = "India" },
            new Airport { Id = 2, Name = "Chhatrapati Shivaji Maharaj International Airport", Code = "BOM", City = "Mumbai", Country = "India" },
            new Airport { Id = 3, Name = "Kempegowda International Airport", Code = "BLR", City = "Bangalore", Country = "India" },
            new Airport { Id = 4, Name = "Chennai International Airport", Code = "MAA", City = "Chennai", Country = "India" }
        );
    }
}