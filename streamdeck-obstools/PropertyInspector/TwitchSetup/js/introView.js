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
    var content = "<div class='leftAlign'><p class='leftAlign'>" + localization['Intro']['Description'] + "</p> \
                   <p class='leftAlign'>" + localization['Intro']['DescriptionPart2'] + "</p><hr/></div> \
                   <div id = 'controls' ></div> ";
    document.getElementById('content').innerHTML = content;


    // Show manual user controls instead
    var controls = "<p class='small leftAlign'>" + localization['Intro']['NotePopup'] + "</p><br/><div class='button' id='start'>" + localization['Intro']['Submit'] + "</div> \
                               <div class='button-transparent' id='close'>" + localization['Intro']['Close'] + "</div>";
    document.getElementById('controls').innerHTML = controls;

    // Add event listener
    document.getElementById("start").addEventListener("click", startPairing);
    document.addEventListener("enterPressed", startPairing);

    document.getElementById("close").addEventListener("click", close);
    document.addEventListener("escPressed", close);


    // Load the pairing view
    function startPairing() {
        unloadIntroView();
        window.opener.openTwitchAuth();
        loadPairingView();
    };


    // Close the window
    function close() {
        window.close();
    };


    // Unload view
    function unloadIntroView() {
        // Remove event listener
        document.removeEventListener("enterPressed", startPairing);
        document.removeEventListener("escPressed", close);
    }
}
