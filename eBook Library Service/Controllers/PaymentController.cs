using Microsoft.AspNetCore.Mvc;
using eBook_Library_Service.Services;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using eBook_Library_Service.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using eBook_Library_Service.Data;
using PayPal.Api;

namespace eBook_Library_Service.Controllers
{
    public class PaymentController : Controller
    {
        private readonly PayPalService _payPalService;
        private readonly StripeService _stripeService;
        private readonly ShoppingCartService _shoppingCartService;
        private readonly ILogger<PaymentController> _logger;
        private readonly IConfiguration _configuration;
        private readonly AppDbContext _context;

        public PaymentController(
            PayPalService payPalService,
            StripeService stripeService,
            ShoppingCartService shoppingCartService,
            ILogger<PaymentController> logger,
            IConfiguration configuration, AppDbContext context)
        {
            _payPalService = payPalService;
            _stripeService = stripeService;
            _shoppingCartService = shoppingCartService;
            _logger = logger;
            _configuration = configuration;
            _context = context;
        }

        // PayPal Checkout for Purchases
        [HttpPost]
        public async Task<IActionResult> Checkout()
        {
            try
            {
                // Get the current user's shopping cart
                var cart = await _shoppingCartService.GetCartAsync();
                if (cart == null || !cart.Items.Any())
                {
                    return RedirectToAction("Index", "ShoppingCart");
                }

                // Define return and cancel URLs
                var returnUrl = Url.Action("PaymentSuccess", "Payment", null, Request.Scheme);
                var cancelUrl = Url.Action("PaymentCancel", "Payment", null, Request.Scheme);

                // Create a PayPal payment
                var payment = _payPalService.CreatePayment(cart.TotalPrice, returnUrl, cancelUrl, "sale");

                // Redirect to PayPal for payment approval
                return Redirect(payment.GetApprovalUrl());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred during PayPal checkout.");
                TempData["ErrorMessage"] = "An error occurred during checkout. Please try again.";
                return RedirectToAction("Index", "ShoppingCart");
            }
        }

        // Stripe Checkout for Purchases
        [HttpPost]
        public async Task<IActionResult> CheckoutWithStripe()
        {
            try
            {
                // Get the current user's shopping cart
                var cart = await _shoppingCartService.GetCartAsync();
                if (cart == null || !cart.Items.Any())
                {
                    return RedirectToAction("Index", "ShoppingCart");
                }

                // Create a Stripe payment intent
                var clientSecret = _stripeService.CreatePaymentIntent(cart.TotalPrice);

                // Pass the client secret and publishable key to the view
                ViewBag.StripeClientSecret = clientSecret;
                ViewBag.StripePublishableKey = _configuration["Stripe:PublishableKey"];

                return View("~/Views/ShoppingCart/StripeCheckout.cshtml");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred during Stripe checkout.");
                TempData["ErrorMessage"] = "An error occurred during checkout. Please try again.";
                return RedirectToAction("Index", "ShoppingCart");
            }
        }

