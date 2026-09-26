# Pause and developer settings

## Editable assets

- `Assets/_Project/Resources/UI/LobbyHUD.prefab` — main pause branch, SettingsContent tabs, rows, scroll views and footer.
- `Assets/_Project/Gameplay/UI/Resources/LobbyPauseMenu.prefab` — matching standalone fallback.
- `Assets/_Project/Resources/UI/PlayerSettings.prefab` — shared existing SettingsManager with the project's music/SFX mixer references.
- `Assets/_Project/Resources/UI/DeveloperSettings.prefab` — F10 window, existing action list, numeric value field, confirmation and output.

Open either pause prefab in Prefab Mode and enable PauseOverlay to preview it. Save the overlay closed. Each page can be enabled independently for editing; AUDIO is the saved default. PauseSettingsPanel exposes tab/page/row references, colors, row height and display confirmation timeout. All visuals are saved, not built at runtime. The old inactive settings controls are retained for recovery; the new panel exclusively handles settings.

## Implemented

- Audio: Master, Music, SFX, Mute All. Master/mute use AudioListener; music and SFX use the existing exposed mixer parameters across menu and gameplay mixers.
- Controls: camera drag-pan multiplier, pinch/scroll zoom multiplier, invert zoom, reset controls. Both gameplay camera implementations use these values.
- Graphics: project quality presets, desktop resolution and fullscreen/windowed choice, desktop VSync, FPS cap. Display changes need Apply and Keep Display within 15 seconds; otherwise they revert. Closing the pause menu during confirmation also reverts.
- Settings UI scale: 100–125%, enlarging scrollable settings rows and labels only, without changing HUD/camera/other-menu transforms.
- Accessibility: existing Large Text, High Contrast and Reduced Motion preferences, tutorial/Big Boss dialogue reveal speed, reset. Essential basket-lift/cooking-world animations are not disabled by Reduced Motion.
- Developer window: existing validated actions through a dropdown and value field; save/currency/reset actions require confirmation. Existing PlayFab authorization is preserved. Windows F10 toggles it. The project's existing authorized Android DEV-button workflow remains; no new public mobile entry point was added.
- Tutorial pause: pause stays above tutorial focus/dialogue layers, works at timeScale 0, restores the prior time scale, blocks click-through dialogue input and defers tutorial advancement until resume. Existing multiplayer pause remains local.

## Deliberately unavailable

- Ambience: there are no ambience mixer groups or separately identified ambience sources. No non-functional slider is shown. Add an Ambience group with an exposed volume parameter and identify its sources before wiring a separate channel.
- Edge pan: neither gameplay camera implements edge panning.
- Color vision mode: no existing clean implementation to reuse; no cosmetic/no-op option was added.
- Android resolution/window mode/VSync: managed by the platform, so desktop-only rows are hidden.

## Validation / manual checklist

Run **Dine In > Validation > Validate Tabbed Pause Settings** and **Run Build Parity Regressions** in Edit Mode. Checks cover authored references, tabs, callbacks, preference reloads, resets, developer open/close and preservation of timeScale 0/0.5/1. Tests restore the preferences and quality values they touch. Native AudioMixer gain writes are not supported in Edit Mode.

Manual acceptance still required:

1. Lobby1 and Lobby2: open every tab; change each setting; restart and confirm persistence. Listen to music/SFX independently, Master at zero, and Mute All on/off.
2. Check pan, pinch/scroll zoom and inversion; reset controls. Check dialogue speed and supported accessibility styles.
3. Windows: Apply resolution/window mode, let it time out, then Apply and Keep Display. Test VSync and FPS cap. Editor display changes do not emulate a standalone player window.
4. Windows: press F10 twice; verify the authorized developer window opens/closes. Do not use progression/currency actions on a production save merely to test the UI.
5. Tutorials: pause during dialogue, focused actions, camera guidance, physical restocking and practice; resume at the same step. Also test an already-frozen tutorial/results prompt.
6. Multiplayer: each participant pauses/resumes while the other continues; open pause from readiness; return to menu.
7. Android landscape phone/tablet/notch: tabs and footer stay accessible, rows scroll, sliders/toggles/dropdowns respond, large text/menu scale remain readable. Native visual capture was unavailable during this implementation; perform this visual pass in-game.

No standalone build, device run, live multiplayer session, or destructive developer action was executed for this change.
