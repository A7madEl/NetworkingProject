// Countdown timer for each book
function startCountdown(bookId, discountEndDate) {
    var countdownDate = new Date(discountEndDate).getTime();

    var x = setInterval(function () {
        var now = new Date().getTime();
        var distance = countdownDate - now;

        var days = Math.floor(distance / (1000 * 60 * 60 * 24));
        var hours = Math.floor((distance % (1000 * 60 * 60 * 24)) / (1000 * 60 * 60));
        var minutes = Math.floor((distance % (1000 * 60 * 60)) / (1000 * 60));
        var seconds = Math.floor((distance % (1000 * 60)) / 1000);

        // Display the countdown
        document.getElementById("countdown-" + bookId).innerHTML = days + "d " + hours + "h "
            + minutes + "m " + seconds + "s ";

        // If the countdown is finished
        if (distance < 0) {
            clearInterval(x);
            document.getElementById("countdown-" + bookId).innerHTML = "EXPIRED";
        }
    }, 1000);
}

// Start the countdown for each book
document.addEventListener("DOMContentLoaded", function () {
    const countdownElements = document.querySelectorAll("[id^='countdown-']");
    countdownElements.forEach(element => {
        const bookId = element.id.split("-")[1];
        const discountEndDate = element.getAttribute("data-end-date");
        if (discountEndDate) {
            startCountdown(bookId, discountEndDate);
        }
    });
});

// Dynamic Search
document.addEventListener("DOMContentLoaded", function () {
    const searchInput = document.getElementById("searchQuery");

    if (searchInput) {
        searchInput.addEventListener("input", function () {
            const searchQuery = this.value;

            // Send an AJAX request to the server
            fetch(`/Book/UserIndex?searchQuery=${encodeURIComponent(searchQuery)}`, {
                headers: {
                    "X-Requested-With": "XMLHttpRequest" // Indicate this is an AJAX request
                }
            })
                .then(response => response.text())
                .then(data => {
                    // Update the book list with the new data
                    document.getElementById("bookList").innerHTML = data;
                })
                .catch(error => {
                    console.error("Error fetching search results:", error);
                });
        });
    }
});