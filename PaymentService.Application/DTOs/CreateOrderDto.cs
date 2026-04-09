namespace PaymentService.Application.DTOs;

public class CreateOrderDto
{
    public decimal Amount { get; set; }
    public int BookingId { get; set; }
}