        // Fake Credit Card Checkout for Purchases
        [HttpPost]
        public async Task<IActionResult> FakeCheckoutWithCreditCard()
        {
            try
            {
                // Get the current user's shopping cart
                var cart = await _shoppingCartService.GetCartAsync();
                if (cart == null || !cart.Items.Any())
                {
                    return RedirectToAction("Index", "ShoppingCart");
                }

                // Simulate a successful payment
                TempData["Message"] = "Payment successful! (Fake Credit Card)";
                await _shoppingCartService.ClearCartAsync(); // Clear the cart after successful payment
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred during fake credit card checkout.");
                TempData["ErrorMessage"] = "An error occurred during checkout. Please try again.";
            }

            return RedirectToAction("UserIndex", "Book");
        }
        public async Task<IActionResult> PurchaseHistory()
        {
            try
            {
                // Get the current user's ID
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                // Fetch the user's purchase history
                var purchases = await _context.PurchaseHistories
                    .Where(p => p.UserId == userId)
                    .OrderByDescending(p => p.PurchaseDate)
                    .ToListAsync();

                // Explicitly specify the view path
                return View("~/Views/ShoppingCart/PurchaseHistory.cshtml", purchases);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while fetching purchase history.");
                TempData["ErrorMessage"] = "An error occurred while fetching your purchase history. Please try again.";
                return RedirectToAction("PurchaseHistory", "ShoppingCart");
            }
        }
        // Payment Success for Purchases
        public async Task<IActionResult> PaymentSuccess(string paymentId, string token, string payerId, int bookId)
        {
            try
            {
                // Fetch the book details
                var book = await _context.Books.FindAsync(bookId);
                if (book == null)
                {
                    TempData["ErrorMessage"] = "Book not found.";
                    return RedirectToAction("Index", "Home");
                }

                // Get the current user's ID
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                // Save the purchase to the PurchaseHistory table
                var purchase = new PurchaseHistory
                {
                    UserId = userId,
                    BookId = book.BookId,
                    BookTitle = book.Title,
                    Publisher = book.Publisher,
                    Description = book.Description,
                    YearPublished = book.YearPublished,
                    Price = book.BuyPrice,
                    PurchaseDate = DateTime.UtcNow
                };

                _context.PurchaseHistories.Add(purchase);
                await _context.SaveChangesAsync();

                // Set success message
                TempData["SuccessMessage"] = "Payment successful! Your purchase history has been updated.";

                // Redirect to the Home page
                return RedirectToAction("Index", "Home");
            }
            catch (Exception ex)
            {
                // Log the error and set an error message
                _logger.LogError(ex, "An error occurred during payment execution.");
                TempData["ErrorMessage"] = "An error occurred during payment execution. Please contact support.";

                // Redirect to the Home page
                return RedirectToAction("Index", "Home");
            }
        }

        // Payment Cancel for Purchases
        public IActionResult PaymentCancel()
        {
            TempData["Message"] = "Payment was canceled.";
            return RedirectToAction("Index", "Home");
        }

        public async Task<IActionResult> BuyNow(int bookId)
        {
            // Fetch the book details
            var book = await _context.Books
                .Include(b => b.BookAuthors)
                .ThenInclude(ba => ba.Author)
                .FirstOrDefaultAsync(b => b.BookId == bookId);

            if (book == null)
            {
                return NotFound(); // Handle the case where the book is not found
            }
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId == null)
            {
                return Unauthorized(); // User is not logged in
            }
            var isAlreadyBought = await _context.PurchaseHistories
       .AnyAsync(wl => wl.UserId == userId && wl.BookId == bookId);

            if (isAlreadyBought)
            {
                TempData["ErrorMessage"] = "You have already bought this book.";
                return RedirectToAction("UserIndex", "Book");
            }
            // Return the BuyNow view located in the Views/ShoppingCart folder
            return View("~/Views/ShoppingCart/BuyNow.cshtml", book);
        }
        [HttpPost]
        public async Task<IActionResult> ProcessCreditCardPayment(int bookId)
        {
            // Fetch the book details
            var book = await _context.Books.FindAsync(bookId);
            if (book == null)
            {
                return NotFound();
            }

            // Create a Stripe payment intent
            var clientSecret = _stripeService.CreatePaymentIntent(book.BuyPrice);

            // Pass the client secret and publishable key to the view
            ViewBag.StripeClientSecret = clientSecret;
            ViewBag.StripePublishableKey = _configuration["Stripe:PublishableKey"];
            ViewBag.BookId = bookId; // Pass the book ID to the view

            // Return the Stripe Checkout view
            return View("~/Views/ShoppingCart/StripeCheckout.cshtml");
        }

        

        // Process PayPal Payment for Purchases
        [HttpPost]
        public async Task<IActionResult> ProcessPayPalPayment(int bookId)
        {
            // Fetch the book details
            var book = await _context.Books.FindAsync(bookId);
            if (book == null)
            {
                return NotFound();
            }

            // Define return and cancel URLs
            var returnUrl = Url.Action("PaymentSuccess", "Payment", new { bookId = bookId }, Request.Scheme);
            var cancelUrl = Url.Action("PaymentCancel", "Payment", null, Request.Scheme);

            // Create a PayPal payment for the buy price
            var payment = _payPalService.CreatePayment(book.BuyPrice, returnUrl, cancelUrl, "sale");

            // Redirect to PayPal for payment approval
            return Redirect(payment.GetApprovalUrl());
        }

