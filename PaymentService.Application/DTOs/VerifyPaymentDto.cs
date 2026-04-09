namespace PaymentService.Application.DTOs;

public class VerifyPaymentDto
{
    public string OrderId { get; set; } = string.Empty;
    public string PaymentId { get; set; } = string.Empty;
    public string Signature { get; set; } = string.Empty;
    public int BookingId { get; set; }
}