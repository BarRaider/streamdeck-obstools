var failReason = null;

function loadValidatingView() {
    setStatusBar('result');

    // Fill the title
    document.getElementById('title').innerHTML = localization['Result']['ValidateTitle'];

    // Fill the content area
    var content = "<p>" + localization['Result']['ValidateDescription'] + "</p> \
                   <div id='loader'></div> \
                   <div class='button-transparent' id='close'>" + localization['Result']['Cancel'] + "</div>";
    document.getElementById('content').innerHTML = content;

    document.getElementById("close").addEventListener("click", closeWindow);
    document.getElementById('app-over').className = "ellipseStart ellipseTopLeft";
    document.getElementById('br-over').className = "ellipseStart  ellipseBottomRight";

    // Set Pairing Timeout
    if (!timerPairingTimeout) {
        timerPairingTimeout = setTimeout(function () {
            console.log('Pairing timeout!');
            setFailReason('Pairing Timeout');
            loadFailedView();
            window.opener.resetPlugin();
        }, 30000)
    }

    // Close this window
    function closeWindow() {
        window.close();
    }
}

function setFailReason(reason) {
    console.log("setFailReason called", reason);
    failReason = reason;
}

function loadFailedView() {
    console.log("loadFailedView called!");
    if (timerPairingTimeout) {
        clearTimeout(timerPairingTimeout);
        timerPairingTimeout = null;
    }

    setStatusBar('result');

    // Fill the title
    document.getElementById('title').innerHTML = localization['Result']['FailTitle'];

    document.getElementById('app-over').className = "ellipseFail ellipseTopLeft";
    document.getElementById('br-over').className = "ellipseFail ellipseBottomRight";

    // Fill the content area
    var content = "<p>" + localization['Result']['FailDescription'] + "<span class='button discord marginLeft0' id='discord'><img src='./images/discord.png'>DISCORD</span></p> \
                  <br/><br/> \
                   <div class='button' id='failRetry'>" + localization['Result']['FailRetry'] + "</div>\
                   <div class='button-transparent' id='close'>" + localization['Result']['Close'] + "</div>";

    // Append fail reason
    if (failReason) {
        content = "<p class='error bold'>" + failReason + "</p>" + content;
    }

    document.getElementById('content').innerHTML = content;

    document.getElementById("close").addEventListener("click", closeWindow);
    document.getElementById("failRetry").addEventListener("click", failRetry);
    document.getElementById("discord").addEventListener("click", discord);

    // Close this window
    function closeWindow() {
        window.close();
    }

    function failRetry() {
        // Remove event listener
        document.removeEventListener("close", closeWindow);
        document.removeEventListener("failRetry", failRetry);

        loadIntroView();
    }

    function discord() {
        window.opener.openDiscord();
    }
}

// Load the results view
function loadSuccessView() {
    console.log("loadSuccessView called!");
    if (timerPairingTimeout) {
        clearTimeout(timerPairingTimeout);
        timerPairingTimeout = null;
    }

    // Set the status bar
    setStatusBar('result');

    // Fill the title
    document.getElementById('title').innerHTML = localization['Result']['SuccessTitle'];

    document.getElementById('title').className = "success";

    document.getElementById('app-over').className = "borderLink";
    document.getElementById('br-over').className = "";
    document.getElementById('app-logo').className = "app-logo borderDone";
    document.getElementById('br-logo').className = "br-logo borderDone";

    // Fill the content area
    // Fill the content area
    var content = "<p>" + localization['Result']['SuccessDescription'] + "</p>";
    document.getElementById('content').innerHTML = content;

    // Show the bar-box
    document.getElementById('bar-box').style.display = "";

    // Add event listener
    document.addEventListener("enterPressed", closeWindow);
    document.getElementById("discord").addEventListener("click", discord);
    document.getElementById("twitter").addEventListener("click", twitter);
    
    // Close this window
    function closeWindow() {
        window.close();
    }

    function discord() {
        window.opener.openDiscord();
    }

    function twitter() {
        window.opener.openTwitter();
    }
}
