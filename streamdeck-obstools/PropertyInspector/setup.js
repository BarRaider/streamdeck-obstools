var authWindow = null;
var twitchWindow = null;

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

    // Open up setup dialog for twitch if needed
    if (payload['twitchTokenExists'] && payload['twitchIntegration']) {
        if (twitchWindow) {
            twitchWindow.loadSuccessView();
        }
    }
    else if (payload['serverInfoExists'] && payload['twitchIntegration']) {
        if (twitchWindow) {
            twitchWindow.loadFailedView();
        }
        else {
            twitchWindow = window.open("TwitchSetup/index.html")
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

function openTwitchAuth() {
    if (websocket && (websocket.readyState === 1)) {
        const json = {
            'event': 'openUrl',
            'payload': {
                'url': 'https://id.twitch.tv/oauth2/authorize?client_id=ejt1rblr4jf0c1sar20dnkzpikh0g1&redirect_uri=https://BarRaider.com/twitchauth&response_type=token&scope=channel_feed_read%20chat:read%20chat:edit%20whispers:read%20whispers:edit%20clips:edit'
            }
        };
        websocket.send(JSON.stringify(json));
    }
}

function updateApprovalCode(val) {
    var approvalCode = val;

    var payload = {};
    payload.property_inspector = 'updateApproval';
    payload.approvalCode = approvalCode;
    sendPayloadToPlugin(payload);
    console.log("Approving code");
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
