# Character customization regression fixes — 2026-09-28

## Proven causes and scoped corrections

### Lobby1 navigation

- Existing Editor log shows `[PlayerMovement] Manager is not on an active NavMesh`, followed by the reported task warning. The rejecting branch is `!agent.isOnNavMesh` in `TryStartPath`, before destination/path validation.
- Every Lobby1 NavMeshSurface had a null data reference. The existing bootstrap intentionally does not build navigation at runtime.
- Git history identifies the exact removed reference in `eef8d2f8` (before customization): the `NavMesh` object's data was changed from `a4ac8b90dfa28d9458000852c17d5a6b` to null.
- Restored only that surface's reference to `Assets/_Project/Scenes/RoleBased/Lobby1/NavMesh-NavMesh.asset`. No rebake, agent teleport, reachability bypass, task/movement edit, or global navigation reset.
- Temporary appearance instances retained root position, rotation, scale and root-motion setting after Apply/rebind/evaluation. Lobby2 retains its separate Fast Food navigation asset.

### Carry sockets / trolley

- CharacterAppearance previously copied legacy bone-relative bind-pose offsets onto the replacement avatar. Bone axes differ. Under the same CarryIdle clip, Waiter's tray changed from actor-local `(1.610, 1.520, 2.602)` to `(-1.536, 1.434, -2.743)`. The trolley grip likewise moved behind the actor and its orientation reversed.
- Existing trolley routes already put trays on `BotTrolleyCarrier.traySlots` through `TryAttach`; ordinary single-tray tasks use WaiterHands/BusserHands. That ownership and both capacities remain unchanged.
- Calibrated existing socket references against the same existing CarryIdle pose on both avatars. Inspector-editable customized bone position/rotation is serialized on the existing appearance binding (32 entries across prefabs/scenes, including inherited player bindings).
- During customization the actual gameplay socket objects live under the gameplay root, not replaceable appearance geometry. Cached bones update their poses in LateUpdate before trolley presentation. Hair/outfit/body selection does not replace sockets. Restoring a legacy appearance restores original parents and local transforms. Root-level sockets remain untouched.
- If adding new skeleton-attached appearance bindings, run the focused `calibrate_carry_sockets.cs` authoring step after assigning them. Do not copy offsets between unrelated bind-pose bases.

### Menu HUD and customization

- Reproduced at supported 115% UI scale: Back's center-minus-353 placement leaves screen bounds, Customize's centered offset crosses the right edge, and the center-offset currency bar overlaps the right-anchored Shop. The wallet parent could even have negative width because of its fixed -700 size delta.
- Back/Shop/Customize now use their screen-edge anchors. Wallet fills its existing parent; MoneyUI is top-right anchored beside Shop, with currency labels/controls inside it. Updated the existing safe-area baseline. No new canvas or duplicated controls; existing Customize visibility/interactability capture/restore is retained.
- Selected/neutral tabs now use matching blue/grey depth-border artwork and readable navy labels.
- Model cards are 112×122, bounded to 2–3 columns in tested landscape layouts, with 12-point two-line labels and measured vertical padding. Swatches remain 58×64. Current preview composition, fixed footer, scrolling and rotation ownership remain unchanged.

## Files changed

- Runtime: `Assets/_Project/Player/Customization/CharacterAppearance.cs`, `AppearanceCustomizationPanel.cs`.
- Prefab socket calibration: `Assets/_Project/Player/Manager.prefab`, `Assets/Resources/ManagerMultiplayer.prefab`.
- Scene socket calibration: `Assets/_Project/Scenes/RoleBased/Lobby1.unity`, `Lobby2.unity`, `Lobby1 Multiplayer.unity`, `Lobby2Tutorial.unity`, and `Assets/_Project/Scenes/TutorialScenes/Lobby1Tutorial.unity`. Tutorial changes are socket calibration only; no lesson/sequence changes.
- HUD/card authoring: `Assets/_Project/Scenes/NewMenu/NewGameMenu.unity`.
- Existing test alignment: `Assets/_Project/Editor/MobileUILayoutRegressionTest.cs`, `tools/CharacterAppearance/verify_application.cs`.
- Focused authoring/inspection/check scripts and these notes under `tools/CharacterAppearance/`.

## Checks actually performed

- Unity script compilation completed without errors; `git diff --check` passed.
- Edit-mode static navigation queries: Manager spawn samples and eight sampled task paths each in Lobby1 and Lobby2 succeeded using their existing baked assets. Temporary registrations were removed afterwards.
- 42 carry-pose assertions: both bodies, Waiter/Busser/Manager, Idle/CarryIdle/WalkingCarry; calibrated CarryIdle matches original socket position/orientation, tray/trolley anchors stay in front, gameplay-root socket ownership preserved.
- Existing 110 Edit-mode appearance pose samples passed, including legacy attachment restoration and retained gameplay references.
- Existing 267 scale/hat assertions passed (adult normalization, legacy renderer hiding/restoration, 14 hairstyle choices × 4 headwear choices).
- 190 isolated UI assertions passed over 16:9, 4:3 and 20:9 at 85%, 100% and 115% UI scale: HUD bounds, currency/Shop separation, card columns, label overflow, compact swatches, Open/Cancel state restoration (including pre-hidden/disabled controls). Menu travel is isolated out of this UI-only test.
- Inspected generated Edit-mode normal-menu and customization renders. Normal-menu render uses the authored inactive-animation preview, not a live gameplay/menu animation run.

## Preserved and NOT runtime verified

No edits to movement/task logic, trolley inventory/capacities/routes, employee generation, saves/migrations, cloud/Photon synchronization, recipes, adult scale configuration, hair/hat compatibility rules, RestaurantSelector animations, or preview rotation code. No save reset, build, Play Mode walkthrough or desktop automation.

These are compile/Edit-mode checks, not proof of runtime gameplay. Manually verify normal Lobby1 task arrival/completion; Lobby2 trolley pickup, delivery, cleaning and parking while walking/turning; and Customize Apply/Cancel plus menu travel on desktop/touch. Network/cloud and restart persistence were not rerun in this pass.
