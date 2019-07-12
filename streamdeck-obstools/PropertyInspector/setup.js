var authWindow = null;

document.addEventListener('websocketCreate', function () {
    console.log("Websocket created!");
    checkToken(actionInfo.payload.settings);

    websocket.addEventListener('message', function (event) {
        console.log("Got message event!");

        // Received message from Stream Deck
        var jsonObj = JSON.parse(event.data);

        if (jsonObj.event === 'sendToPropertyInspector') {
            var payload = jsonObj.payload;
            checkToken(payload);
        }
        else if (jsonObj.event === 'didReceiveSettings') {
            var payload = jsonObj.payload;
            checkToken(payload.settings);
        }
    });
});

function checkToken(payload) {
    console.log("Checking Server Info...");
    var tokenExists = document.getElementById('serverInfoExists');
    tokenExists.value = payload['serverInfoExists'];

    if (payload['serverInfoExists']) {
        var event = new Event('serverInfoExists');
        document.dispatchEvent(event);

        if (authWindow) {
            authWindow.loadSuccessView();
        }
    }
    else {
        if (authWindow) {
            authWindow.loadFailedView();
        }
        else {
            authWindow = window.open("Setup/index.html")
        }
    }
}

function openObsDownload() {
    if (websocket && (websocket.readyState === 1)) {
        const json = {
            'event': 'openUrl',
            'payload': {
                'url': 'https://github.com/Palakis/obs-websocket/releases'
            }
        };
        websocket.send(JSON.stringify(json));
    }
}


function resetPlugin() {
    var payload = {};
    payload.property_inspector = 'resetPlugin';
    sendPayloadToPlugin(payload);
}

function updateServerInfo(ip, port, password) {
    console.log("Setting Server Info...");

    var payload = {};
    payload.property_inspector = 'setserverinfo';
    payload.ip = ip;
    payload.port = port;
    payload.password = password;
    sendPayloadToPlugin(payload);
    console.log("Approving server info");
}

function sendPayloadToPlugin(payload) {
    if (websocket && (websocket.readyState === 1)) {
        const json = {
            'action': actionInfo['action'],
            'event': 'sendToPlugin',
            'context': uuid,
            'payload': payload
        };
        websocket.send(JSON.stringify(json));
    }
}