        public async Task<IActionResult> BorrowNow(int bookId)
        {
            // Get the current user's ID
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId == null)
            {
                return Unauthorized(); // User is not logged in
            }

            // Fetch the book details
            var book = await _context.Books.FindAsync(bookId);
            if (book == null)
            {
                TempData["ErrorMessage"] = "Book not found.";
                return RedirectToAction("UserIndex", "Book");
            }

            // Check if this is a borrow request or a waiting list request
            bool isBorrow = TempData["IsBorrow"] as bool? ?? false;

            if (isBorrow)
            {
                // Borrow the book
                if (book.Stock > 0)
                {
                    book.Stock -= 1; // Reduce the book's stock
                    var borrowHistory = new BorrowHistory
                    {
                        UserId = userId,
                        BookId = bookId,
                        BorrowDate = DateTime.UtcNow,
                        ReturnDate = DateTime.UtcNow.AddDays(30) // 30-day borrowing period
                    };
                    _context.BorrowHistory.Add(borrowHistory);
                    await _context.SaveChangesAsync();

                    TempData["Message"] = $"You have successfully borrowed {book.Title}.";
                    return RedirectToAction("BorrowHistory", "Book");
                }
                else
                {
                    TempData["ErrorMessage"] = "The book is no longer available for borrowing.";
                    return RedirectToAction("UserIndex", "Book");
                }
            }
            else
            {
                // Add the user to the waiting list
                var position = await _context.WaitingLists
                    .CountAsync(wl => wl.BookId == bookId) + 1;

                var waitingListEntry = new WaitingList
                {
                    UserId = userId,
                    BookId = bookId,
                    JoinDate = DateTime.UtcNow,
                    Position = position
                };
                _context.WaitingLists.Add(waitingListEntry);
                await _context.SaveChangesAsync();

                TempData["Message"] = $"You have been added to the waiting list for {book.Title}. Your position is {position}.";
                return RedirectToAction("BorrowedRequests", "Book");
            }
        }
        // Process PayPal Payment for Borrowing
        [HttpPost]
        public async Task<IActionResult> ProcessPayPalBorrow(int bookId)
        {
            // Fetch the book details
            var book = await _context.Books.FindAsync(bookId);
            if (book == null)
            {
                return NotFound();
            }

            // Define return and cancel URLs
            var returnUrl = Url.Action("BorrowSuccess", "Payment", new { bookId = bookId }, Request.Scheme);
            var cancelUrl = Url.Action("BorrowCancel", "Payment", null, Request.Scheme);

            // Create a PayPal payment for the borrow price
            var payment = _payPalService.CreatePayment(book.BorrowPrice, returnUrl, cancelUrl, "sale");

            // Redirect to PayPal for payment approval
            return Redirect(payment.GetApprovalUrl());
        }

        // Process Stripe Payment for Borrowing
        [HttpPost]
        public async Task<IActionResult> ProcessStripeBorrow(int bookId)
        {
            // Fetch the book details
            var book = await _context.Books.FindAsync(bookId);
            if (book == null)
            {
                return NotFound();
            }

            // Create a Stripe payment intent for the borrow price
            var clientSecret = _stripeService.CreatePaymentIntent(book.BorrowPrice);

            // Pass the client secret and publishable key to the view
            ViewBag.StripeClientSecret = clientSecret;
            ViewBag.StripePublishableKey = _configuration["Stripe:PublishableKey"];
            ViewBag.BookId = bookId; // Pass the book ID to the view

            // Return the Stripe Checkout view
            return View("~/Views/ShoppingCart/StripeCheckout.cshtml");
        }

        // Process Fake Credit Card Payment for Borrowing
        [HttpPost]
        public async Task<IActionResult> ProcessFakeCreditCardBorrow(int bookId)
        {
            try
            {
                // Fetch the book details
                var book = await _context.Books.FindAsync(bookId);
                if (book == null)
                {
                    return NotFound();
                }

                // Simulate a successful borrow payment
                await BorrowBookAsync(bookId);

                TempData["Message"] = "Borrow successful! (Fake Credit Card)";
                return RedirectToAction("UserIndex", "Book");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred during fake credit card borrow.");
                TempData["ErrorMessage"] = "An error occurred during borrow. Please try again.";
                return RedirectToAction("UserIndex", "Book");
            }
        }

        // Borrow Success for Borrowing
        public async Task<IActionResult> BorrowSuccess(int bookId, bool isBorrow)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId == null)
            {
                return Unauthorized(); // User is not logged in
            }

            // Fetch the book details
            var book = await _context.Books.FindAsync(bookId);
            if (book == null)
            {
                TempData["ErrorMessage"] = "Book not found.";
                return RedirectToAction("Index", "Home");
            }

            if (isBorrow)
            {
                // Borrow the book
                await BorrowBookAsync(userId, bookId);
                TempData["SuccessMessage"] = "Borrow successful! Your borrow history has been updated.";
            }
            else
            {
                // Add the user to the waiting list
                var position = await AddToWaitingListAsync(userId, bookId);
                TempData["SuccessMessage"] = $"You have been added to the waiting list. Your position is {position}.";
            }

            return RedirectToAction("UserIndex", "Book");
        }

        // Borrow Cancel for Borrowing
        public IActionResult BorrowCancel()
        {
            TempData["Message"] = "Borrow was canceled.";
            return RedirectToAction("Index", "Home");
        }

        // Helper Method to Borrow a Book
        private async Task BorrowBookAsync(int bookId)
        {
            // Fetch the book details
            var book = await _context.Books.FindAsync(bookId);
            if (book == null)
            {
                throw new InvalidOperationException("Book not found.");
            }

            // Reduce the book's stock
            if (book.Stock > 0)
            {
                book.Stock -= 1;
            }
            else
            {
                throw new InvalidOperationException("The book is out of stock.");
            }

            // Get the current user's ID
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            // Add a record to the BorrowHistory table
            var borrow = new BorrowHistory
            {
                UserId = userId,
                BookId = book.BookId,
                BorrowDate = DateTime.UtcNow,
                ReturnDate = DateTime.UtcNow.AddDays(30) // 30-day borrowing period
            };

            _context.BorrowHistory.Add(borrow);
            await _context.SaveChangesAsync();
        }
        private async Task BorrowBookAsync(string userId, int bookId)
        {
            // Fetch the book details
            var book = await _context.Books.FindAsync(bookId);
            if (book == null)
            {
                throw new InvalidOperationException("Book not found.");
            }

            // Reduce the book's stock
            if (book.Stock > 0)
            {
                book.Stock -= 1;
            }
            else
            {
                throw new InvalidOperationException("The book is out of stock.");
            }

            // Add a record to the BorrowHistory table
            var borrowHistory = new BorrowHistory
            {
                UserId = userId,
                BookId = bookId,
                BorrowDate = DateTime.UtcNow,
                ReturnDate = DateTime.UtcNow.AddDays(30) // 30-day borrowing period
            };

            _context.BorrowHistory.Add(borrowHistory);
            await _context.SaveChangesAsync();
        }
        private async Task<int> AddToWaitingListAsync(string userId, int bookId)
        {
            // Calculate the user's position in the waiting list
            var position = await _context.WaitingLists
                .CountAsync(wl => wl.BookId == bookId) + 1;

            // Add the user to the waiting list
            var waitingListEntry = new WaitingList
            {
                UserId = userId,
                BookId = bookId,
                JoinDate = DateTime.UtcNow,
                Position = position
            };

            _context.WaitingLists.Add(waitingListEntry);
            await _context.SaveChangesAsync();

            return position; // Return the user's position in the waiting list
        }
    }
}