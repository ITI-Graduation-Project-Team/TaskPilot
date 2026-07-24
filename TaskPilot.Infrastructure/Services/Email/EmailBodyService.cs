using TaskPilot.Services.Interfaces.External;

namespace TaskPilot.Infrastructure.Services.Email
{
    public class EmailBodyService : IEmailBodyService
    {
        // Set to a publicly reachable HTTPS PNG (transparent bg, ~300px wide) once hosted.
        // Example: "https://taskpilot.runasp.net/TaskPilotLogo.png"
        private const string LogoUrl = "";

        private const string Primary       = "#3B5BDB";
        private const string PrimaryDark   = "#2F49B0";
        private const string PrimaryLight  = "#EEF2FF";
        private const string Ink           = "#0F172A";
        private const string Body          = "#334155";
        private const string Muted         = "#64748B";
        private const string Subtle        = "#94A3B8";
        private const string Border        = "#E2E8F0";
        private const string Surface       = "#FFFFFF";
        private const string Canvas        = "#F1F5F9";
        private const string WarnBg        = "#FFF8E1";
        private const string WarnBorder    = "#F5D680";
        private const string WarnText      = "#7A5A00";
        private const string DangerBg      = "#FEF2F2";
        private const string DangerBorder  = "#FCA5A5";
        private const string DangerText    = "#991B1B";

        private static string BaseTemplate(string preheader, string content) => $@"<!DOCTYPE html PUBLIC ""-//W3C//DTD XHTML 1.0 Transitional//EN"" ""http://www.w3.org/TR/xhtml1/DTD/xhtml1-transitional.dtd"">
<html xmlns=""http://www.w3.org/1999/xhtml"" lang=""en"">
<head>
  <meta charset=""utf-8"" />
  <meta name=""viewport"" content=""width=device-width, initial-scale=1"" />
  <meta http-equiv=""X-UA-Compatible"" content=""IE=edge"" />
  <meta name=""color-scheme"" content=""light"" />
  <meta name=""supported-color-schemes"" content=""light"" />
  <title>TaskPilot</title>
  <!--[if mso]>
  <style type=""text/css"">
    table, td, div, p, a {{ font-family: Arial, Helvetica, sans-serif !important; }}
  </style>
  <![endif]-->
  <style>
    a {{ text-decoration: none; }}
    @media only screen and (max-width: 620px) {{
      .container {{ width: 100% !important; }}
      .px {{ padding-left: 24px !important; padding-right: 24px !important; }}
      .otp {{ font-size: 30px !important; letter-spacing: 8px !important; }}
      .h1 {{ font-size: 24px !important; line-height: 32px !important; }}
      .btn a {{ display: block !important; width: 100% !important; box-sizing: border-box !important; }}
    }}
  </style>
</head>
<body style=""margin:0;padding:0;background:{Canvas};"">
  <div style=""display:none;font-size:1px;color:{Canvas};line-height:1px;max-height:0;max-width:0;opacity:0;overflow:hidden;"">
    {preheader}
  </div>
  <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""background:{Canvas};"">
    <tr>
      <td align=""center"" style=""padding:32px 16px;"">
        <table role=""presentation"" class=""container"" width=""600"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""width:600px;max-width:600px;background:{Surface};border:1px solid {Border};border-radius:14px;overflow:hidden;box-shadow:0 1px 2px rgba(15,23,42,0.04);"">
          <tr><td style=""height:4px;background:{Primary};line-height:4px;font-size:0;"">&nbsp;</td></tr>
          <tr>
            <td class=""px"" style=""padding:28px 40px 8px 40px;"">
              {HeaderLogo()}
            </td>
          </tr>
          <tr>
            <td class=""px"" style=""padding:8px 40px 32px 40px;font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,Helvetica,Arial,sans-serif;color:{Body};font-size:15px;line-height:24px;"">
              {content}
            </td>
          </tr>
          <tr><td style=""height:1px;background:{Border};line-height:1px;font-size:0;"">&nbsp;</td></tr>
          <tr>
            <td class=""px"" style=""padding:20px 40px 24px 40px;font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,Helvetica,Arial,sans-serif;"">
              <p style=""margin:0 0 6px 0;color:{Muted};font-size:12px;line-height:18px;"">
                This email was sent by <strong style=""color:{Ink};"">TaskPilot</strong>. If you didn't request this, you can safely ignore it.
              </p>
              <p style=""margin:0;color:{Subtle};font-size:12px;line-height:18px;"">
                © 2025 TaskPilot, Inc. &nbsp;·&nbsp;
                <a href=""#"" style=""color:{Subtle};text-decoration:underline;"">Privacy</a> &nbsp;·&nbsp;
                <a href=""#"" style=""color:{Subtle};text-decoration:underline;"">Help Center</a>
              </p>
            </td>
          </tr>
        </table>
        <div style=""height:24px;line-height:24px;font-size:24px;"">&nbsp;</div>
      </td>
    </tr>
  </table>
</body>
</html>";

