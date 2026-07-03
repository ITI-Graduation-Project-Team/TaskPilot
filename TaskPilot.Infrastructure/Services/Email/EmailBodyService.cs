using TaskPilot.Services.Interfaces.External;

namespace TaskPilot.Infrastructure.Services.Email
{
    public class EmailBodyService : IEmailBodyService
    {
        private static string BaseTemplate(string content) => $@"
<!DOCTYPE html>
<html lang=""en"">
<head>
  <meta charset=""UTF-8"" />
  <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"" />
  <title>TaskPilot</title>
</head>
<body style=""margin:0;padding:0;background-color:#f4f5f7;font-family:'Segoe UI',Arial,sans-serif;"">
  <table width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""background-color:#f4f5f7;padding:40px 0;"">
    <tr>
      <td align=""center"">
        <table width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""max-width:560px;"">

          <!-- Header / Logo -->
          <tr>
            <td align=""center"" style=""padding-bottom:24px;"">
              <table cellpadding=""0"" cellspacing=""0"">
                <tr>
                  <td style=""background-color:#D51C39;border-radius:12px;padding:10px 14px;display:inline-block;"">
                    <span style=""font-size:22px;font-weight:900;color:#ffffff;letter-spacing:-0.5px;"">&#10003; TaskPilot</span>
                  </td>
                </tr>
              </table>
            </td>
          </tr>

          <!-- Main Card -->
          <tr>
            <td style=""background-color:#ffffff;border-radius:20px;box-shadow:0 4px 24px rgba(0,0,0,0.08);overflow:hidden;"">
              {content}
            </td>
          </tr>

          <!-- Footer -->
          <tr>
            <td align=""center"" style=""padding:28px 20px 0;"">
              <p style=""margin:0;font-size:12px;color:#9ca3af;"">
                This email was sent by <strong style=""color:#D51C39;"">TaskPilot</strong>. If you didn't request this, you can safely ignore it.
              </p>
              <p style=""margin:8px 0 0;font-size:11px;color:#d1d5db;"">
                &copy; 2025 TaskPilot. All rights reserved.
              </p>
            </td>
          </tr>

        </table>
      </td>
    </tr>
  </table>
</body>
</html>";

