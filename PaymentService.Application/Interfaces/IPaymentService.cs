using PaymentService.Application.DTOs;

namespace PaymentService.Application.Interfaces;

public interface IPaymentService
{
    Task<List<PaymentResponseDto>> GetAllPaymentsAsync();
    Task<List<PaymentResponseDto>> GetPaymentsByPassengerAsync(int passengerId);
}