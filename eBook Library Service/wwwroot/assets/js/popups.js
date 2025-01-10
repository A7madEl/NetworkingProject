// Get the popups and buttons
const borrowingLimitPopup = document.getElementById("borrowingLimitPopup");
const waitingListPopup = document.getElementById("waitingListPopup");
const errorPopup = document.getElementById("errorPopup");

const closeBorrowingLimitBtn = document.getElementById("closeBorrowingLimitBtn");
const joinWaitingListBtn = document.getElementById("joinWaitingListBtn");
const cancelWaitingListBtn = document.getElementById("cancelWaitingListBtn");
const closeErrorPopupBtn = document.getElementById("closeErrorPopupBtn");

const closeBtns = document.querySelectorAll(".close");

// Function to show the borrowing limit popup
function showBorrowingLimitPopup() {
    borrowingLimitPopup.style.display = "block";
}

// Function to show the waiting list popup
function showWaitingListPopup() {
    waitingListPopup.style.display = "block";
}

// Function to show the error popup
function showErrorPopup(message) {
    document.getElementById("errorMessage").innerText = message;
    errorPopup.style.display = "block";
}

// Close the borrowing limit popup
closeBorrowingLimitBtn.onclick = function () {
    borrowingLimitPopup.style.display = "none";
};

// Handle joining the waiting list
joinWaitingListBtn.onclick = function () {
    const bookId = TempData["BookId"]; // Get the book ID from TempData
    if (bookId) {
        window.location.href = `/Book/JoinWaitingList?bookId=${bookId}`;
    }
};

// Close the waiting list popup
cancelWaitingListBtn.onclick = function () {
    waitingListPopup.style.display = "none";
};

// Close the error popup
closeErrorPopupBtn.onclick = function () {
    errorPopup.style.display = "none";
};

// Close popups when the close button (×) is clicked
closeBtns.forEach(btn => {
    btn.onclick = function () {
        const popup = btn.closest(".popup");
        popup.style.display = "none";
    };
});

// Close popups if the user clicks outside of them
window.onclick = function (event) {
    if (event.target.classList.contains("popup")) {
        event.target.style.display = "none";
    }
};

// Example usage:
// showBorrowingLimitPopup();
// showWaitingListPopup();
// showErrorPopup("An error occurred!");