        private static string HeaderLogo()
        {
            if (!string.IsNullOrWhiteSpace(LogoUrl))
            {
                return $@"<img src=""{LogoUrl}"" alt=""TaskPilot"" width=""140"" style=""display:block;border:0;outline:none;max-width:140px;height:auto;"" />";
            }

            return $@"<table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"">
              <tr>
                <td style=""background:{Primary};width:36px;height:36px;border-radius:9px;text-align:center;vertical-align:middle;color:#ffffff;font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,Helvetica,Arial,sans-serif;font-size:18px;font-weight:700;line-height:36px;"">TP</td>
                <td style=""padding-left:12px;font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,Helvetica,Arial,sans-serif;font-size:18px;font-weight:700;color:{Ink};letter-spacing:-0.2px;"">TaskPilot</td>
              </tr>
            </table>";
        }

        private static string EyebrowBadge(string label) => $@"
              <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"">
                <tr>
                  <td style=""background:{PrimaryLight};color:{Primary};font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,Helvetica,Arial,sans-serif;font-size:11px;font-weight:700;letter-spacing:1.2px;text-transform:uppercase;padding:6px 12px;border-radius:999px;"">
                    {label}
                  </td>
                </tr>
              </table>";

        private static string H1(string text) => $@"
              <h1 class=""h1"" style=""margin:16px 0 12px 0;font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,Helvetica,Arial,sans-serif;font-size:28px;line-height:36px;font-weight:700;color:{Ink};letter-spacing:-0.4px;"">
                {text}
              </h1>";

        private static string Paragraph(string text) => $@"
              <p style=""margin:0 0 16px 0;font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,Helvetica,Arial,sans-serif;font-size:15px;line-height:24px;color:{Body};"">
                {text}
              </p>";

        private static string OtpBlock(string label, string otp, string expiry = "15 minutes") => $@"
              <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""margin:8px 0 24px 0;"">
                <tr>
                  <td align=""center"" style=""background:{PrimaryLight};border:1px solid #DBE3FE;border-radius:12px;padding:24px 20px;"">
                    <p style=""margin:0 0 10px 0;font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,Helvetica,Arial,sans-serif;font-size:12px;font-weight:600;letter-spacing:1px;text-transform:uppercase;color:{Primary};"">
                      {label}
                    </p>
                    <p class=""otp"" style=""margin:0 0 12px 0;font-family:'SF Mono',Menlo,Consolas,'Courier New',monospace;font-size:36px;line-height:44px;font-weight:700;letter-spacing:12px;color:{Ink};"">
                      {otp}
                    </p>
                    <p style=""margin:0;font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,Helvetica,Arial,sans-serif;font-size:13px;color:{Muted};"">
                      Expires in {expiry}
                    </p>
                  </td>
                </tr>
              </table>";

        private static string Button(string href, string label) => $@"
              <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"" class=""btn"" style=""margin:8px 0 24px 0;"">
                <tr>
                  <td align=""center"" style=""border-radius:10px;background:{Primary};"">
                    <a href=""{href}"" target=""_blank"" style=""display:inline-block;padding:14px 28px;font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,Helvetica,Arial,sans-serif;font-size:15px;font-weight:600;color:#ffffff;background:{Primary};border-radius:10px;border:1px solid {PrimaryDark};"">
                      {label}
                    </a>
                  </td>
                </tr>
              </table>";

        private static string Step(int number, string title, string desc) => $@"
                <tr>
                  <td style=""padding:0 0 14px 0;"">
                    <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"" width=""100%"">
                      <tr>
                        <td width=""32"" valign=""top"" style=""width:32px;"">
                          <div style=""width:28px;height:28px;line-height:28px;text-align:center;background:{Primary};color:#ffffff;font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,Helvetica,Arial,sans-serif;font-size:13px;font-weight:700;border-radius:50%;"">
                            {number}
                          </div>
                        </td>
                        <td valign=""top"" style=""padding-left:14px;font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,Helvetica,Arial,sans-serif;"">
                          <div style=""font-size:14px;font-weight:600;color:{Ink};line-height:20px;"">{title}</div>
                          <div style=""font-size:13px;color:{Muted};line-height:20px;margin-top:2px;"">{desc}</div>
                        </td>
                      </tr>
                    </table>
                  </td>
                </tr>";

