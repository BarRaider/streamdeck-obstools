// Global variable containting the localizations
var localization = null;
var timerPairingTimeout = null;

// Global function to load the localizations
function getLocalization(callback) {
    var language = "en";
    var url = language + ".json";
    var xhr = new XMLHttpRequest();
    xhr.open("GET", url, true);
    xhr.onload = function () {
        if (xhr.readyState == XMLHttpRequest.DONE) {
            try {
                data = JSON.parse(xhr.responseText);
                localization = data['Localization']['Setup'];
                callback(true);
            }
            catch(e) {
                callback(false);
            }
        }
        else {
            callback(false);
        }
    };
    xhr.onerror = function () {
        callback(false);
    };
    xhr.ontimeout = function () {
        callback(false);
    };
    xhr.send();
}

var currentSetupPhase;
function clearStatusBar() {
    var statusCells = document.getElementsByClassName('status-cell');
    Array.from(statusCells).forEach(function (cell) {
        cell.className = "status-cell";
    });
}

// Global function to set the status bar to the correct view
function setStatusBar(view) {
    currentSetupPhase = view;
    setStatusBarTitles(view);

    // Remove active status from all status cells
    var foundView = false;
    var statusCells = document.getElementsByClassName('status-cell');
    Array.from(statusCells).forEach(function (cell) {
        if (cell.id === 'status-' + view) { // Set current one to active
            foundView = true;
            cell.classList.add("active");
        }
        else if (cell.classList.contains("active")) {
            cell.classList.remove("active");
        }
        if (!foundView) {
            cell.classList.add("completed");
        }
    });
}

function setStatusBarTitles(view) {
    console.log("View", view);
    var titleCells = document.getElementsByClassName('title-cell');
    Array.from(titleCells).forEach(function (cell) {
        cell.classList.remove("active");
        cell.innerHTML = localization['General'][cell.id];
    });

    // Set it only to the current one
    document.getElementById('title-' + view).classList.add("active");
}

// Main function run after the page is fully loaded
window.onload = function() {
    // Bind enter and ESC keys
    document.addEventListener('keypress', function (e) {
        var key = e.which || e.keyCode;
        if (key === 13) {
            var event = new CustomEvent("enterPressed");
            document.dispatchEvent(event);
        }
        else if (key === 27) {
            var event = new CustomEvent("escPressed");
            document.dispatchEvent(event);
        }
    });

    // Show the intro view
    getLocalization(function(status) {
        if (status) {
            loadIntroView();
        }
        else {
            document.getElementById('content').innerHTML = "<p>Could not load the localizations.</p>";
        }
    });
};
