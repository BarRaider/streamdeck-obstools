﻿const activeSceneName = "- Active Scene -";

document.addEventListener('websocketCreate', function () {
    console.log("Websocket created!");
    checkSettings(actionInfo.payload.settings);

    websocket.addEventListener('message', function (event) {
        console.log("Got message event!");

        // Received message from Stream Deck
        var jsonObj = JSON.parse(event.data);

       if (jsonObj.event === 'didReceiveSettings') {
            var payload = jsonObj.payload;
            checkSettings(payload.settings);
        }
    });
});


function checkSettings(payload) {
    console.log("Checking Settings");
    setAutoReplayWrapper("none");
    setTwitchSettings("none");
    setTwitchReplaySettings("none");

    if (payload['autoReplay']) {
        setAutoReplayWrapper("");
    }

    if (payload['twitchIntegration']) {
        setTwitchSettings("");
    }

    if (payload['chatReplay']) {
        setTwitchReplaySettings("");
    }

    showAutoSwitchTextBox("");
    if (payload['sceneName'] === activeSceneName) {
        showAutoSwitchTextBox("none");
    }
}

function setAutoReplayWrapper(displayValue) {
    var dvAutoReplaySettings = document.getElementById('dvAutoReplaySettings');
    dvAutoReplaySettings.style.display = displayValue;
}

function setTwitchSettings(displayValue) {
    var dvTwitchSettings = document.getElementById('dvTwitchSettings');
    dvTwitchSettings.style.display = displayValue;
}

function setTwitchReplaySettings(displayValue) {
    var dvTwitchReplaySettings = document.getElementById('dvTwitchReplaySettings');
    dvTwitchReplaySettings.style.display = displayValue;
}

function openTutorial() {
    if (websocket && (websocket.readyState === 1)) {
        const json = {
            'event': 'openUrl',
            'payload': {
                'url': 'https://buz.bz/oht2'
            }
        };
        websocket.send(JSON.stringify(json));
    }
}

function showAutoSwitchTextBox(show) {
    var dvAutoSwitch = document.getElementById('dvAutoSwitch');
    dvAutoSwitch.style.display = show;
}

function setInputNameSetting() {
    var dvSourceSelect = document.getElementById('inputs');
    var dvSourceName = document.getElementById('inputName');

    dvSourceName.value = dvSourceSelect.value;
    setSettings();
}