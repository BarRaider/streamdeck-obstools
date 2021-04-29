// Load the intro view
function loadIntroView() {
    // Set the status bar
    clearStatusBar();
    setStatusBar('intro');

    // Fill the title
    document.getElementById('title').innerHTML = localization['Intro']['Title'];

    // Clear all custom classes
    document.getElementById('title').className = "";
    document.getElementById('app-over').className = "";
    document.getElementById('br-over').className = "";
    document.getElementById('app-logo').className = "app-logo";
    document.getElementById('br-logo').className = "br-logo";

    // Fill the content area
    var content = "<p>" + localization['Intro']['Description'] + "<span class='linkspan' onclick='window.opener.openObsDownload()'>" + localization['Intro']['WebSocket'] + "</span>" + localization['Intro']['Description2'] + "</p> \
                  <br/><p class='nextStep'>" + localization['Intro']['NextStep'] + "<p>\
                   <div class='button' id='start'>" + localization['Intro']['Start'] + "</div> \
                   <div class='button-transparent' id='close'>" + localization['Intro']['Close'] + "</div>";
    document.getElementById('content').innerHTML = content;

    // Add event listener
    document.getElementById("start").addEventListener("click", startPairing);
    document.addEventListener("enterPressed", startPairing);

    document.getElementById("close").addEventListener("click", closeWindow);
    document.addEventListener("escPressed", closeWindow);


    // Load the pairing view
    function startPairing() {
        unloadIntroView();
        loadPairingView();
    };


    // Close the window
    function closeWindow() {
        window.close();
    };


    // Unload view
    function unloadIntroView() {
        // Remove event listener
        document.removeEventListener("enterPressed", startPairing);
        document.removeEventListener("escPressed", closeWindow);
    }
}
