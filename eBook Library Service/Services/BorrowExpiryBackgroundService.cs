using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;
using eBook_Library_Service.Data;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;

public class BorrowExpiryBackgroundService : IHostedService, IDisposable
{
    private Timer _timer;
    private readonly IServiceProvider _services;
    private readonly ILogger<BorrowExpiryBackgroundService> _logger;

    public BorrowExpiryBackgroundService(IServiceProvider services, ILogger<BorrowExpiryBackgroundService> logger)
    {
        _services = services;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Borrow Expiry Background Service is starting.");
        _timer = new Timer(DoWork, null, TimeSpan.Zero, TimeSpan.FromMinutes(10)); // Run every 10 minutes
        return Task.CompletedTask;
    }

    private void DoWork(object state)
    {
        _logger.LogInformation("Borrow Expiry Background Service is working.");
        using (var scope = _services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            // Find expired borrows
            var expiredBorrows = dbContext.BorrowHistory
                .Where(b => b.ReturnDate <= DateTime.UtcNow && b.ReturnDate != DateTime.MinValue)
                .ToList();

            foreach (var borrow in expiredBorrows)
            {
                // Update book stock
                var book = dbContext.Books.Find(borrow.BookId);
                if (book != null)
                {
                    book.Stock += 1;
                }

                // Remove the borrow record
                dbContext.BorrowHistory.Remove(borrow);

                // Notify the next user in the waiting list
                var waitingList = dbContext.WaitingLists
                    .Where(w => w.BookId == borrow.BookId)
                    .OrderBy(w => w.Position)
                    .ToList();

                if (waitingList.Any())
                {
                    var nextUser = waitingList.First();
                    dbContext.WaitingLists.Remove(nextUser);

                    // Notify the next user (e.g., send an email or update their account)
                    _logger.LogInformation($"Notifying user {nextUser.UserId} that the book {borrow.BookId} is available.");

                    // Update positions of remaining users
                    foreach (var entry in waitingList.Skip(1))
                    {
                        entry.Position -= 1;
                    }
                }

                dbContext.SaveChanges();
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Borrow Expiry Background Service is stopping.");
        _timer?.Change(Timeout.Infinite, 0);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _timer?.Dispose();
    }
}