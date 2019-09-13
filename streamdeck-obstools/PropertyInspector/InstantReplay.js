﻿document.addEventListener('websocketCreate', function () {
    console.log("Websocket created!");
    checkSettings(actionInfo.payload.settings);

    websocket.addEventListener('message', function (event) {
        console.log("Got message event!");

        // Received message from Stream Deck
        var jsonObj = JSON.parse(event.data);

        if (jsonObj.event === 'sendToPropertyInspector') {
            var payload = jsonObj.payload;
            checkSettings(payload);
        }
        else if (jsonObj.event === 'didReceiveSettings') {
            var payload = jsonObj.payload;
            checkSettings(payload.settings);
        }
    });
});

function checkSettings(payload) {
    console.log("Checking Settings");
    setAutoReplayWrapper("none");
    setTwitchSettings("none");
    if (payload['autoReplay']) {
        setAutoReplayWrapper("");
    }

    if (payload['twitchIntegration']) {
        setTwitchSettings("");
    }
}

function setAutoReplayWrapper(displayValue) {
    var dvAutoReplaySettings = document.getElementById('dvAutoReplaySettings');
    dvAutoReplaySettings.style.display = displayValue;
}

function setTwitchSettings(displayValue) {
    var dvChatIntegrationSettings = document.getElementById('dvChatIntegrationSettings');
    dvChatIntegrationSettings.style.display = displayValue;
}