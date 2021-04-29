// Load the pairing view
function loadPairingView() {
    // Set the status bar
    setStatusBar('pairing');

    // Fill the title
    document.getElementById('title').innerHTML = localization['Pairing']['Title'];

    // Fill the content area
    var content = "<p>" + localization['Pairing']['Description'] + "</p> \
                   <div id='loader'></div> \
                   <div class='button-transparent' id='close'>" + localization['Pairing']['Cancel'] + "</div>";
    document.getElementById('content').innerHTML = content;

    document.getElementById("close").addEventListener("click", closeWindow);

    if (!timerPairingTimeout) {
        timerPairingTimeout = setTimeout(function () { console.log('Pairing timeout!'); loadFailedView(); }, 60000)
    }

    document.getElementById('app-over').className = "ellipseStart ellipseTopLeft";
    document.getElementById('br-over').className = "ellipseStart  ellipseBottomRight";

    // Close this window
    function closeWindow() {
        window.close();
    }
}
