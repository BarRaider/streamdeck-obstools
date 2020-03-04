document.addEventListener('websocketCreate', function () {
    console.log("Websocket created!");
    checkSettings(actionInfo.payload.settings);
    window.setTimeout(updateVolumeLabel, 500);

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
        window.setTimeout(updateVolumeLabel, 500);
    });
});

document.addEventListener('settingsUpdated', function (event) {
    console.log("Got settingsUpdated event!");
    window.setTimeout(updateVolumeLabel, 500);
})

function checkSettings(payload) {
    console.log("Checking Settings");
}

function updateVolumeLabel() {
    var volumeLabel = document.getElementById('volumeLabel');
    var volume = document.getElementById('volume');

    volumeLabel.innerText = "Volume: " + volume.value + "%";
}
