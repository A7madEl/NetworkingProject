using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SendGrid;
using SendGrid.Helpers.Mail;
using System.Threading.Tasks;

namespace eBook_Library_Service.Services
{
    public class EmailService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task SendEmailAsync(string toEmail, string subject, string message)
        {
            try
            {
                var apiKey = _configuration["EmailSettings:SendGridApiKey"];
                var client = new SendGridClient(apiKey);
                var from = new EmailAddress(_configuration["EmailSettings:FromEmail"], _configuration["EmailSettings:FromName"]);
                var to = new EmailAddress(toEmail);
                var msg = MailHelper.CreateSingleEmail(from, to, subject, message, message);
                var response = await client.SendEmailAsync(msg);

                // Log the response status and body
                _logger.LogInformation($"Email sent. Status: {response.StatusCode}");
                _logger.LogInformation($"Response Body: {await response.Body.ReadAsStringAsync()}");
            }
            catch (Exception ex)
            {
                // Log the exception
                _logger.LogError($"Error sending email: {ex.Message}");
            }
        }
    }
}