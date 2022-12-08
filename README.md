# OBS Tools
Advanced OBS commands and tools to use on your Elgato Stream Deck

**Author's website and contact information:** [https://barraider.com](https://barraider.com)

# New in v2.9
- Stability and performance improvements

## Features:
- `Smart Scene Switcher`  - Easily switch scenes between Preview/Studio (if enabled)  and Live modes. 
  - Shows a border on the scene indicating if it's in preview or live
  - **See a Preview of how the scene will look on the Stream Deck key**
- `Source Animation` action! Create cool transitions and effects for your sources with one click!! 
    - Phases allow multi-phased animations, without the need of a multi-action
	- Easily create animation with the `RECORD` feature: 1. Place source at starting position, then press 'Record'. 2. Move/Modify source to end result and then press 'End Recording' => The plugin will automatically calculate and input the changes.
	- Import/Export Settings allows you to share your animations (or keep a backup)
    - Options to hide source/remove filter at various stages
- `Studio Mode Toggle` action allows you to quickly toggle Studio/Preview mode on and off.
- `Instant Replay` Action - Click to save the last seconds of your stream to your OBS "Recordings" folder.
  - Long-Press the button to toggle whether the Instant Replay buffer is recording or not
- `Video Player` allows using the same source to display different media files
- Support for modifying the speed of Videos/Instant Replay (great when you want to do a slow mo)
- `Source Volume` allows increasing/decreasing/setting the volume of an audio source
- `Dropped Frames Alarm` - Shows the current amount of dropped frames and starts alerting if it increases.
  - Choose between 3 different dropped frame types: Dropped Frames, Output Skipped Frames, Render Missed Frames
  - You can now customize the color of the alert
- OBS `CPU Usage` - Shows how much CPU is being utilized by OBS
- `Previous Scene` Action - Allows you to switch back to your previously used scene. Writes the name of the scene on the key.
- ***Twitch Integration***
	- Let your chat to type !replay and trigger an instant replay which is shown on stream
    - Instant Replay can now also create a Twitch Clip for you, and post it on chat
    - Instant Replay can now create a Twitch Clip even if the replay buffer is off
- `Browser Sources` can now be modified using the Stream Deck
- `Remote Recording Toggle` to toggle recording from a remote PC (if your Stream Deck is not connected to your Streaming PC)
- `Remote Streaming Toggle` to stop/start streaming from a remote PC (if your Stream Deck is not connected to your Streaming PC)
- `Set Transition` allows you to modify the default scene transition from the Stream Deck
- `Filter Toggle` allows you to enable/disable filters on a source from the Stream Deck.
- `Set Profile` action allows you to modify the OBS Profile
- `Set Scene Collection` action allows you to modify the Scene Collection
- `Source Visibility` action allows you to toggle sources on/off (+ multi-action support)
- `Image Settings` action allows to change the settings of an Image source (as an example - think changing your background image with one press)
- `HotkeyTriggerAction` allows you to send Hotkeys directly to OBS (even when OBS is running as Admin)
- `Source Monitor Set` action allows you to set the Monitor Type of an Audio Source (None, Monitor Only, Monitor and Output)
- `Source Mute Toggle` action allows you to mute/unmute Audio Sources.
- `Virtual Camera` action allows you to enable/disable the Virtual Camera from the Stream Deck




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

## Change Log

# New in v2.8
- Support for OBS v28 (with OBS Websocket v5)
- `Instant Replay` action now allows to switch to a specific Scene before starting the replay 🔥
- Stability and performance improvements

# New in v2.1
***NOTE: This version requires upgrading to OBS Websocket v4.9 (!!) see: https://barraider.com/obs***
- :new: `HotkeyTriggerAction` allows you to send Hotkeys directly to OBS (even when OBS is running as Admin)
- :new: Auto-Reconnect feature will try to connect to OBS every few seconds automatically .
    - Keys will now show an indicator if plugin is not connected to OBS
- `Instant Replay` key allows enabling/disabling the Replay Buffer (long pressing the key) *even when you're not streaming or recording.*
    -  `Instant Replay` key now shows a visual indicator if the Replay Buffer is enabled
- `Toggle Filter` action now shows a visual indicator if the filter is enabled or disabled
- `Toggle Filter` action can now toggle filters on ***Sources*** (just like Scenes)
- `Set Transition` action now allows to set the transition duration

- Multiple improvements to the `Recording Toggle` action:
    - Recording action now supports to Pause/Resume recordings
    - Recording action now supports multi-actions
    - Recording Action now allows to customize the recording indicator
- `Stream Toggle` Action now allows to customize the streaming indicator
- `Source Volume Adjuster` action now supports -/+1 steps

## New in v1.9
- Fixed issues with `Filter Toogle` not working when scene does not include that source
- Improved load times of `SmartSceneSwitcher` and `Instant Replay` actions
- Stability improvements


## New in v1.8
- New `Set Profile` action allows you to modify the OBS Profile
- New `Set Scene Collection` action allows you to modify the Scene Collection
- New `Source Visibility` action allows you to toggle sources on/off (+ multi-action support)
- New `Image Settings` action allows to change the settings of an Image source (as an example - think changing your background image with one press)
- `Set Transition`/`Set Profile`/`Set Scene Collection` all change color if the active Transition/Profile/Scene Collection matches the one set on the key.
- Added support for custom images to both `Smart Scene Switcher` and `Source Visibility`
- `Source Animation` action now supports looping the animation multiple times

***NOTE: This version requires upgrading to OBS Websocket v4.8 (!!) see: https://barraider.com/obs***


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

# New in v2.5
- Migrated code to use Twitch's new APIs

# New in v2.4
- :new: `Source Monitor Set` action allows you to set the Monitor Type of an Audio Source (None, Monitor Only, Monitor and Output)
- :new: `Source Mute Toggle` action allows you to mute/unmute Audio Sources.
- :new: `Virtual Camera` action allows you to enable/disable the Virtual Camera from the Stream Deck
     - Includes Multi-Action support!
- Added support for customizable icons to many of the actions
- Reworked the setup wizard to clearly state the required Websocket version and give more informative messages in case of errors.
