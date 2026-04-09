namespace FlightService.Domain.Entities;

public class Airport
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty; // DEL, BOM, BLR
    public string City { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
}