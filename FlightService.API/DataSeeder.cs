using FlightService.Infrastructure.Data;
using FlightService.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FlightService.API;

public static class DataSeeder
{
    public static void Seed(FlightDbContext context)
    {
        // Check if we already seeded our custom international airports
        if (!context.Airports.Any(a => a.Code == "JFK"))
        {
            var jfk = new Airport { Name = "John F. Kennedy International", Code = "JFK", City = "New York", Country = "USA" };
            var lhr = new Airport { Name = "Heathrow Airport", Code = "LHR", City = "London", Country = "UK" };
            var dxb = new Airport { Name = "Dubai International", Code = "DXB", City = "Dubai", Country = "UAE" };
            var hnd = new Airport { Name = "Haneda Airport", Code = "HND", City = "Tokyo", Country = "Japan" };
            var syd = new Airport { Name = "Sydney Kingsford Smith", Code = "SYD", City = "Sydney", Country = "Australia" };
            var sfo = new Airport { Name = "San Francisco International", Code = "SFO", City = "San Francisco", Country = "USA" };
            var cdg = new Airport { Name = "Paris Charles de Gaulle", Code = "CDG", City = "Paris", Country = "France" };
            var sin = new Airport { Name = "Changi Airport", Code = "SIN", City = "Singapore", Country = "Singapore" };

            context.Airports.AddRange(jfk, lhr, dxb, hnd, syd, sfo, cdg, sin);
            context.SaveChanges();

            // Fetch the Indian airports that are seeded by EnsureCreated via HasData
            var del = context.Airports.FirstOrDefault(a => a.Code == "DEL");
            var bom = context.Airports.FirstOrDefault(a => a.Code == "BOM");
            var blr = context.Airports.FirstOrDefault(a => a.Code == "BLR");
            var maa = context.Airports.FirstOrDefault(a => a.Code == "MAA");

            if (del == null || bom == null) return;

            var flights = new List<Flight>
            {
                new Flight { FlightNumber = "VK-100", Airline = "Veloskyra Airlines", OriginAirportId = jfk.Id, DestinationAirportId = lhr.Id, TotalEconomySeats = 250, TotalBusinessSeats = 40 },
                new Flight { FlightNumber = "VK-101", Airline = "Veloskyra Airlines", OriginAirportId = lhr.Id, DestinationAirportId = jfk.Id, TotalEconomySeats = 250, TotalBusinessSeats = 40 },
                
                new Flight { FlightNumber = "VK-200", Airline = "Veloskyra Airlines", OriginAirportId = lhr.Id, DestinationAirportId = dxb.Id, TotalEconomySeats = 300, TotalBusinessSeats = 50 },
                new Flight { FlightNumber = "VK-201", Airline = "Veloskyra Airlines", OriginAirportId = dxb.Id, DestinationAirportId = lhr.Id, TotalEconomySeats = 300, TotalBusinessSeats = 50 },
                
                new Flight { FlightNumber = "VK-300", Airline = "Veloskyra Airlines", OriginAirportId = dxb.Id, DestinationAirportId = del.Id, TotalEconomySeats = 200, TotalBusinessSeats = 30 },
                new Flight { FlightNumber = "VK-301", Airline = "Veloskyra Airlines", OriginAirportId = del.Id, DestinationAirportId = dxb.Id, TotalEconomySeats = 200, TotalBusinessSeats = 30 },
                
                new Flight { FlightNumber = "VK-400", Airline = "Veloskyra Airlines", OriginAirportId = del.Id, DestinationAirportId = sin.Id, TotalEconomySeats = 180, TotalBusinessSeats = 20 },
                new Flight { FlightNumber = "VK-401", Airline = "Veloskyra Airlines", OriginAirportId = sin.Id, DestinationAirportId = del.Id, TotalEconomySeats = 180, TotalBusinessSeats = 20 },

                new Flight { FlightNumber = "VK-500", Airline = "Veloskyra Airlines", OriginAirportId = sin.Id, DestinationAirportId = syd.Id, TotalEconomySeats = 350, TotalBusinessSeats = 60 },
                new Flight { FlightNumber = "VK-501", Airline = "Veloskyra Airlines", OriginAirportId = syd.Id, DestinationAirportId = sin.Id, TotalEconomySeats = 350, TotalBusinessSeats = 60 },

                new Flight { FlightNumber = "VK-600", Airline = "Veloskyra Airlines", OriginAirportId = syd.Id, DestinationAirportId = sfo.Id, TotalEconomySeats = 400, TotalBusinessSeats = 80 },
                new Flight { FlightNumber = "VK-601", Airline = "Veloskyra Airlines", OriginAirportId = sfo.Id, DestinationAirportId = syd.Id, TotalEconomySeats = 400, TotalBusinessSeats = 80 },

                new Flight { FlightNumber = "VK-700", Airline = "Veloskyra Airlines", OriginAirportId = sfo.Id, DestinationAirportId = hnd.Id, TotalEconomySeats = 300, TotalBusinessSeats = 50 },
                new Flight { FlightNumber = "VK-701", Airline = "Veloskyra Airlines", OriginAirportId = hnd.Id, DestinationAirportId = sfo.Id, TotalEconomySeats = 300, TotalBusinessSeats = 50 },

                new Flight { FlightNumber = "VK-800", Airline = "Veloskyra Airlines", OriginAirportId = hnd.Id, DestinationAirportId = del.Id, TotalEconomySeats = 280, TotalBusinessSeats = 40 },
                new Flight { FlightNumber = "VK-801", Airline = "Veloskyra Airlines", OriginAirportId = del.Id, DestinationAirportId = hnd.Id, TotalEconomySeats = 280, TotalBusinessSeats = 40 },

                new Flight { FlightNumber = "VK-900", Airline = "Veloskyra Airlines", OriginAirportId = bom.Id, DestinationAirportId = lhr.Id, TotalEconomySeats = 250, TotalBusinessSeats = 30 },
                new Flight { FlightNumber = "VK-901", Airline = "Veloskyra Airlines", OriginAirportId = lhr.Id, DestinationAirportId = bom.Id, TotalEconomySeats = 250, TotalBusinessSeats = 30 },

                new Flight { FlightNumber = "VK-010", Airline = "Veloskyra Airlines", OriginAirportId = cdg.Id, DestinationAirportId = dxb.Id, TotalEconomySeats = 330, TotalBusinessSeats = 55 },
                new Flight { FlightNumber = "VK-011", Airline = "Veloskyra Airlines", OriginAirportId = dxb.Id, DestinationAirportId = cdg.Id, TotalEconomySeats = 330, TotalBusinessSeats = 55 },
                
                new Flight { FlightNumber = "VK-020", Airline = "Veloskyra Airlines", OriginAirportId = blr.Id, DestinationAirportId = sin.Id, TotalEconomySeats = 220, TotalBusinessSeats = 25 },
                new Flight { FlightNumber = "VK-021", Airline = "Veloskyra Airlines", OriginAirportId = sin.Id, DestinationAirportId = blr.Id, TotalEconomySeats = 220, TotalBusinessSeats = 25 }
            };

            context.Flights.AddRange(flights);
            context.SaveChanges();

            // Generate daily schedules for the next 30 days
            var schedules = new List<Schedule>();
            var baseDate = DateTime.UtcNow.Date;

            foreach (var f in flights)
            {
                for (int i = 0; i < 30; i++)
                {
                    // Generate pseudo-random consistent hour between 6 AM and 10 PM for each flight
                    int depHour = 6 + (f.Id % 16);
                    var depTime = baseDate.AddDays(i).AddHours(depHour);
                    
                    // Estimate random duration 3-12 hours
                    int durationHours = 3 + (f.Id % 9);
                    var arrTime = depTime.AddHours(durationHours).AddMinutes(f.Id % 60);

                    // Make base price realistic for INR (e.g., ₹15,000 to ₹80,000 depending on distance)
                    decimal basePrice = 15000m + ((f.Id % 10) * 7500m);

                    schedules.Add(new Schedule
                    {
                        FlightId = f.Id,
                        DepartureTime = depTime,
                        ArrivalTime = arrTime,
                        EconomyPrice = basePrice,
                        BusinessPrice = basePrice * 2.5m,
                        AvailableEconomySeats = f.TotalEconomySeats,
                        AvailableBusinessSeats = f.TotalBusinessSeats,
                        Status = "Scheduled"
                    });
                }
            }
            context.Schedules.AddRange(schedules);
            context.SaveChanges();
        }

        if (!context.Airports.Any(a => a.Code == "HYD"))
        {
            var hyd = new Airport { Name = "Rajiv Gandhi International", Code = "HYD", City = "Hyderabad", Country = "India" };
            var ccu = new Airport { Name = "Netaji Subhas Chandra Bose", Code = "CCU", City = "Kolkata", Country = "India" };
            var pnq = new Airport { Name = "Pune Airport", Code = "PNQ", City = "Pune", Country = "India" };
            var amd = new Airport { Name = "Sardar Vallabhbhai Patel", Code = "AMD", City = "Ahmedabad", Country = "India" };

            context.Airports.AddRange(hyd, ccu, pnq, amd);
            context.SaveChanges();

            var del = context.Airports.FirstOrDefault(a => a.Code == "DEL");
            var bom = context.Airports.FirstOrDefault(a => a.Code == "BOM");
            var blr = context.Airports.FirstOrDefault(a => a.Code == "BLR");
            var maa = context.Airports.FirstOrDefault(a => a.Code == "MAA");

            if (del != null && bom != null && blr != null && maa != null)
            {
                var domesticFlights = new List<Flight>
                {
                    new Flight { FlightNumber = "VK-D10", Airline = "Veloskyra Airlines", OriginAirportId = del.Id, DestinationAirportId = hyd.Id, TotalEconomySeats = 180, TotalBusinessSeats = 12 },
                    new Flight { FlightNumber = "VK-D11", Airline = "Veloskyra Airlines", OriginAirportId = hyd.Id, DestinationAirportId = del.Id, TotalEconomySeats = 180, TotalBusinessSeats = 12 },
                    
                    new Flight { FlightNumber = "VK-D20", Airline = "Veloskyra Airlines", OriginAirportId = bom.Id, DestinationAirportId = ccu.Id, TotalEconomySeats = 180, TotalBusinessSeats = 12 },
                    new Flight { FlightNumber = "VK-D21", Airline = "Veloskyra Airlines", OriginAirportId = ccu.Id, DestinationAirportId = bom.Id, TotalEconomySeats = 180, TotalBusinessSeats = 12 },

                    new Flight { FlightNumber = "VK-D30", Airline = "Veloskyra Airlines", OriginAirportId = blr.Id, DestinationAirportId = pnq.Id, TotalEconomySeats = 150, TotalBusinessSeats = 8 },
                    new Flight { FlightNumber = "VK-D31", Airline = "Veloskyra Airlines", OriginAirportId = pnq.Id, DestinationAirportId = blr.Id, TotalEconomySeats = 150, TotalBusinessSeats = 8 },

                    new Flight { FlightNumber = "VK-D40", Airline = "Veloskyra Airlines", OriginAirportId = del.Id, DestinationAirportId = amd.Id, TotalEconomySeats = 180, TotalBusinessSeats = 12 },
                    new Flight { FlightNumber = "VK-D41", Airline = "Veloskyra Airlines", OriginAirportId = amd.Id, DestinationAirportId = del.Id, TotalEconomySeats = 180, TotalBusinessSeats = 12 },

                    new Flight { FlightNumber = "VK-D50", Airline = "Veloskyra Airlines", OriginAirportId = hyd.Id, DestinationAirportId = maa.Id, TotalEconomySeats = 150, TotalBusinessSeats = 8 },
                    new Flight { FlightNumber = "VK-D51", Airline = "Veloskyra Airlines", OriginAirportId = maa.Id, DestinationAirportId = hyd.Id, TotalEconomySeats = 150, TotalBusinessSeats = 8 },
                    
                    new Flight { FlightNumber = "VK-D60", Airline = "Veloskyra Airlines", OriginAirportId = ccu.Id, DestinationAirportId = blr.Id, TotalEconomySeats = 180, TotalBusinessSeats = 12 },
                    new Flight { FlightNumber = "VK-D61", Airline = "Veloskyra Airlines", OriginAirportId = blr.Id, DestinationAirportId = ccu.Id, TotalEconomySeats = 180, TotalBusinessSeats = 12 }
                };

                context.Flights.AddRange(domesticFlights);
                context.SaveChanges();

                var schedules = new List<Schedule>();
                var baseDate = DateTime.UtcNow.Date;

                foreach (var f in domesticFlights)
                {
                    for (int i = 0; i < 30; i++)
                    {
                        int depHour = 5 + (f.Id % 18); // 5 AM to 11 PM
                        var depTime = baseDate.AddDays(i).AddHours(depHour);
                        
                        int durationHours = 1 + (f.Id % 2); // 1-2 hours for domestic
                        var arrTime = depTime.AddHours(durationHours).AddMinutes((f.Id * 15) % 60);

                        decimal basePrice = 3500m + ((f.Id % 5) * 1200m); // ₹3500 - ₹8300

                        schedules.Add(new Schedule
                        {
                            FlightId = f.Id,
                            DepartureTime = depTime,
                            ArrivalTime = arrTime,
                            EconomyPrice = basePrice,
                            BusinessPrice = basePrice * 3m, // ₹10500 - ₹24900
                            AvailableEconomySeats = f.TotalEconomySeats,
                            AvailableBusinessSeats = f.TotalBusinessSeats,
                            Status = "Scheduled"
                        });
                    }
                }
                context.Schedules.AddRange(schedules);
                context.SaveChanges();
            }
        }
    }
}
