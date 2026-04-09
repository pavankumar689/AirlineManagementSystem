using Microsoft.EntityFrameworkCore;
using PaymentService.Application.DTOs;
using PaymentService.Application.Interfaces;
using PaymentService.Infrastructure.Data;

namespace PaymentService.Infrastructure.Services;

public class PaymentServiceImpl : IPaymentService
{
    private readonly PaymentDbContext _db;

    public PaymentServiceImpl(PaymentDbContext db)
    {
        _db = db;
    }

    public async Task<List<PaymentResponseDto>> GetAllPaymentsAsync()
    {
        var payments = await _db.Payments
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        return payments.Select(MapToResponse).ToList();
    }

    public async Task<List<PaymentResponseDto>> GetPaymentsByPassengerAsync(int passengerId)
    {
        var payments = await _db.Payments
            .Where(p => p.PassengerId == passengerId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        return payments.Select(MapToResponse).ToList();
    }

    private PaymentResponseDto MapToResponse(PaymentService.Domain.Entities.Payment payment)
    {
        return new PaymentResponseDto
        {
            Id = payment.Id,
            BookingId = payment.BookingId,
            Amount = payment.Amount,
            Method = payment.Method,
            Status = payment.Status,
            FlightNumber = payment.FlightNumber,
            CreatedAt = payment.CreatedAt,
            PaidAt = payment.PaidAt,
            FailureReason = payment.FailureReason
        };
    }
}