        public string GenerateConfirmationEmailBody(string name, string email, string otp)
        {
            var digits = otp.Length == 6
                ? string.Join("", otp.ToCharArray().Select(c =>
                    $@"<td style=""width:44px;height:52px;background:#f8f9fa;border:2px solid #e5e7eb;border-radius:10px;text-align:center;vertical-align:middle;"">
                         <span style=""font-size:26px;font-weight:900;color:#D51C39;"">{c}</span>
                       </td>
                       <td style=""width:6px;""></td>"))
                : $@"<td style=""padding:16px 32px;background:#f8f9fa;border:2px solid #e5e7eb;border-radius:10px;text-align:center;"">
                       <span style=""font-size:28px;font-weight:900;color:#D51C39;letter-spacing:12px;"">{otp}</span>
                     </td>";

            var content = $@"
              <!-- Top accent bar -->
              <tr>
                <td style=""height:5px;background:linear-gradient(90deg,#D51C39,#121338);""></td>
              </tr>

              <!-- Body -->
              <tr>
                <td style=""padding:48px 48px 40px;"">

                  <!-- Icon -->
                  <table cellpadding=""0"" cellspacing=""0"" style=""margin-bottom:32px;"">
                    <tr>
                      <td style=""background:linear-gradient(135deg,#fef2f4,#fce7ea);border-radius:16px;padding:18px;display:inline-block;"">
                        <span style=""font-size:36px;"">✉️</span>
                      </td>
                    </tr>
                  </table>

                  <!-- Title -->
                  <h1 style=""margin:0 0 12px;font-size:26px;font-weight:800;color:#121338;letter-spacing:-0.5px;"">
                    Verify your email address
                  </h1>
                  <p style=""margin:0 0 8px;font-size:15px;color:#6b7280;line-height:1.6;"">
                    Hi <strong style=""color:#121338;"">{name}</strong>,
                  </p>
                  <p style=""margin:0 0 32px;font-size:15px;color:#6b7280;line-height:1.6;"">
                    Use the verification code below to confirm your email address and activate your <strong style=""color:#D51C39;"">TaskPilot</strong> account.
                  </p>

                  <!-- OTP Box -->
                  <table cellpadding=""0"" cellspacing=""0"" style=""margin-bottom:32px;"">
                    <tr>
                      <td style=""background:#f8f9fa;border:2px dashed #D51C39;border-radius:14px;padding:24px 36px;text-align:center;"">
                        <p style=""margin:0 0 10px;font-size:11px;font-weight:700;color:#9ca3af;letter-spacing:2px;text-transform:uppercase;"">Your verification code</p>
                        <table cellpadding=""0"" cellspacing=""0"" style=""margin:0 auto;"">
                          <tr>{digits}</tr>
                        </table>
                        <p style=""margin:12px 0 0;font-size:12px;color:#9ca3af;"">⏱ Expires in <strong>15 minutes</strong></p>
                      </td>
                    </tr>
                  </table>

                  <!-- Divider -->
                  <table cellpadding=""0"" cellspacing=""0"" width=""100%"" style=""margin-bottom:28px;"">
                    <tr>
                      <td style=""height:1px;background:#f3f4f6;""></td>
                    </tr>
                  </table>

                  <!-- Tips -->
                  <table cellpadding=""0"" cellspacing=""0"" style=""background:#fafafa;border-radius:12px;padding:20px 24px;width:100%;"">
                    <tr>
                      <td style=""font-size:13px;color:#6b7280;line-height:2;"">
                        <p style=""margin:0 0 6px;font-weight:700;color:#374151;"">💡 Quick tips:</p>
                        <p style=""margin:0;"">• Enter the code on the verification page</p>
                        <p style=""margin:0;"">• Check your spam folder if you don't see it</p>
                        <p style=""margin:0;"">• The code works only once</p>
                      </td>
                    </tr>
                  </table>

                </td>
              </tr>

              <!-- Bottom accent -->
              <tr>
                <td style=""height:4px;background:linear-gradient(90deg,#121338,#D51C39);""></td>
              </tr>";

            return BaseTemplate(content);
        }

        public string GeneratePasswordResetEmailBody(string name, string email, string otp)
        {
            var content = $@"
              <!-- Top accent bar -->
              <tr>
                <td style=""height:5px;background:linear-gradient(90deg,#D51C39,#121338);""></td>
              </tr>

              <!-- Body -->
              <tr>
                <td style=""padding:48px 48px 40px;"">

                  <!-- Icon -->
                  <table cellpadding=""0"" cellspacing=""0"" style=""margin-bottom:32px;"">
                    <tr>
                      <td style=""background:linear-gradient(135deg,#fef2f4,#fce7ea);border-radius:16px;padding:18px;display:inline-block;"">
                        <span style=""font-size:36px;"">🔐</span>
                      </td>
                    </tr>
                  </table>

                  <!-- Title -->
                  <h1 style=""margin:0 0 12px;font-size:26px;font-weight:800;color:#121338;letter-spacing:-0.5px;"">
                    Reset your password
                  </h1>
                  <p style=""margin:0 0 8px;font-size:15px;color:#6b7280;line-height:1.6;"">
                    Hi <strong style=""color:#121338;"">{name}</strong>,
                  </p>
                  <p style=""margin:0 0 32px;font-size:15px;color:#6b7280;line-height:1.6;"">
                    We received a request to reset your password. Use the code below to proceed. If you didn't make this request, you can safely ignore this email.
                  </p>

                  <!-- OTP Box -->
                  <table cellpadding=""0"" cellspacing=""0"" style=""margin-bottom:32px;width:100%;"">
                    <tr>
                      <td style=""background:#f8f9fa;border:2px dashed #D51C39;border-radius:14px;padding:24px 36px;text-align:center;"">
                        <p style=""margin:0 0 10px;font-size:11px;font-weight:700;color:#9ca3af;letter-spacing:2px;text-transform:uppercase;"">Password reset code</p>
                        <p style=""margin:0;font-size:36px;font-weight:900;color:#D51C39;letter-spacing:10px;font-family:monospace;"">{otp}</p>
                        <p style=""margin:12px 0 0;font-size:12px;color:#9ca3af;"">⏱ Expires in <strong>15 minutes</strong></p>
                      </td>
                    </tr>
                  </table>

                  <!-- Warning -->
                  <table cellpadding=""0"" cellspacing=""0"" style=""background:#fffbeb;border-left:4px solid #f59e0b;border-radius:8px;padding:16px 20px;width:100%;margin-bottom:20px;"">
                    <tr>
                      <td style=""font-size:13px;color:#92400e;line-height:1.6;"">
                        ⚠️ <strong>Security notice:</strong> Never share this code with anyone. TaskPilot staff will never ask for your code.
                      </td>
                    </tr>
                  </table>

                </td>
              </tr>

              <!-- Bottom accent -->
              <tr>
                <td style=""height:4px;background:linear-gradient(90deg,#121338,#D51C39);""></td>
              </tr>";

            return BaseTemplate(content);
        }

        public string GenerateEmployeeInvitationBody(
            string employeeName,
            string companyName,
            string invitationLink)
        {
            var content = $@"
              <!-- Top accent bar -->
              <tr>
                <td style=""height:5px;background:linear-gradient(90deg,#D51C39,#121338);""></td>
              </tr>

              <!-- Body -->
              <tr>
                <td style=""padding:48px 48px 40px;"">

                  <!-- Icon -->
                  <table cellpadding=""0"" cellspacing=""0"" style=""margin-bottom:32px;"">
                    <tr>
                      <td style=""background:linear-gradient(135deg,#eff6ff,#dbeafe);border-radius:16px;padding:18px;display:inline-block;"">
                        <span style=""font-size:36px;"">🎉</span>
                      </td>
                    </tr>
                  </table>

                  <!-- Title -->
                  <h1 style=""margin:0 0 12px;font-size:26px;font-weight:800;color:#121338;letter-spacing:-0.5px;"">
                    You're invited to join {companyName}!
                  </h1>
                  <p style=""margin:0 0 8px;font-size:15px;color:#6b7280;line-height:1.6;"">
                    Hi <strong style=""color:#121338;"">{employeeName}</strong>,
                  </p>
                  <p style=""margin:0 0 32px;font-size:15px;color:#6b7280;line-height:1.6;"">
                    You've been invited to join <strong style=""color:#D51C39;"">{companyName}</strong> on <strong>TaskPilot</strong> — the smart project management platform. Click the button below to accept your invitation and set up your account.
                  </p>

                  <!-- CTA Button -->
                  <table cellpadding=""0"" cellspacing=""0"" style=""margin-bottom:32px;"">
                    <tr>
                      <td style=""border-radius:12px;background:linear-gradient(135deg,#D51C39,#b91c3e);box-shadow:0 4px 14px rgba(213,28,57,0.35);"">
                        <a href=""{invitationLink}"" style=""display:inline-block;padding:16px 40px;font-size:16px;font-weight:700;color:#ffffff;text-decoration:none;letter-spacing:0.3px;"">
                          Accept Invitation &rarr;
                        </a>
                      </td>
                    </tr>
                  </table>

                  <!-- Info box -->
                  <table cellpadding=""0"" cellspacing=""0"" style=""background:#f0fdf4;border-left:4px solid #22c55e;border-radius:8px;padding:16px 20px;width:100%;margin-bottom:24px;"">
                    <tr>
                      <td style=""font-size:13px;color:#166534;line-height:1.7;"">
                        ✅ <strong>What happens next?</strong><br/>
                        Complete your profile, upload your CV, and start collaborating with your team on TaskPilot.
                      </td>
                    </tr>
                  </table>

                  <p style=""margin:0;font-size:13px;color:#9ca3af;"">
                    This invitation link expires in <strong>7 days</strong>. If you can't click the button, copy and paste this link:<br/>
                    <a href=""{invitationLink}"" style=""color:#D51C39;word-break:break-all;font-size:12px;"">{invitationLink}</a>
                  </p>

                </td>
              </tr>

              <!-- Bottom accent -->
              <tr>
                <td style=""height:4px;background:linear-gradient(90deg,#121338,#D51C39);""></td>
              </tr>";

            return BaseTemplate(content);
        }
    }
}