using eBook_Library_Service.Data;
using eBook_Library_Service.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

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
        _timer = new Timer(DoWork, null, TimeSpan.Zero, TimeSpan.FromSeconds(10)); // Check every 10 seconds
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
                try
                {
                    // Update book stock
                    var book = dbContext.Books.Find(borrow.BookId);
                    if (book != null)
                    {
                        book.Stock += 1;
                        _logger.LogInformation($"Increased stock for book ID {borrow.BookId}.");
                    }

                    // Remove the borrow record
                    dbContext.BorrowHistory.Remove(borrow);
                    _logger.LogInformation($"Removed borrow record ID {borrow.BorrowId}.");

                    // Notify the next user in the waiting list
                    var waitingList = dbContext.WaitingLists
                        .Where(w => w.BookId == borrow.BookId)
                        .OrderBy(w => w.Position)
                        .ToList();

                    if (waitingList.Any())
                    {
                        var nextUser = waitingList.First();

                        // Assign the book to the next user in the waiting list
                        var newBorrow = new BorrowHistory
                        {
                            UserId = nextUser.UserId,
                            BookId = nextUser.BookId,
                            BorrowDate = DateTime.UtcNow,
                            ReturnDate = DateTime.UtcNow.AddMinutes(10) // Set the return date (e.g., 10 minutes from now)
                        };

                        dbContext.BorrowHistory.Add(newBorrow);
                        _logger.LogInformation($"Assigned book ID {borrow.BookId} to user {nextUser.UserId}.");

                        // Remove the user from the waiting list
                        dbContext.WaitingLists.Remove(nextUser);
                        _logger.LogInformation($"Removed user {nextUser.UserId} from the waiting list.");

                        // Update positions of remaining users
                        foreach (var entry in waitingList.Skip(1))
                        {
                            entry.Position -= 1;
                        }
                    }

                    dbContext.SaveChanges();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error processing borrow record ID {borrow.BorrowId}.");
                }
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