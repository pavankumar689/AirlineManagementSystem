using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;

namespace PaymentService.Infrastructure.Services;

public class RazorpayService
{
    private readonly string _keyId;
    private readonly string _keySecret;

    public RazorpayService(IConfiguration config)
    {
        _keyId = config["Razorpay:KeyId"]!;
        _keySecret = config["Razorpay:KeySecret"]!;
    }

    public string CreateOrder(decimal amount, string currency = "INR")
    {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes($"{_keyId}:{_keySecret}")));

        var content = new StringContent(
            System.Text.Json.JsonSerializer.Serialize(new
            {
                amount = (int)(amount * 100),
                currency = currency,
                receipt = $"order_{Guid.NewGuid().ToString()[..8]}",
                payment_capture = 1
            }),
            System.Text.Encoding.UTF8,
            "application/json");

        var response = client.PostAsync("https://api.razorpay.com/v1/orders", content).GetAwaiter().GetResult();
        var responseString = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"Razorpay API returned {response.StatusCode}: {responseString}");
        }

        using var json = System.Text.Json.JsonDocument.Parse(responseString);
        return json.RootElement.GetProperty("id").GetString()!;
    }

    public bool VerifyPayment(string orderId, string paymentId, string signature)
    {
        try
        {
            // Pure HMAC-SHA256 — no SDK network calls, no NullReferenceException bugs
            // Razorpay signature = HMAC_SHA256(key=keySecret, data="{orderId}|{paymentId}")
            var payload = $"{orderId}|{paymentId}";
            using var hmac = new HMACSHA256(System.Text.Encoding.UTF8.GetBytes(_keySecret));
            var hash = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(payload));
            var computedSignature = BitConverter.ToString(hash).Replace("-", "").ToLower();

            Console.WriteLine($"[VerifyPayment] Expected: {computedSignature}, Got: {signature}");
            return computedSignature == signature;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[VerifyPayment] Exception during signature check: {ex.Message}");
            return false;
        }
    }
}