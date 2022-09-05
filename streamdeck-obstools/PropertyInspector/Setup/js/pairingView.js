// Load the pairing view
function loadPairingView() {
    console.log("Pairing loaded");
    // Set the status bar
    setStatusBar('pairing');

    // Fill the title
    document.getElementById('title').innerHTML = localization['Pairing']['Title'];

    // Fill the content area
    var content = "<div class='leftAlign'><p class='leftAlign'>" + localization['Pairing']['Description'] + "</p> \
                   <hr/><br/></div> \
        <div id = 'controls' class = 'leftAlign'></div> ";
    document.getElementById('content').innerHTML = content;

    // Show manual user controls instead
    var controls = "<input type='textarea' class='textbox' placeholder='" + localization['Pairing']['IpPlaceholder'] + "' value='127.0.0.1' id='ip'><span id='ip-error' class='error bold' style='display:none;'>*</span><br/>\
                    <input type='textarea' class='textbox' placeholder='" + localization['Pairing']['PortPlaceholder'] + "' value = '4455' id = 'port'><span id='port-error' class='error bold' style='display:none;'>*</span><br/> \
                    <input type='textarea' class='textbox' placeholder='" + localization['Pairing']['PassPlaceholder'] + "' value = '' id = 'pass'>&nbsp;&nbsp;<span title='It is HIGHLY RECOMMENDED to set a password in the WebSockets Server settings.'>🛈</span><br/>\
                              <div class='button' id='submit'>" + localization['Pairing']['Submit'] + "</div> \
                               <div class='button-transparent' id='close'>" + localization['Pairing']['Close'] + "</div>";
    document.getElementById('controls').innerHTML = controls;
    document.getElementById('app-over').className = "ellipseStart ellipseTopLeft";

    // Add event listener
    document.getElementById("submit").addEventListener("click", submit);
    document.addEventListener("enterPressed", submit);

    document.getElementById("close").addEventListener("click", closeWindow);
    document.addEventListener("escPressed", closeWindow);

}

// Retry pairing by reloading the view
function submit() {
    var ip = document.getElementById('ip').value;
    var port = document.getElementById('port').value;
    var pass = document.getElementById('pass').value;
    var validationFailed = false;

    if (!ip || ip.length === 0) {
        validationFailed = true;
        document.getElementById('ip-error').style.display = '';
    }

    if (!port || port.length === 0) {
        validationFailed = true;
        document.getElementById('port-error').style.display = '';
    }

    if (validationFailed) {
        return;
    }

    unloadControlsBinding();
    loadValidatingView();
    window.opener.updateServerInfo(ip, port, pass);
}

function updateLinkStatus(status, reason) {
    console.log('Update Link Status')
    if (!status) {
        unloadControlsBinding();
        setFailReason(reason);
        loadFailedView();
    }
}

// Close the window
function closeWindow() {
    window.close();
}

// Unload view
function unloadControlsBinding() {
    // Remove event listener
    document.removeEventListener("escPressed", submit);
    document.removeEventListener("enterPressed", closeWindow);
}