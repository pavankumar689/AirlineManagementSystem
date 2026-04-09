using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using MimeKit;

namespace AuthService.Infrastructure.Services;

public class AuthEmailService
{
    private readonly IConfiguration _config;

    public AuthEmailService(IConfiguration config)
    {
        _config = config;
    }

    public async Task SendPasswordResetEmailAsync(string toEmail, string userName, string resetLink)
    {
        var subject = "🔐 Reset Your Veloskyra Password";
        var firstName = userName.Split(' ')[0];

        var html = $@"
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
          <tr><td>

            <!-- Header -->
            <div style='background:#1a237e;padding:32px 40px;text-align:center;'>
              <h1 style='color:#ffffff;font-size:28px;margin:0;letter-spacing:1px;'>✈ Veloskyra</h1>
              <p style='color:#f9a825;font-size:12px;margin:6px 0 0;letter-spacing:3px;text-transform:uppercase;'>Account Security</p>
            </div>

            <!-- Body -->
            <div style='padding:36px 40px 20px;'>
              <div style='background:#e3f2fd;border-left:4px solid #1565c0;border-radius:6px;padding:16px 20px;margin-bottom:28px;'>
                <p style='margin:0;color:#0d47a1;font-size:16px;font-weight:700;'>🔐 Password Reset Request</p>
                <p style='margin:4px 0 0;color:#1565c0;font-size:13px;'>We received a request to reset your password.</p>
              </div>

              <p style='font-size:16px;color:#333;'>Dear <strong>{firstName}</strong>,</p>
              <p style='font-size:14px;color:#555;line-height:1.7;'>
                Someone (hopefully you!) requested a password reset for your Veloskyra account associated with this email address.
                Click the button below to set a new password. This link is valid for <strong>1 hour</strong>.
              </p>

              <!-- CTA Button -->
              <div style='text-align:center;margin:32px 0;'>
                <a href='{resetLink}'
                   style='display:inline-block;background:linear-gradient(135deg,#1a237e,#3949ab);
                          color:#ffffff;text-decoration:none;padding:16px 40px;
                          border-radius:10px;font-size:16px;font-weight:700;
                          letter-spacing:0.5px;box-shadow:0 4px 14px rgba(26,35,126,0.35);'>
                  Reset My Password →
                </a>
              </div>

              <div style='background:#fff8e1;border-radius:8px;padding:14px 18px;margin-bottom:24px;'>
                <p style='margin:0;color:#6d4c41;font-size:13px;'>
                  ⚠️ <strong>Didn't request this?</strong> You can safely ignore this email.
                  Your password will <strong>not</strong> change unless you click the button above.
                  If you're concerned, please contact us at
                  <a href='mailto:support@veloskyra.com' style='color:#1a237e;'>support@veloskyra.com</a>.
                </p>
              </div>

              <p style='font-size:12px;color:#9ca3af;'>
                Or copy this link into your browser:<br/>
                <span style='color:#1a237e;word-break:break-all;'>{resetLink}</span>
              </p>

              <p style='font-size:14px;color:#333;margin-top:24px;'>
                Safe travels,<br/>
                <strong style='color:#1a237e;'>The Veloskyra Security Team</strong><br/>
                <span style='font-size:12px;color:#888;'>Your trusted travel partner ✈</span>
              </p>
            </div>

          </td></tr>
          <!-- Footer -->
          <tr>
            <td style='background:#1a237e;padding:20px 40px;text-align:center;'>
              <p style='color:#90caf9;font-size:12px;margin:0;'>
                © 2026 Veloskyra Airlines ·
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

        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(
                _config["Email:SenderName"] ?? "Veloskyra Airlines",
                _config["Email:SenderEmail"]));
            message.To.Add(new MailboxAddress("", toEmail));
            message.Subject = subject;

            var builder = new BodyBuilder { HtmlBody = html };
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

            Console.WriteLine($"✅ Password reset email sent to {toEmail}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Password reset email failed: {ex.Message}");
            throw; // Re-throw so caller knows it failed
        }
    }
}
