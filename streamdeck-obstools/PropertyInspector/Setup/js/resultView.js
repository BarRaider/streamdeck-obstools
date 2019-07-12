
function loadValidatingView() {
    setStatusBar('result');

    // Fill the title
    document.getElementById('title').innerHTML = localization['Result']['ValidateTitle'];

    // Fill the content area
    var content = "<p>" + localization['Result']['ValidateDescription'] + "</p> \
                   <div id='loader'></div> \
                   <div class='button' id='close'>" + localization['Result']['Cancel'] + "</div>";
    document.getElementById('content').innerHTML = content;

    document.getElementById("close").addEventListener("click", close);

    // Close this window
    function close() {
        window.close();
    }
}

function loadFailedView() {
    setStatusBar('result');

    // Fill the title
    document.getElementById('title').innerHTML = localization['Result']['FailTitle'];

    // Fill the content area
    var content = "<p>" + localization['Result']['FailDescription'] + "</p> \
                   <img class='image' src='images/fail.png'> \
                   <div class='button' id='failRetry'>" + localization['Result']['FailRetry'] + "</div> \
                   <div class='button-transparent' id='close'>" + localization['Result']['Close'] + "</div>";
    document.getElementById('content').innerHTML = content;

    document.getElementById("close").addEventListener("click", close);
    document.getElementById("failRetry").addEventListener("click", failRetry);

    // Close this window
    function close() {
        window.close();
    }

    function failRetry() {
        // Remove event listener
        document.removeEventListener("close", close);
        document.removeEventListener("failRetry", failRetry);

        loadIntroView();
    }



}

// Load the results view
function loadSuccessView() {
    // Set the status bar
    setStatusBar('result');

    // Fill the title
    document.getElementById('title').innerHTML = localization['Result']['SuccessTitle'];

    // Fill the content area
    var content = "<p>" + localization['Result']['SuccessDescription'] + "</p> \
                   <img class='image' src='images/paired.png'> \
                   <div class='button' id='close'>" + localization['Result']['Close'] + "</div>";
    document.getElementById('content').innerHTML = content;

    // Add event listener
    document.getElementById("close").addEventListener("click", close);
    document.addEventListener("enterPressed", close);
    
    // Close this window
    function close() {
        window.close();
    }
}