        private static string StepsSection(string heading, string stepsHtml) => $@"
              <div style=""margin:8px 0 20px 0;padding:20px 20px 8px 20px;background:#F8FAFC;border:1px solid {Border};border-radius:12px;"">
                <p style=""margin:0 0 14px 0;font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,Helvetica,Arial,sans-serif;font-size:13px;font-weight:700;color:{Ink};text-transform:uppercase;letter-spacing:0.6px;"">
                  {heading}
                </p>
                <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0"">
                  {stepsHtml}
                </table>
              </div>";

        private static string Notice(string bg, string border, string text, string message) => $@"
              <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""margin:8px 0 20px 0;"">
                <tr>
                  <td style=""background:{bg};border:1px solid {border};border-left:3px solid {border};border-radius:8px;padding:14px 16px;font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,Helvetica,Arial,sans-serif;font-size:13px;line-height:20px;color:{text};"">
                    {message}
                  </td>
                </tr>
              </table>";

        private static string Divider() => $@"
              <div style=""height:1px;background:{Border};line-height:1px;font-size:0;margin:20px 0;"">&nbsp;</div>";

        public string GenerateConfirmationEmailBody(string name, string email, string otp)
        {
            var preheader = $"Your TaskPilot verification code is {otp}. It expires in 15 minutes.";

            var steps =
                Step(1, "Return to the signup page", "Open the tab or screen where you started signing up.") +
                Step(2, "Enter the 6-digit code", "Type the code exactly as shown above.") +
                Step(3, "You're in", "Your TaskPilot account will be activated instantly.");

            var content =
                EyebrowBadge("Verify your email") +
                H1("Confirm your email address") +
                Paragraph($"Hi <strong style=\"color:{Ink};\">{name}</strong>,") +
                Paragraph("Welcome to TaskPilot. Use the verification code below to confirm your email address and activate your account.") +
                OtpBlock("Verification code", otp) +
                StepsSection("How to verify", steps) +
                Divider() +
                Paragraph($"<span style=\"color:{Muted};font-size:13px;\">Didn't create a TaskPilot account? You can safely ignore this email — no account will be created.</span>");

            return BaseTemplate(preheader, content);
        }

        public string GeneratePasswordResetEmailBody(string name, string email, string otp)
        {
            var preheader = $"Your TaskPilot password reset code is {otp}. It expires in 15 minutes.";

            var content =
                EyebrowBadge("Security") +
                H1("Reset your password") +
                Paragraph($"Hi <strong style=\"color:{Ink};\">{name}</strong>,") +
                Paragraph("We received a request to reset the password for your TaskPilot account. Use the code below to continue. If you didn't make this request, you can safely ignore this email.") +
                OtpBlock("Password reset code", otp) +
                Notice(WarnBg, WarnBorder, WarnText,
                    "<strong>Security notice:</strong> Never share this code with anyone. TaskPilot staff will never ask you for it.") +
                Notice(DangerBg, DangerBorder, DangerText,
                    "<strong>Not you?</strong> Someone may be trying to access your account. We recommend changing your password and reviewing your recent activity immediately.");

            return BaseTemplate(preheader, content);
        }

        public string GenerateEmployeeInvitationBody(
            string employeeName,
            string companyName,
            string invitationLink)
        {
            var preheader = $"{companyName} invited you to collaborate on TaskPilot.";

            var steps =
                Step(1, "Accept the invitation", "Click the button above to open your secure setup page.") +
                Step(2, "Complete your profile", "Add your photo, role, and contact details.") +
                Step(3, "Start collaborating", "Jump into your team's projects, tasks, and boards.");

            var content =
                EyebrowBadge("Team Invitation") +
                H1($"You're invited to join {companyName}") +
                Paragraph($"Hi <strong style=\"color:{Ink};\">{employeeName}</strong>,") +
                Paragraph($"<strong style=\"color:{Ink};\">{companyName}</strong> has invited you to collaborate on <strong style=\"color:{Ink};\">TaskPilot</strong> — a modern project management platform for teams that ship. Accept your invitation to set up your account and get started.") +
                Button(invitationLink, "Accept invitation") +
                StepsSection("What happens next", steps) +
                Notice(PrimaryLight, "#DBE3FE", PrimaryDark,
                    $"This invitation expires in <strong>7 days</strong>. If the button above doesn't work, copy and paste this link into your browser:<br/><a href=\"{invitationLink}\" style=\"color:{Primary};word-break:break-all;text-decoration:underline;\">{invitationLink}</a>");

            return BaseTemplate(preheader, content);
        }
    }
}
