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
                   <hr/><br/></div> \
        <div id = 'controls' ></div> ";
    document.getElementById('content').innerHTML = content;

    // Start the pairing
    autoPairing();


    // For n seconds try to connect to the bridge automatically
    function autoPairing() {

        // Show manual user controls instead
        var controls = "<div class='inputTitle'><table border=0><tbody><tr><td>" + localization['Pairing']['IpTitle'] + ":</td><td><input type='textarea' class='approvalCode' placeholder='" + localization['Pairing']['IpPlaceholder'] + "' value='127.0.0.1' id='ip'></td><tr><td>" +
            localization['Pairing']['PortTitle'] + ":</td><td><input type='textarea' class='approvalCode' placeholder='" + localization['Pairing']['PortPlaceholder'] + "' value = '4444' id = 'port'></td><tr><td>" + 
            localization['Pairing']['PassTitle'] + ":</td><td><input type='textarea' class='approvalCode' placeholder='" + localization['Pairing']['PassPlaceholder'] + "' value = '' id = 'pass'></td></tr></tbody></table><br/>\
                              <div class='button' id='submit'>" + localization['Pairing']['Submit'] + "</div> \
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
        var ip = document.getElementById('ip');
        var port = document.getElementById('port');
        var pass = document.getElementById('pass');
        window.opener.updateServerInfo(ip.value, port.value, pass.value);
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
