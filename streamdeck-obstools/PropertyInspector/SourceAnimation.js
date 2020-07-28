document.addEventListener('websocketCreate', function () {
    console.log("Websocket created!");
    handlePhases(actionInfo.payload.settings);

    websocket.addEventListener('message', function (event) {
        console.log("Got message event!");

        // Received message from Stream Deck
        var jsonObj = JSON.parse(event.data);

        if (jsonObj.event === 'sendToPropertyInspector') {
            var payload = jsonObj.payload;
            handlePhases(payload);
        }
        else if (jsonObj.event === 'didReceiveSettings') {
            var payload = jsonObj.payload;
            handlePhases(payload.settings);
        }
    });
});

function handlePhases(payload) {
    console.log("Handling Phases");
    let currentPhase = payload['selectedPhase'];

    if (payload['animationPhases']) {
        populatePhasesDropdown(payload['animationPhases'], currentPhase);
        let phaseSettings = payload['animationPhases'][currentPhase];
        console.log("Setting configuration for phase", currentPhase, phaseSettings);
        loadConfiguration(phaseSettings);
    }
    else {
        console.log("WARNING: animationPhases is null");
    }

    showHideRecording(payload['isRecording']);

}

function populatePhasesDropdown(animationPhases, currentValue) {
    var phasesDropdown = document.getElementById("phasesDropdown");
    phasesDropdown.options.length = 0;

    for (var idx = 0; idx < animationPhases.length; idx++) {
        var opt = document.createElement('option');
        opt.value = idx;
        opt.text = animationPhases[idx]['phaseName'];
        phasesDropdown.appendChild(opt);
    }
    phasesDropdown.value = currentValue;
}

function updatePhase() {
    var phasesDropdown = document.getElementById("phasesDropdown");
    var selectedPhase = document.getElementById("selectedPhase");
    selectedPhase.value = phasesDropdown.value;
    setSettings();
}

function addAnimation() {
    var propertyName = document.getElementById("propertyName");
    var startValue = document.getElementById("startValue");
    var endValue = document.getElementById("endValue");
    var payload = {};
    payload.property_inspector = 'addAnimation';
    payload.propertyName = propertyName.value;
    payload.startValue = startValue.value;
    payload.endValue = endValue.value;
    sendPayloadToPlugin(payload);
}

function removeAnimation() {
    var animationActions = document.getElementById("animationActions");
    var payload = {};
    payload.property_inspector = 'removeAnimation';
    payload.removeIndex = animationActions.selectedIndex;
    sendPayloadToPlugin(payload);
}

function addAnimationPhaseAbove() {
    var payload = {};
    payload.property_inspector = 'addAnimationPhaseAbove';
    sendPayloadToPlugin(payload);
}

function addAnimationPhaseBelow() {
    var payload = {};
    payload.property_inspector = 'addAnimationPhaseBelow';
    sendPayloadToPlugin(payload);
}


function delAnimationPhase() {
    var payload = {};
    payload.property_inspector = 'delAnimationPhase';
    sendPayloadToPlugin(payload);
}

function exportSettings() {
    console.log("Export settings...");
    var payload = {};
    payload.property_inspector = 'exportSettings';
    sendPayloadToPlugin(payload);
}

function importSettings() {
    console.log("Import settings...");
    var payload = {};
    payload.property_inspector = 'importSettings';
    sendPayloadToPlugin(payload);
}

function startRecording() {
    console.log("Start Recording...");
    var payload = {};
    payload.property_inspector = 'startRecording';
    sendPayloadToPlugin(payload);
}

function endRecording() {
    console.log("Ending Recording...");
    var payload = {};
    payload.property_inspector = 'endRecording';
    sendPayloadToPlugin(payload);
}

function showHideRecording(isRecording) {
    var btnStartRecording = document.getElementById('btnStartRecording');
    var btnEndRecording = document.getElementById('btnEndRecording');

    if (isRecording) {
        btnStartRecording.style.display = "none";
        btnEndRecording.style.display = "";
    }
    else {
        btnStartRecording.style.display = "";
        btnEndRecording.style.display = "none";
    }
}