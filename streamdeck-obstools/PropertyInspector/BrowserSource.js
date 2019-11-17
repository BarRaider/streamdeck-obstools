document.addEventListener('websocketCreate', function () {
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
    setLocalFile("none");
    setURL("");
    if (payload['localFile']) {
        setLocalFile("");
        setURL("none");
    }
}

function setLocalFile(displayValue) {
    var dvLocalFile = document.getElementById('dvLocalFile');
    dvLocalFile.style.display = displayValue;
}

function setURL(displayValue) {
    var dvSourceURL = document.getElementById('dvSourceURL');
    dvSourceURL.style.display = displayValue;
}