# OBS Tools
Advanced OBS commands and tools to use on your Elgato Stream Deck

**Author's website and contact information:** [https://barraider.com](https://barraider.com)

## New in v1.7
***NOTE: This version requires upgrading to OBS Websocket v4.8 (!!) see: https://barraider.com/obs***

NOTE2: There is now a dedicated channel to speak about the OBS Animations. Reach out to @BarRaider to join.

What's New:
- Introducing `Source Animation` action! Create cool transitions and effects for your sources with one click!! :kreygasm10000: 
    - Phases allow multi-phased animations, without the need of a multi-action
	- Easily create animation with the `RECORD` feature: 1. Place source at starting position, then press 'Record'. 2. Move/Modify source to end result and then press 'End Recording' => The plugin will automatically calculate and input the changes.
	- Import/Export Settings allows you to share your animations (or keep a backup)
    - Options to hide source/remove filter at various stages
- New `Studio Mode Toggle` action allows you to quickly toggle Studio/Preview mode on and off.
- Multi-Action support for Smart Scene Switcher
    - Behavior can now be customized when inside the multi-action (Standard, Force Studio, Force Live)
- `Video Player` action now supports Export/Import of settings to share your animations (or keep a backup)
- Upgraded to OBS Websocket 4.8
- Refreshed the icons to better reflect each action

## New in v1.6
- `Source Volume` allows increasing/decreasing/setting the volume of an audio source
- Support for modifying the speed of Videos/Instant Replay (great when you want to do a slow mo)
- :new: `Smart Scene Switcher`  - Easily switch scenes between Preview/Studio (if enabled)  and Live modes. 
  - Shows a border on the scene indicating if it's in preview or live
  - **See a Preview of how the scene will look on the Stream Deck key**

## New in v1.5
- New Action: `Set Transition` allows you to modify the default scene transition from the Stream Deck
- New Action `Filter Toggle` allows you to enable/disable filters on a source from the Stream Deck.
- Added Multi-Action support for the `Previous Scene` action
- Added checks to verify user is on the correct version of obs-websocket
***Make sure to upgrade to obs-websocket v4.7 or above ***

## Features:
- Instant Replay Action - Click to save the last seconds of your stream to your OBS "Recordings" folder.
  - Long-Press the button to toggle whether the Instant Replay buffer is recording or not
- Dropped Frames Alarm - Shows the current amount of dropped frames and starts alerting if it increases.
  - Choose between 3 different dropped frame types: Dropped Frames, Output Skipped Frames, Render Missed Frames
  - You can now customize the color of the alert
- OBS CPU Usage - Shows how much CPU is being utilized by OBS
- Previous Scene Action - Allows you to switch back to your previously used scene. Writes the name of the scene on the key.
- Twitch Integration
	- Let your chat to type !replay and trigger an instant replay which is shown on stream
    - Instant Replay can now also create a Twitch Clip for you, and post it on chat
    - Instant Replay can now create a Twitch Clip even if the replay buffer is off
- `Browser Sources` can now be modified using the Stream Deck
- `Instant Replay` is now support in Multi Actions (including the options to Enable/Disable/Take a replay)
- New `Remote Recording Toggle` to toggle recording from a remote PC (if your Stream Deck is not connected to your Streaming PC)
- New `Remote Streaming Toggle` to stop/start streaming from a remote PC (if your Stream Deck is not connected to your Streaming PC)


# INSTALLATION
**Important:** You must download and install obs-websocket before using this plugin. Install from here: https://github.com/Palakis/obs-websocket/releases/

2. After installing, enable from inside OBS: Tools -> WebSockets Server Setting 
[You can keep the port as is, but It is ***highly recommended you Enable authentication and set a password***]

3. For instant replay to work, you must check the **Enabled Replay Buffer** from File->Settings->Output->Recording

## Usage
**Demo for Instant Replay**: https://www.youtube.com/watch?v=7mioa-hnndw

OBS must be streaming for the majority of the features to work
For instant replay to work, you must check the **Enabled Replay Buffer** from File->Settings->Output->Recording

### Download

* [Download plugin](https://github.com/BarRaider/streamdeck-obstools/releases/)

## I found a bug, who do I contact?
For support please contact the developer. Contact information is available at https://barraider.com

## I have a feature request, who do I contact?
Please contact the developer. Contact information is available at https://barraider.com

## Dependencies
* Uses StreamDeck-Tools by BarRaider: [![NuGet](https://img.shields.io/nuget/v/streamdeck-tools.svg?style=flat)](https://www.nuget.org/packages/streamdeck-tools)
* Uses [Easy-PI](https://github.com/BarRaider/streamdeck-easypi) by BarRaider - Provides seamless integration with the Stream Deck PI (Property Inspector) 

