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
using eBook_Library_Service.ViewModels;
using Microsoft.AspNetCore.Authorization;


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
        private readonly EmailService _emailService;

        public PaymentController(
            PayPalService payPalService,
            StripeService stripeService,
            ShoppingCartService shoppingCartService,
            ILogger<PaymentController> logger,
            IConfiguration configuration, AppDbContext context,EmailService emailService)
        {
            _payPalService = payPalService;
            _stripeService = stripeService;
            _shoppingCartService = shoppingCartService;
            _logger = logger;
            _configuration = configuration;
            _context = context;
            _emailService = emailService;   
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
        [Authorize]
        public async Task<IActionResult> PurchaseHistory()
        {
            try
            {
                // Get the current user's ID
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                // Fetch the user's purchase history and include the Book entity
                var purchases = await _context.PurchaseHistories
                    .Include(p => p.Book) // Include the Book entity
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

        [Authorize]
        public async Task<IActionResult> PaymentSuccess(string paymentId, string token, string payerId, int? bookId)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var userEmail = User.FindFirst(ClaimTypes.Email)?.Value;

                if (bookId.HasValue)
                {
                    // Handle Buy Now scenario (single book purchase)
                    var book = await _context.Books.FindAsync(bookId.Value);
                    if (book == null)
                    {
                        TempData["ErrorMessage"] = "Book not found.";
                        return RedirectToAction("Index", "Home");
                    }

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
                        PurchaseDate = DateTime.UtcNow,
                         ImageUrl = book.ImageUrl
                    };

                    _context.PurchaseHistories.Add(purchase);
                    await _context.SaveChangesAsync();

                    // Send email notification for the single book
                    var emailSubject = "Payment Successful";
                    var emailMessage = $"Dear {User.Identity.Name},<br/><br/>" +
                                       $"Your payment for the book <strong>{book.Title}</strong> has been processed successfully.<br/>" +
                                       $"You can now access the book in your library.<br/><br/>" +
                                       $"Thank you for using eBook Library Service!<br/><br/>" +
                                       $"Best regards,<br/>" +
                                       $"eBook Library Service Team";

                    await _emailService.SendEmailAsync(userEmail, emailSubject, emailMessage);
                    _logger.LogInformation($"Payment confirmation email sent to {userEmail}.");
                }
                else
                {
                    // Handle Shopping Cart scenario (multiple books purchase)
                    var cart = await _shoppingCartService.GetCartAsync();
                    if (cart == null || !cart.Items.Any())
                    {
                        TempData["ErrorMessage"] = "Your cart is empty.";
                        return RedirectToAction("Index", "ShoppingCart");
                    }

                    // Process each item in the cart
                    foreach (var item in cart.Items)
                    {
                        var book = await _context.Books.FindAsync(item.BookId);
                        if (book == null)
                        {
                            _logger.LogWarning($"Book with ID {item.BookId} not found in the database.");
                            continue; // Skip this item and proceed with the next one
                        }

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
                            PurchaseDate = DateTime.UtcNow,
                            ImageUrl = book.ImageUrl
                        };

                        _context.PurchaseHistories.Add(purchase);
                    }

                    await _context.SaveChangesAsync();

                    // Send email notification for the entire cart
                    var emailSubject = "Payment Successful";
                    var emailMessage = $"Dear {User.Identity.Name},<br/><br/>" +
                                       $"Your payment for the following books has been processed successfully:<br/><br/>" +
                                       $"<ul>{string.Join("", cart.Items.Select(item => $"<li>{item.Book.Title}</li>"))}</ul>" +
                                       $"You can now access the books in your library.<br/><br/>" +
                                       $"Thank you for using eBook Library Service!<br/><br/>" +
                                       $"Best regards,<br/>" +
                                       $"eBook Library Service Team";

                    await _emailService.SendEmailAsync(userEmail, emailSubject, emailMessage);
                    _logger.LogInformation($"Payment confirmation email sent to {userEmail}.");

                    // Clear the cart after successful payment
                    await _shoppingCartService.ClearCartAsync();
                }

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
        [HttpGet]
        [Authorize]
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
        [Authorize]
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

        [HttpPost]
        public async Task<IActionResult> DeletePurchase(int purchaseId)
        {
            try
            {
                // Get the current user's ID
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (userId == null)
                {
                    return Unauthorized(); // User is not logged in
                }

                // Fetch the purchase record
                var purchase = await _context.PurchaseHistories
                    .FirstOrDefaultAsync(p => p.Id == purchaseId && p.UserId == userId);

                if (purchase == null)
                {
                    TempData["ErrorMessage"] = "Purchase not found or you do not have permission to delete it.";
                    return RedirectToAction("PurchaseHistory");
                }

                // Remove the purchase record
                _context.PurchaseHistories.Remove(purchase);
                await _context.SaveChangesAsync();

                TempData["Message"] = "Purchase deleted successfully.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while deleting the purchase.");
                TempData["ErrorMessage"] = "An error occurred while deleting the purchase. Please try again.";
            }

            return RedirectToAction("PurchaseHistory");
        }
        [Authorize]
        public async Task<IActionResult> BorrowSuccess(int bookId)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var userEmail = User.FindFirst(ClaimTypes.Email)?.Value;

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

                if (book.Stock > 0)
                {
                    // Reduce the book's stock
                    book.Stock -= 1;

                    // Add the borrow record to the BorrowHistory table
                    var borrowHistory = new BorrowHistory
                    {
                        UserId = userId,
                        BookId = bookId,
                        BorrowDate = DateTime.Now,
                        ReturnDate = DateTime.Now.AddDays(30) // 30-day borrowing period
                    };

                    _context.BorrowHistory.Add(borrowHistory);
                    await _context.SaveChangesAsync();

                    // Send a success email
                    var emailSubject = "Borrow Successful";
                    var emailMessage = $"Dear {User.Identity.Name},<br/><br/>" +
                                       $"You have successfully borrowed the book <strong>{book.Title}</strong>.<br/>" +
                                       $"You can now access the book in your library.<br/><br/>" +
                                       $"Thank you for using eBook Library Service!<br/><br/>" +
                                       $"Best regards,<br/>" +
                                       $"eBook Library Service Team";

                    await _emailService.SendEmailAsync(userEmail, emailSubject, emailMessage);
                    _logger.LogInformation($"Borrow confirmation email sent to {userEmail}.");

                    // Set success message
                    TempData["Message"] = $"You have successfully borrowed {book.Title}.";

                    // Redirect to the BorrowHistory page
                    return RedirectToAction("BorrowHistory", "Book");
                }
                else
                {
                    // Redirect to the JoinWaitingList action in the BookController
                    return RedirectToAction("JoinWaitingList", "Book", new { bookId = bookId });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred during the borrow success process.");
                TempData["ErrorMessage"] = "An error occurred during the borrow success process. Please contact support.";
                return RedirectToAction("UserIndex", "Book");
            }
        }
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> ProcessBorrowPayment(int bookId, string paymentMethod)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var userEmail = User.FindFirst(ClaimTypes.Email)?.Value;

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

                // Check if the user has already borrowed this book
                var isAlreadyBorrowed = await _context.BorrowHistory
                    .AnyAsync(bh => bh.UserId == userId && bh.BookId == bookId);

                if (isAlreadyBorrowed)
                {
                    TempData["ErrorMessage"] = "You have already borrowed this book.";
                    return RedirectToAction("UserIndex", "Book");
                }

                // Check if the user has already borrowed 3 books
                var borrowedBooksCount = await _context.BorrowHistory
                    .CountAsync(bh => bh.UserId == userId && bh.ReturnDate > DateTime.UtcNow);

                if (borrowedBooksCount >= 3)
                {
                    TempData["ErrorMessage"] = "You have reached the maximum borrowing limit of 3 books.";
                    return RedirectToAction("UserIndex", "Book");
                }

                // If the book is available, proceed with the payment
                if (book.Stock > 0)
                {
                    if (paymentMethod == "paypal")
                    {
                        // Handle PayPal payment
                        var returnUrl = Url.Action("BorrowSuccess", "Payment", new { bookId = bookId }, Request.Scheme);
                        var cancelUrl = Url.Action("PaymentCancel", "Payment", null, Request.Scheme);

                        var payment = _payPalService.CreatePayment(book.BorrowPrice, returnUrl, cancelUrl, "sale");
                        return Redirect(payment.GetApprovalUrl());
                    }
                    else if (paymentMethod == "stripe")
                    {
                        // Handle Stripe payment
                        var clientSecret = _stripeService.CreatePaymentIntent(book.BorrowPrice);

                        ViewBag.StripeClientSecret = clientSecret;
                        ViewBag.StripePublishableKey = _configuration["Stripe:PublishableKey"];
                        ViewBag.BookId = bookId;
                        ViewBag.IsBorrow = true; // Set this flag to indicate a borrow payment

                        return View("~/Views/ShoppingCart/StripeCheckout.cshtml");
                    }
                    else
                    {
                        TempData["ErrorMessage"] = "Invalid payment method selected.";
                        return RedirectToAction("UserIndex", "Book");
                    }
                }
                else
                {


                    TempData["ShowWaitingListPopup"] = true;
                    TempData["BookId"] = bookId;
                    TempData["BookTitle"] = book.Title;
                    return RedirectToAction("UserIndex", "Book");
                }
            }
            
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred during the borrow payment process.");
                TempData["ErrorMessage"] = "An error occurred during the borrow payment process. Please try again.";
                return RedirectToAction("UserIndex", "Book");
            }
        }

    }
}
