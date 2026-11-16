# aion2-remote-play v6.0

A Windows application that redirects software-injected keyboard and mouse input through virtual HID devices. Intended for remote-play setups where an application accepts pointer movement but does not process ordinary software-injected button/key events.

## Setup and use

1. Set up remote access to Windows. The project's tested streaming combination is [Sunshine](https://github.com/SunshineStream/Sunshine) on the host with [Moonlight](https://github.com/moonlight-stream/moonlight-qt) on the client.
2. Install [.NET Desktop Runtime 10 x64](https://dotnet.microsoft.com/en-us/download/dotnet/10.0) and [HIDMaestro 2.1.0](https://github.com/hifihedgehog/HIDMaestro/releases/tag/v2.1.0), including the driver using its official instructions, on Windows. The bridge does not install drivers or modify Sunshine.
3. Extract the entire user ZIP. Close older bridge instances, then run `Aion2RemotePlay.exe` on the Windows host in the same interactive session as the remote input. Accept administrator elevation.
4. Click **Turn on**, release your keys, and wait three seconds. The status changes to **Active: mouse and keyboard**. Clicking again turns it off; clicking during the countdown cancels activation.
5. For the first trial, open **Advanced**, select **Test: 90 seconds**, and test in Notepad before opening a game. The normal mode runs until switched off. Keys held during activation cancel it: release them and try again.

**Emergency OFF: Ctrl+Alt+Pause.** If Pause is unavailable, select Ctrl+Alt+ScrollLock in Advanced before enabling. Normal window close also turns the bridge off and submits neutral keyboard/mouse states. Turn the bridge off before disconnecting your remote session; automatic disconnect detection is not implemented.

The UI supports English and Russian. Advanced contains duration, emergency shortcut, counters, a test field and access to logs. No Sunshine rebuild, USB/IP server or additional hardware is required.

## Input pipeline

```text
Remote client → remote-access host on Windows
    → Windows software input → bridge low-level hooks
    → queue → sender thread → HIDMaestro SDK → virtual HID devices
```

The bridge installs `WH_KEYBOARD_LL` and `WH_MOUSE_LL` hooks and selects supported events marked `LLKHF_INJECTED` / `LLMHF_INJECTED`. When enabled, it queues each supported event and suppresses the original software event to avoid duplicate delivery. A separate worker maintains held keyboard/button state and submits HID reports. Ordinary keys use an NKRO descriptor; media keys use a consumer report.

Pointer movement stays with the original remote-access software. The bridge does not transport video, audio or network traffic and does not replace a remote-access server. Selection is based on the injected flag, not the source process: input from other software in the same session may also be redirected. Unknown codes pass through as normal Windows input.

## Compatibility and verification

**The project's tested configuration is Sunshine/Moonlight with Aion 2.** The previous bridge version was user-tested in ordinary Windows applications and the game. V6 keeps the input-redirection approach, simplifies the UI and removes experimental mouse-pair waiting. V6 has passed compilation and built-in logic tests; the new build has not yet received a separate interactive game test.

**The bridge is not tied to Sunshine.** Other remote-access software is expected to work if it generates equivalent software events visible to these hooks with the injected flag in the same interactive Windows session. This is an architectural expectation, not verified support for every remote client. Driver-based input, different event-delivery paths and separate RDP sessions may behave differently. RDP, AnyDesk, RustDesk and Steam Remote Play have not been separately tested with this bridge. Client OS alone does not determine compatibility; the host-side input delivery path does.

Common keys, combinations, modifiers, function keys, arrows and numpad are mapped. Mouse support includes left/right/middle, side buttons 4/5 and scrolling. Prior interactive tests confirmed common keys/combinations, five mouse buttons and vertical scrolling. Horizontal scrolling and media/browser codes are implemented but unverified in game. The code has no six-normal-key cap, but end-to-end input above six simultaneous normal keys is unverified. Not every layout, vendor/Fn feature or game has been tested.

Requires Windows x64 supported by .NET 10. The previously tested host reported build 26200; Windows 10 was not separately confirmed. Compatibility with every game/security update is not guaranteed.

## Left + right and middle-click

**The bridge never converts left+right into middle-click.** Left/right edges are processed independently, with no artificial pair delay, grouping or third-button emulation. The real middle button remains independent.

If the client or remote-access software already converts left+right into middle-click, the bridge receives a middle event and cannot reliably recover the original buttons. Disable middle-button emulation at the input source. For example, on X11/evdev inspect `xinput list-props "YOUR MOUSE NAME"`; if `Evdev Middle Button Emulation` exists and is `1`, temporarily disable it with `xinput set-prop "YOUR MOUSE NAME" "Evdev Middle Button Emulation" 0`. Restore `1` to undo; the setting may reset after login. This command does not apply to other drivers or Wayland. The bridge does not automatically modify client system settings.

## Logs and recovery

Logs are stored in `%LOCALAPPDATA%\Aion2RemotePlay\logs`. By default they record operational state/errors; detailed key-code logging is off. It can be explicitly enabled only in the 90-second test. Error messages may include local paths; review logs before sharing. Personal logs and machine-specific settings are excluded from the distributions.

If startup fails, check that all archive files are present, Desktop Runtime 10 and the HIDMaestro 2.1.0 driver are installed, and elevation was accepted. Do not run v5/v5.1/v6 together. A submission error or queue overflow requests OFF and neutral input reports; this does not independently verify Windows input state. The shortcut, button and normal close do not guarantee recovery from a process crash or driver failure.

## Source and credits

The separate GitHub archive contains `src/`, `build.ps1` and build instructions. The bridge source is MIT. It uses HIDMaestro 2.1.0, C#/.NET 10 and Windows Forms; Sunshine/Moonlight is the tested streaming stack. Codex assisted development and documentation. Upstream notices are preserved in `licenses/` and `THIRD_PARTY_NOTICES.md`. This is an independent project, without an upstream/game-developer support commitment.
