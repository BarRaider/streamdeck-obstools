const activeSceneName = "- Active Scene -";

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
    showSourceTextBox(false);

    if (payload['sceneName'] === activeSceneName) {
        showSourceTextBox(true);
    }
}

function showSourceTextBox(show) {
    var dvSourceSelect = document.getElementById('dvSourceNameSelect');
    var dvSourceTextBox = document.getElementById('dvSourceName');

    if (show) {
        dvSourceSelect.style.display = "none";
        dvSourceTextBox.style.display = "";
    }
    else {
        dvSourceSelect.style.display = "";
        dvSourceTextBox.style.display = "none";
    }
}

function setSourceNameSetting() {
    var dvSourceSelect = document.getElementById('sources');
    var dvSourceName = document.getElementById('sourceName');

    dvSourceName.value = dvSourceSelect.value;
    setSettings();
}