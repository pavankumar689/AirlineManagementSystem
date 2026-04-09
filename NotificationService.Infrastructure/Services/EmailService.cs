using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using MimeKit;
using NotificationService.Domain.Entities;
using NotificationService.Infrastructure.Data;

namespace NotificationService.Infrastructure.Services;

public class EmailService
{
    private readonly IConfiguration _config;
    private readonly NotificationDbContext _db;

    public EmailService(IConfiguration config, NotificationDbContext db)
    {
        _config = config;
        _db = db;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // BOOKING CONFIRMED  (with PDF boarding pass attachment)
    // ─────────────────────────────────────────────────────────────────────────
    public async Task SendBookingConfirmedAsync(
        string toEmail,
        string passengerName,
        string flightNumber,
        string origin,
        string destination,
        DateTime departureTime,
        string seatNumber,
        string seatClass,
        decimal amount,
        string pnr)
    {
        var subject = $"✈ Your Booking is Confirmed! Flight {flightNumber} — Veloskyra";

        var firstName = passengerName.Split(' ')[0];

        var html = $@"
{BaseHtml($@"
  <div style='background:#1a237e;padding:32px 40px;text-align:center;'>
    <h1 style='color:#ffffff;font-size:28px;margin:0;letter-spacing:1px;'>✈ Veloskyra</h1>
    <p style='color:#f9a825;font-size:12px;margin:6px 0 0;letter-spacing:3px;text-transform:uppercase;'>Your Journey Begins Here</p>
  </div>

  <div style='padding:36px 40px 20px;'>
    <div style='background:#e8f5e9;border-left:4px solid #43a047;border-radius:6px;padding:16px 20px;margin-bottom:28px;'>
      <p style='margin:0;color:#2e7d32;font-size:16px;font-weight:700;'>🎉 Booking Confirmed!</p>
      <p style='margin:4px 0 0;color:#388e3c;font-size:13px;'>Your seat is reserved and payment has been received successfully.</p>
    </div>

    <p style='font-size:16px;color:#333;'>Dear <strong>{firstName}</strong>,</p>
    <p style='font-size:14px;color:#555;line-height:1.7;'>
      Thank you for choosing <strong>Veloskyra Airlines</strong> for your travel. We are delighted to confirm your booking.
      Your boarding pass is attached to this email as a PDF — please present it at the airport.
    </p>

    <!-- Flight Details Card -->
    <div style='background:#f4f6fb;border-radius:12px;padding:24px;margin:24px 0;'>
      <p style='font-size:12px;color:#888;text-transform:uppercase;letter-spacing:2px;margin:0 0 16px;font-weight:700;'>Flight Details</p>

      <!-- Route display -->
      <div style='display:flex;align-items:center;justify-content:space-between;text-align:center;margin-bottom:20px;'>
        <div>
          <p style='font-size:36px;font-weight:800;color:#1a237e;margin:0;'>{origin}</p>
          <p style='font-size:11px;color:#888;margin:2px 0 0;'>Origin</p>
        </div>
        <div>
          <p style='font-size:28px;color:#f9a825;margin:0;'>✈</p>
          <p style='font-size:10px;color:#aaa;margin:0;'>{flightNumber}</p>
        </div>
        <div>
          <p style='font-size:36px;font-weight:800;color:#1a237e;margin:0;'>{destination}</p>
          <p style='font-size:11px;color:#888;margin:2px 0 0;'>Destination</p>
        </div>
      </div>

      <table style='width:100%;border-collapse:collapse;'>
        {DetailRow("📅 Departure", departureTime.ToString("dddd, dd MMM yyyy  •  hh:mm tt"))}
        {DetailRow("💺 Seat", $"{seatNumber}  ({seatClass} Class)")}
        {DetailRow("🏷 Flight No.", flightNumber)}
        {DetailRow("💳 Amount Paid", $"₹{amount:N2}")}
      </table>
    </div>

    <!-- PNR -->
    <div style='background:#1a237e;border-radius:10px;padding:20px 24px;text-align:center;margin:0 0 28px;'>
      <p style='color:#f9a825;font-size:11px;letter-spacing:3px;text-transform:uppercase;margin:0 0 6px;'>Booking Reference / PNR</p>
      <p style='color:#ffffff;font-size:34px;font-weight:800;letter-spacing:8px;font-family:""Courier New"",monospace;margin:0;'>{pnr}</p>
      <p style='color:#90caf9;font-size:11px;margin:8px 0 0;'>Use this code at the check-in counter or self-service kiosk</p>
    </div>

    <div style='background:#fff8e1;border-radius:8px;padding:14px 18px;margin-bottom:24px;'>
      <p style='margin:0;color:#795548;font-size:13px;'>
        📎 <strong>Your boarding pass is attached.</strong> Please download and keep it handy —
        you can present it digitally or take a printout to the airport.
      </p>
    </div>

    <p style='font-size:14px;color:#555;line-height:1.7;'>
      We look forward to welcoming you onboard. If you have any questions regarding your journey, 
      please don't hesitate to contact our support team at
      <a href='mailto:support@veloskyra.com' style='color:#1a237e;'>support@veloskyra.com</a>.
    </p>

    <p style='font-size:14px;color:#333;margin-top:24px;'>Warm regards,<br/>
      <strong style='color:#1a237e;'>The Veloskyra Team</strong><br/>
      <span style='font-size:12px;color:#888;'>Your trusted travel partner ✈</span>
    </p>
  </div>
")}";

        // Generate boarding pass PDF
        byte[]? pdfBytes = null;
        try
        {
            pdfBytes = BoardingPassGenerator.Generate(
                passengerName, flightNumber, origin, destination,
                departureTime, seatNumber, seatClass, pnr, amount);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠ Boarding pass PDF generation failed: {ex.Message}");
        }

        await SendEmailAsync(toEmail, subject, html, "BookingConfirmed", pdfBytes,
            pnr.Length > 0 ? $"BoardingPass_{pnr}_{flightNumber}.pdf" : $"BoardingPass_{flightNumber}.pdf");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // BOOKING CANCELLED
    // ─────────────────────────────────────────────────────────────────────────
    public async Task SendBookingCancelledAsync(
        string toEmail,
        string passengerName,
        string flightNumber,
        decimal refundAmount)
    {
        var subject = $"Your Booking Cancellation — Flight {flightNumber} | Veloskyra";
        var firstName = passengerName.Split(' ')[0];

        var html = $@"
{BaseHtml($@"
  <div style='background:#1a237e;padding:32px 40px;text-align:center;'>
    <h1 style='color:#ffffff;font-size:28px;margin:0;letter-spacing:1px;'>✈ Veloskyra</h1>
    <p style='color:#f9a825;font-size:12px;margin:6px 0 0;letter-spacing:3px;text-transform:uppercase;'>Customer Support</p>
  </div>

  <div style='padding:36px 40px 20px;'>
    <div style='background:#fff3e0;border-left:4px solid #ef6c00;border-radius:6px;padding:16px 20px;margin-bottom:28px;'>
      <p style='margin:0;color:#e65100;font-size:16px;font-weight:700;'>Booking Cancelled</p>
      <p style='margin:4px 0 0;color:#bf360c;font-size:13px;'>Flight {flightNumber} — Cancellation Processed</p>
    </div>

    <p style='font-size:16px;color:#333;'>Dear <strong>{firstName}</strong>,</p>
    <p style='font-size:14px;color:#555;line-height:1.7;'>
      We have received your cancellation request and have successfully processed it. 
      We completely understand that travel plans can change, and we appreciate you letting us know.
    </p>

    <!-- Refund Card -->
    <div style='background:#f4f6fb;border-radius:12px;padding:24px;margin:24px 0;'>
      <p style='font-size:12px;color:#888;text-transform:uppercase;letter-spacing:2px;margin:0 0 16px;font-weight:700;'>Cancellation & Refund Summary</p>
      <table style='width:100%;border-collapse:collapse;'>
        {DetailRow("✈ Flight", flightNumber)}
        {DetailRow("💰 Refund Amount", $"₹{refundAmount:N2}")}
        {DetailRow("📅 Refund Timeline", "5–7 business days to your original payment method")}
        {DetailRow("📋 Deduction", "10% cancellation fee has been applied as per our policy")}
      </table>
    </div>

    <div style='background:#e3f2fd;border-radius:8px;padding:14px 18px;margin-bottom:24px;'>
      <p style='margin:0;color:#1565c0;font-size:13px;'>
        💡 <strong>Did you know?</strong> Veloskyra members enjoy reduced cancellation fees and priority rebooking.
        <a href='#' style='color:#1a237e;font-weight:700;'>Explore our membership plans →</a>
      </p>
    </div>

    <p style='font-size:14px;color:#555;line-height:1.7;'>
      We truly hope to have the opportunity to serve you again. If there is anything we can do to make your 
      next journey more comfortable, please reach out to us at
      <a href='mailto:support@veloskyra.com' style='color:#1a237e;'>support@veloskyra.com</a>.
    </p>

    <p style='font-size:14px;color:#333;margin-top:24px;'>
      With sincere apologies for any inconvenience,<br/>
      <strong style='color:#1a237e;'>The Veloskyra Customer Care Team</strong><br/>
      <span style='font-size:12px;color:#888;'>We hope to welcome you onboard again soon ✈</span>
    </p>
  </div>
")}";

        await SendEmailAsync(toEmail, subject, html, "BookingCancelled");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // FLIGHT STATUS CHANGED  (alert subscribers)
    // ─────────────────────────────────────────────────────────────────────────
    public async Task SendFlightAlertAsync(
        string toEmail,
        string passengerName,
        string flightNumber,
        string origin,
        string destination,
        DateTime departureTime,
        string oldStatus,
        string newStatus)
    {
        var (emoji, badgeColor, badgeBg, headline, reason, advice) =
            GetStatusChangeContent(oldStatus, newStatus, flightNumber, departureTime);

        var subject = $"{emoji} Flight {flightNumber} Status Update: {oldStatus} → {newStatus} | Veloskyra";
        var firstName = passengerName.Split(' ')[0];

        var html = $@"
{BaseHtml($@"
  <div style='background:#1a237e;padding:32px 40px;text-align:center;'>
    <h1 style='color:#ffffff;font-size:28px;margin:0;letter-spacing:1px;'>✈ Veloskyra</h1>
    <p style='color:#f9a825;font-size:12px;margin:6px 0 0;letter-spacing:3px;text-transform:uppercase;'>Flight Status Alert</p>
  </div>

  <div style='padding:36px 40px 20px;'>
    <!-- Status Badge -->
    <div style='background:{badgeBg};border-radius:10px;padding:20px 24px;margin-bottom:28px;text-align:center;'>
      <p style='font-size:32px;margin:0 0 4px;'>{emoji}</p>
      <p style='margin:0;color:{badgeColor};font-size:18px;font-weight:800;'>{headline}</p>
      <p style='margin:6px 0 0;color:{badgeColor};opacity:0.85;font-size:13px;'>Flight {flightNumber} &nbsp;•&nbsp; {origin} → {destination}</p>
    </div>

    <p style='font-size:16px;color:#333;'>Dear <strong>{firstName}</strong>,</p>
    <p style='font-size:14px;color:#555;line-height:1.8;'>{reason}</p>

    <!-- Flight Details -->
    <div style='background:#f4f6fb;border-radius:12px;padding:24px;margin:24px 0;'>
      <p style='font-size:12px;color:#888;text-transform:uppercase;letter-spacing:2px;margin:0 0 16px;font-weight:700;'>Flight Information</p>
      <table style='width:100%;border-collapse:collapse;'>
        {DetailRow("✈ Flight Number", flightNumber)}
        {DetailRow("📍 Route", $"{origin}  →  {destination}")}
        {DetailRow("🕐 Scheduled Departure", departureTime.ToString("dddd, dd MMM yyyy  •  hh:mm tt"))}
        {DetailRow("⬅ Previous Status", oldStatus)}
        {DetailRow("✅ Current Status", newStatus)}
      </table>
    </div>

    <div style='background:#fff8e1;border-radius:8px;padding:14px 18px;margin-bottom:24px;'>
      <p style='margin:0;color:#6d4c41;font-size:13px;'>
        💡 <strong>What should you do?</strong> {advice}
      </p>
    </div>

    <p style='font-size:14px;color:#555;line-height:1.7;'>
      We appreciate your understanding and patience. For real-time updates, please check our
      <a href='http://localhost:4201/flight-status' style='color:#1a237e;font-weight:700;'>Flight Status page</a>
      or contact us at <a href='mailto:support@veloskyra.com' style='color:#1a237e;'>support@veloskyra.com</a>.
    </p>

    <p style='font-size:14px;color:#333;margin-top:24px;'>
      Sincerely,<br/>
      <strong style='color:#1a237e;'>Veloskyra Flight Operations</strong><br/>
      <span style='font-size:12px;color:#888;'>You subscribed to flight alerts for this route ✈</span>
    </p>
  </div>
")}";

        await SendEmailAsync(toEmail, subject, html, "FlightAlert");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // HELPERS
    // ─────────────────────────────────────────────────────────────────────────

    private static (string emoji, string badgeColor, string badgeBg,
        string headline, string reason, string advice)
        GetStatusChangeContent(string oldStatus, string newStatus,
            string flightNumber, DateTime dep)
    {
        var key = $"{oldStatus.ToLower()}→{newStatus.ToLower()}";
        var depStr = dep.ToString("hh:mm tt");

        return key switch
        {
            "scheduled→delayed" => (
                "🟡", "#7b5800", "#fff8e1",
                $"Flight {flightNumber} is Delayed",
                $"We sincerely regret to inform you that flight <strong>{flightNumber}</strong> " +
                $"originally scheduled to depart at {depStr} has experienced an unexpected delay. " +
                $"This has been caused by air traffic management requirements and operational factors " +
                $"beyond our immediate control. Our ground crew is working diligently to minimise the " +
                $"impact and get you on your way as quickly as possible. " +
                $"We deeply apologise for the inconvenience this causes to your travel plans.",
                "Please remain in the departure lounge and keep an eye on the flight information display boards. " +
                "We will notify you as soon as an updated departure time is confirmed."
            ),

            "delayed→scheduled" => (
                "🟢", "#1b5e20", "#e8f5e9",
                $"Great News — Flight {flightNumber} is Back on Schedule!",
                $"We are very pleased to inform you that flight <strong>{flightNumber}</strong> " +
                $"has been restored to its original schedule following the earlier delay. " +
                $"Our team worked quickly to resolve the operational issue, and the aircraft is " +
                $"now ready for departure as originally planned. " +
                $"We thank you sincerely for your patience and understanding.",
                $"Please proceed to your designated gate at your earliest convenience. " +
                $"Boarding will commence 45 minutes before the scheduled departure at {depStr}."
            ),

            "scheduled→cancelled" => (
                "🔴", "#7f0000", "#ffebee",
                $"Flight {flightNumber} Has Been Cancelled",
                $"We are truly sorry to inform you that flight <strong>{flightNumber}</strong> " +
                $"has been cancelled due to unforeseen operational circumstances. We fully understand " +
                $"how disappointing this news can be, and we want you to know that this decision " +
                $"was made as a last resort, prioritising the safety and wellbeing of our passengers. " +
                $"Veloskyra is committed to ensuring the highest standards of safety at all times.",
                "You are entitled to a full refund or complimentary rebooking on the next available flight. " +
                "Please contact our support team or visit the Veloskyra help desk at the airport for immediate assistance."
            ),

            "delayed→cancelled" => (
                "🔴", "#7f0000", "#ffebee",
                $"Flight {flightNumber} — Further Update: Cancellation",
                $"We are deeply sorry to share that following the earlier delay, " +
                $"flight <strong>{flightNumber}</strong> has unfortunately been cancelled. " +
                $"Despite our team's best efforts to resolve the situation, continuing circumstances " +
                $"have made safe operation of this flight impossible today. " +
                $"We sincerely apologise for the additional disruption this causes you.",
                "Our team is ready to help you with a full refund or rebook you on the earliest available flight to your destination. " +
                "Please contact support at support@veloskyra.com or visit our airport help desk."
            ),

            "cancelled→scheduled" => (
                "🟢", "#1b5e20", "#e8f5e9",
                $"Flight {flightNumber} Has Been Reinstated!",
                $"Wonderful news! We are delighted to inform you that flight <strong>{flightNumber}</strong>, " +
                $"which was previously cancelled, has now been reinstated and is confirmed to operate as scheduled. " +
                $"Our team worked around the clock to restore this flight, and we are so pleased to be able " +
                $"to bring you this positive update.",
                $"Please ensure you are at the airport and ready for boarding 45 minutes before your departure at {depStr}. " +
                "Your original booking reference remains valid."
            ),

            "delayed→delayed" => (
                "🟠", "#bf360c", "#fbe9e7",
                $"Updated Delay Notice — Flight {flightNumber}",
                $"We regret to inform you of a further update regarding the delay of flight <strong>{flightNumber}</strong>. " +
                $"Our team continues to work to minimise the delay as much as possible, " +
                $"and we are committed to providing you with timely updates as the situation evolves. " +
                $"We truly appreciate your continued patience.",
                "Please stay in the departure area and monitor the flight information displays. " +
                "We will send you another notification as soon as we have a confirmed time."
            ),

            _ => (
                "🔵", "#0d47a1", "#e3f2fd",
                $"Flight {flightNumber} Status Update: {newStatus}",
                $"We would like to inform you of an important update regarding flight <strong>{flightNumber}</strong>. " +
                $"The flight status has changed from <strong>{oldStatus}</strong> to <strong>{newStatus}</strong>. " +
                $"We apologise for any inconvenience this may cause and appreciate your understanding.",
                "Please check the Veloskyra Flight Status page or contact the airport for the latest information."
            )
        };
    }

    private static string DetailRow(string label, string value) => $@"
      <tr>
        <td style='padding:8px 0;border-bottom:1px solid #e8eaf6;color:#888;font-size:12px;
                   font-weight:700;text-transform:uppercase;letter-spacing:1px;width:45%;vertical-align:top;'>
          {label}
        </td>
        <td style='padding:8px 0 8px 12px;border-bottom:1px solid #e8eaf6;color:#1a237e;
                   font-size:14px;font-weight:600;vertical-align:top;'>
          {value}
        </td>
      </tr>";

    private static string BaseHtml(string content) => $@"
<!DOCTYPE html>
<html lang='en'>
<head>
  <meta charset='UTF-8'/>
  <meta name='viewport' content='width=device-width,initial-scale=1.0'/>
</head>
<body style='margin:0;padding:0;background:#eef0f5;font-family:""Segoe UI"",Arial,sans-serif;'>
  <table width='100%' cellpadding='0' cellspacing='0' style='background:#eef0f5;padding:30px 0;'>
    <tr>
      <td align='center'>
        <table width='600' cellpadding='0' cellspacing='0'
               style='background:#ffffff;border-radius:16px;overflow:hidden;
                      box-shadow:0 4px 24px rgba(0,0,0,0.10);max-width:600px;'>
          <tr><td>{content}</td></tr>
          <!-- Footer -->
          <tr>
            <td style='background:#1a237e;padding:20px 40px;text-align:center;'>
              <p style='color:#90caf9;font-size:12px;margin:0;'>
                © 2026 Veloskyra Airlines · 
                <a href='#' style='color:#f9a825;text-decoration:none;'>Unsubscribe from alerts</a> · 
                <a href='#' style='color:#f9a825;text-decoration:none;'>Privacy Policy</a>
              </p>
              <p style='color:#5c6bc0;font-size:11px;margin:6px 0 0;'>
                This is an automated message. Please do not reply directly to this email.
              </p>
            </td>
          </tr>
        </table>
      </td>
    </tr>
  </table>
</body>
</html>";

    // ─────────────────────────────────────────────────────────────────────────
    // CORE SEND  (HTML + optional PDF attachment)
    // ─────────────────────────────────────────────────────────────────────────
    private async Task SendEmailAsync(
        string toEmail,
        string subject,
        string htmlBody,
        string type,
        byte[]? pdfAttachment = null,
        string? attachmentFileName = null)
    {
        var log = new NotificationLog
        {
            ToEmail = toEmail,
            Subject = subject,
            Type = type
        };

        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(
                _config["Email:SenderName"] ?? "Veloskyra Airlines",
                _config["Email:SenderEmail"]));
            message.To.Add(new MailboxAddress("", toEmail));
            message.Subject = subject;

            var builder = new BodyBuilder { HtmlBody = htmlBody };

            if (pdfAttachment != null && attachmentFileName != null)
                builder.Attachments.Add(attachmentFileName, pdfAttachment,
                    new ContentType("application", "pdf"));

            message.Body = builder.ToMessageBody();

            using var client = new SmtpClient();
            await client.ConnectAsync(
                _config["Email:SmtpHost"],
                int.Parse(_config["Email:SmtpPort"]!),
                SecureSocketOptions.StartTls);

            await client.AuthenticateAsync(
                _config["Email:SenderEmail"],
                _config["Email:AppPassword"]);

            await client.SendAsync(message);
            await client.DisconnectAsync(true);

            log.IsSuccess = true;
            Console.WriteLine($"✅ Email sent to {toEmail} — {subject}");
        }
        catch (Exception ex)
        {
            log.IsSuccess = false;
            log.ErrorMessage = ex.Message;
            Console.WriteLine($"❌ Email failed: {ex.Message}");
        }
        finally
        {
            _db.NotificationLogs.Add(log);
            await _db.SaveChangesAsync();
        }
    }
}