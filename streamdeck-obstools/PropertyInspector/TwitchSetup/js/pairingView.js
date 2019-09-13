// Load the pairing view
function loadPairingView() {
    // Time used to automatically pair bridges
    var autoPairingTimeout = 5;

    // Define local timer
    var timer = null;

    // Set the status bar
    setStatusBar('pairing');

    // Fill the title
    document.getElementById('title').innerHTML = localization['Pairing']['Title'];

    // Fill the content area
    var content = "<div class='leftAlign'><p class='leftAlign'>" + localization['Pairing']['Description'] + "</p> \
                   <p class='leftAlign'>" + localization['Pairing']['DescriptionPart2'] + "<span class='linkspan' onclick='window.opener.openTwitchAuth()'>" + localization['Pairing']['ClickHere'] + "</span> " +
        localization['Pairing']['DescriptionPart3'] + "</p><hr/><br/></div> \
        <div id = 'controls' ></div> ";
    document.getElementById('content').innerHTML = content;

    // Start the pairing
    autoPairing();


    // For n seconds try to connect to the bridge automatically
    function autoPairing() {

        // Show manual user controls instead
        var controls = "<div class='inputTitle'>" + localization['Pairing']['ApprovalCodeTitle'] + ":</div><input type='textarea' class='approvalCode' placeholder='" + localization['Pairing']['ApprovalPlaceholder'] + "' value='' id='approvalCode'>\
                               <p class='small leftAlign'>" + localization['Pairing']['NotePopup'] + "</p><br/><div class='button' id='submit'>" + localization['Pairing']['Submit'] + "</div> \
                               <div class='button-transparent' id='close'>" + localization['Pairing']['Close'] + "</div>";
        document.getElementById('controls').innerHTML = controls;

        // Add event listener
        document.getElementById("submit").addEventListener("click", submit);
        document.addEventListener("enterPressed", submit);

        document.getElementById("close").addEventListener("click", close);
        document.addEventListener("escPressed", close);

    }

    // Retry pairing by reloading the view
    function submit() {
        var approvalCode = document.getElementById('approvalCode');
        window.opener.updateApprovalCode(approvalCode.value);
        unloadPairingView();
        loadValidatingView();
    }


    // Close the window
    function close() {
        window.close();
    }

    // Unload view
    function unloadPairingView() {
        // Stop the timer
        clearInterval(timer);
        timer = null;

        // Remove event listener
        document.removeEventListener("escPressed", submit);
        document.removeEventListener("enterPressed", close);
    }
}
