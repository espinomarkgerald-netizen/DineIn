# Editor / Windows / Android parity

## Editable assets

- `Assets/_Project/Resources/UI/LobbyHUD.prefab`: combined HUD, including the saved pause settings controls.
- `Assets/_Project/Gameplay/UI/Resources/LobbyPauseMenu.prefab`: standalone pause fallback. Edit saved child RectTransforms, Images and TMP labels.
- `Assets/_Project/Resources/UI/MultiplayerReadyPrompt.prefab`: team readiness card, roster, ready/cancel/pause controls.
- `Assets/_Project/Resources/UI/MobileUISettings.asset`: touch and visible-control minimums; optional Editor Play Mode mobile preview.
- `UIScreenSafeArea`: saved anchor baselines for edge controls. New scenes can use **Dine In > Polish > Author Safe Areas in Open Scene**, then save.
- `Assets/_Project/Restaurant/CookingAssets/Batch/World Outline.mat`: black outline width/color. PC and Mobile renderer assets retain their outline passes.

**Dine In > Polish > Save Build Scene UI Layouts** applies the larger kitchen layout and scene safe areas. Save existing scene edits first; this command intentionally refuses dirty scenes. It preserves other gameplay objects and restores the previous scene setup.

## Checks before every build

1. Run **Dine In > Validation > Run Build Parity Regressions**. This checks the active Build Profile's scene list, shader references, editable pause/readiness assets, idempotent Resume, and readiness votes for 2–4 participants. It does not connect to Photon.
2. Use the same saved Accessibility settings when comparing Editor and player. Reduced Motion intentionally suppresses animations. Windows and Editor have separate PlayerPrefs. Development players print these values at startup; do not delete a player's save to reset animation preferences.
3. Verify Bootstrap, both menus, Lobby1, Lobby1Tutorial, Lobby1 Multiplayer, Lobby2 and RestockScene are in the selected profile. Build callbacks also reject missing UI input modules in the actual built scenes.

## Actual build acceptance (requires devices/accounts)

- Windows: fresh install and existing save; campaign to each restaurant; tutorial accept/decline/skip; pause/resume repeatedly; leave/rejoin.
- Android: smallest supported phone, tablet and notched landscape screen. Check labels, hotbar dragging, basket collection, discard, station switching and ticket slides. Test app background/resume and changing resolution/orientation where supported.
- Match Reduced Motion OFF between Editor and each player. Check button feedback, kitchen pickup animations, cooking effects, order tickets and complaint transitions. Then test Reduced Motion ON independently.
- Multiplayer: 2, 3 and 4 distinct signed-in accounts. Each player opens/closes their own pause menu; other players keep moving. Everyone readies; withdraw during countdown; cancel as host; disconnect/rejoin a guest; leave as host; return to menu. Check remote players cannot control local UI.
- Compare black world edges and white hover/interact outlines on food, furniture and characters in both builds. Check all used quality levels. A shader-reference check is not a GPU render test.
- Confirm local/cloud progression and balances remain unchanged by UI transitions. These changes do not modify save, economy or reward calculations.

Passing the code/prefab checks is not a substitute for the Windows, Android and live four-account acceptance run.
