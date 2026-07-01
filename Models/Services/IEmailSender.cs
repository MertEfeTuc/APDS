namespace APDS.Services.Notifications
{
    public interface IEmailSender
    {
        Task SendAsync(string toEmail, string subject, string body);
    }

    public class SmtpSettings
    {
        public string Host { get; set; } = "";
        public int Port { get; set; }
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";
        public string FromAddress { get; set; } = "";
        public bool EnableSsl { get; set; } = true;
    }
}