# Waiter and Busser Trolley Gameplay Fix — Handoff Plan

**Status:** Implemented on 2026-08-28. Keep this document as the design/QA contract for regression testing.

**Implementation result:** Both trolley prefabs now use identity gameplay roots, editable `VisualPivot`, four editable `TraySlot` anchors, and a non-physical `HoldingPoint`. Runtime discovers purchased scene/prefab instances, aligns the trolley handle to an editable bot grip point, batches one to four trays after a short grace period, restores claims/UI on cancellation, and returns the trolley to its authored parking point. The authoring installer is versioned and no longer overwrites version-6 manual trolley edits.

**Purpose:** Give another Codex account enough project-specific context to repair the trolley feature without erasing the user's manual prefab work or breaking the existing waiter/busser fallback workflows.

## Desired Player-Facing Result

- A purchased waiter trolley is visible at its parking point near prepared-food pickup.
- A purchased busser trolley is visible at its parking point near the sink/cleaning area.
- The trolley remains parked while idle. It is not permanently attached to a bot.
- When eligible work begins, the correct bot walks to the trolley, takes it, pushes it upright in front of the bot, loads up to four trays, performs the deliveries or cleanup, and returns the trolley to its parking point.
- The bot uses the existing carrying animation while pushing.
- Trays sit in the four authored `TraySlot` transforms without changing size, turning vertical, colliding with the bot, or floating away from the shelves.
- The system behaves the same in Unity Play Mode, Windows builds, and Android builds.
- All presentation and placement values remain editable in prefabs or the Inspector.

## Preserve These User Changes First

Do not rebuild, revert, reset, or automatically regenerate `WaiterTrolley.prefab` before preserving the current authored result.

The user has manually changed:

- the waiter trolley's visual orientation;
- the waiter trolley's apparent scale relative to the bots;
- all four waiter `TraySlot` positions;
- a new `HoldingPoint` child placed on the trolley handle;
- the `HoldingPoint` MeshRenderer is disabled so it acts as an authoring marker rather than visible geometry.

At the time this plan was written, the waiter prefab also has a root X rotation of approximately `-90` degrees and a root scale of `2`. These values explain the desired appearance in Prefab Mode, but they are unsafe as the long-term gameplay correction because runtime code replaces the trolley root rotation whenever it parks or follows a bot.

Before implementation:

1. Capture Inspector values and Prefab Mode screenshots for the waiter root, `VisualPivot`, `TraySlot1` through `TraySlot4`, and `HoldingPoint`.
2. Preserve the current prefab in Git or make a temporary prefab variant. Do not use destructive Git commands because the working tree contains unrelated user changes.
3. Treat the current waiter appearance and slot placement as the visual reference to preserve, even if its transform hierarchy is migrated.
4. Do not copy the waiter's blue material or `WaiterTrolley` effect onto the busser trolley. The busser must keep its own color/material and `BusserTrolley` effect.

## Current Project Findings

### 1. `HoldingPoint` is not used by gameplay

`Assets/_Project/Resources/Upgrades/WaiterTrolley.prefab` contains the new `HoldingPoint`, but `Assets/_Project/Gameplay/AutonomousService/Lobby/BotTrolleyCarrier.cs` has no serialized `holdingPoint` field and never reads that transform. Adding the cube alone therefore cannot change where a bot holds or pushes the trolley.

The current `HoldingPoint` also has an enabled `BoxCollider`. A hidden authoring marker should not take part in runtime physics because it may push characters, trays, or navigation obstacles. Retain the transform, but remove/disable its collider or convert it to a non-physical gizmo-only marker.

### 2. Runtime placement overwrites a root rotation correction

`BotTrolleyCarrier.FollowOperatorImmediate()` currently does this conceptually:

```text
trolley position = bot-local pushOffset
trolley rotation = bot rotation + pushEulerAngles
```

`ParkImmediate()` similarly replaces the root position and rotation with the parking transform. A visual correction authored on the trolley root is therefore lost at runtime.

The recommended contract is:

- gameplay root: position `0,0,0`, rotation identity, scale `1,1,1` in the prefab;
- `VisualPivot`: owns the cart model's presentation rotation, scale, and grounding correction;
- `TraySlot1..4`: editable tray anchors in stable trolley-local space;
- `HoldingPoint`: editable handle/grip anchor in stable trolley-local space;
- runtime code moves only the gameplay root.

When migrating the waiter prefab, transfer the user's visual correction into `VisualPivot` and reposition the anchors as needed so the trolley looks exactly like the user's current version. Do not simply reset the root and lose the corrected look.

### 3. The busser prefab does not match the new waiter contract

`Assets/_Project/Resources/Upgrades/BusserTrolley.prefab` currently has no `HoldingPoint` and still uses the older orientation/scale/slot layout. It needs the same structural contract as the waiter trolley, while keeping busser-specific material, effect, parking, and balance values.

### 4. Trolleys are purchase-gated runtime objects

`LobbyAutonomousService.ConfigureTrolley()` creates a trolley from:

- `Resources/Upgrades/WaiterTrolley`, or
- `Resources/Upgrades/BusserTrolley`.

It does this only when `EquipmentUpgradeService.IsPurchased(effect)` returns true. If the upgrade is not present in `EquipmentManager.AllEquipment`, has not been purchased in the current save, or purchases have not loaded yet, no runtime trolley is created.

Manually dropping a prefab into `Lobby1` is currently not the authoritative runtime path because the service fields are private runtime references and do not discover an arbitrary scene copy. A scene copy can therefore be visible in editing but never be used by the service. The implementation should retain one clear ownership model and prevent duplicates.

### 5. Batch conditions can make a working trolley appear unused

The trolley workflow starts only if all relevant conditions pass:

- the corresponding upgrade is purchased;
- the waiter/busser role has an active assigned employee;
- `GameDayManager.ServiceActive` is true;
- the runtime trolley has a parking point, tray slots, and renderable visual;
- the bot is idle and its normal hands are free;
- enough eligible, unclaimed trays exist;
- the bot can route to the trolley and later task destinations.

The current waiter prefab has `minimumBatchSize = 1`, while the busser prefab has `minimumBatchSize = 2`. This means the busser deliberately falls back to normal one-tray cleanup when only one dirty tray is ready. If the design requirement is that purchased trolleys are always used for tray work, both should use a minimum of one and a short grace period should collect additional trays up to four.

### 6. Existing debug commands can provide deterministic purchase tests

The PC-only developer console already supports:

- `upgrade(1)` — unlock and purchase the busser trolley;
- `upgrade(2)` — unlock and purchase the waiter trolley.

Use these during validation instead of editing save data by hand. Confirm the upgrade purchase persists after a scene reload and full application restart.

## Recommended Implementation Plan

### Phase 1 — Reproduce Before Changing Assets

1. Open `Lobby1` from the normal Bootstrap/game flow so save, equipment, employees, and day state initialize exactly as they do in a build.
2. Assign an active waiter and busser.
3. Use `upgrade(2)` and confirm whether one waiter trolley instance appears at `WaiterTrolleyParkingPoint` immediately.
4. Use `upgrade(1)` and confirm whether one busser trolley instance appears at `BusserTrolleyParkingPoint` immediately.
5. Record the following once, without per-frame log spam:
   - effect and resource path;
   - whether the upgrade asset was found;
   - whether it is purchased;
   - whether the role bot is active;
   - parking-point resolution;
   - prefab load result;
   - `IsConfigured` result and the reason for failure;
   - final spawned instance position, rotation, and active state.
6. Confirm whether a manually placed scene trolley is creating a duplicate or is simply ignored.
7. Create at least one ready waiter tray and two dirty busser trays. Record which guard prevents a trolley batch if it does not start.

Do not begin with visual guessing. This phase identifies whether the missing trolley is a purchase/save/catalog problem, a spawn/configuration problem, a route problem, or only a batch-threshold problem.

### Phase 2 — Establish a Safe Editable Prefab Contract

Apply this hierarchy to both prefabs:

```text
WaiterTrolley / BusserTrolley        (gameplay root: identity transform)
├── VisualPivot                      (editable model orientation/scale/grounding)
│   └── TrolleyModel
├── TraySlot1
├── TraySlot2
├── TraySlot3
├── TraySlot4
└── HoldingPoint                     (hidden, non-physical grip marker)
```

Implementation requirements:

1. Preserve the user's corrected waiter appearance and tray positions during migration.
2. Move presentation rotation/scale from the waiter gameplay root into `VisualPivot` without changing the visible result.
3. Return the gameplay root to identity so runtime movement does not erase the correction.
4. Keep each tray slot independently editable.
5. Add a `HoldingPoint` to the busser trolley and place it on the equivalent handle location.
6. Keep the waiter and busser materials visually distinct.
7. Make `HoldingPoint` an empty transform, or keep its disabled MeshRenderer only for manual authoring. It must have no enabled solid collider.
8. Add an optional selected-object gizmo for `HoldingPoint` so it remains easy to edit without runtime geometry.

### Phase 3 — Make `BotTrolleyCarrier` Use the Grip Anchor

Update `BotTrolleyCarrier` with serialized, editable references and values rather than hardcoded model-specific numbers:

- `Transform holdingPoint`;
- the existing `visualRoot`;
- the four tray slots;
- an operator grip target or grip offset;
- orientation offset, parking offset, and approach distance;
- optional position/rotation smoothing values, with zero-smoothing support for debugging.

Use a two-anchor alignment contract:

1. The trolley's `HoldingPoint` identifies the physical handle location.
2. The bot supplies the desired hand/grip target. First inspect the existing `WaiterHands.TrayHoldPoint` and `BusserHands.TrayHoldPoint`; reuse them only if they visually match the pushing pose. Otherwise add one editable `TrolleyGripPoint` to each bot instead of guessing humanoid hand bones.
3. Set the trolley's desired facing from the bot plus the serialized trolley orientation offset.
4. Place the trolley provisionally in front of the bot.
5. Translate the trolley root by the exact world-space difference between the bot grip target and the trolley `HoldingPoint`. This keeps the handle aligned even when the visual pivot or cart scale changes.
6. Continue using `AutonomousStaffBot.SetUsingTrolley(true/false)` so the existing carrying animation is active only while the trolley is in use.

The fallback when either grip anchor is missing should be the existing push-offset behavior plus one clear warning. Missing authoring data must not freeze service gameplay.

### Phase 4 — Copy the Working Contract to the Busser Trolley

After the waiter alignment is correct:

1. Give the busser prefab the same root/`VisualPivot`/slot/`HoldingPoint` structure.
2. Match the corrected waiter trolley's overall physical size relative to the bot unless the art requires a deliberate difference.
3. Place the busser tray slots on usable shelf surfaces; do not blindly copy waiter local coordinates if the busser visual pivot differs.
4. Keep `effect = BusserTrolley`, capacity `4`, the busser material, and the busser parking configuration.
5. Validate both empty and fully loaded trolley bounds so trays do not intersect the cart or each other.

### Phase 5 — Fix Spawn, Purchase, and Lifecycle Reliability

Keep exactly one authoritative trolley instance per purchased effect.

Recommended lifecycle:

1. At service initialization, wait until the initial save/equipment load is complete before making the first final purchase decision.
2. Subscribe to `EquipmentManager.PurchasesChanged` and refresh immediately after a purchase or debug purchase.
3. If purchased, resolve or instantiate exactly one trolley, configure it, show it, and park it even when that employee role is temporarily inactive.
4. If not purchased, no trolley should be visible or usable.
5. Distinguish runtime-created objects from scene-authored objects before destroying anything. Never destroy a user's authored scene object merely because a purchase has not loaded yet.
6. Detect duplicate trolley effects and keep one deterministic authoritative instance while logging a single actionable warning.
7. Validate that `WaiterTrolleyParkingPoint` and `BusserTrolleyParkingPoint` exist, are near their intended stations, and have a reachable NavMesh position for the corresponding bot.
8. On scene unload, role deactivation, shift end, or service destruction, release claimed trays, clear the carrying pose, and cleanly return or dispose only the runtime instance owned by the service.

If the project keeps the Resources-spawn model, remove test copies from `Lobby1` after confirming the prefab works. If scene-authored trolley instances are intentionally supported, add explicit serialized scene references or deterministic discovery by `EquipmentUpgradeEffect`; do not mix both ownership styles silently.

### Phase 6 — Guarantee That Purchased Trolleys Are Actually Used

The intended behavior should be explicit:

- Without the upgrade: retain the current single-tray waiter/busser workflow.
- With the upgrade: use the trolley for every eligible tray workflow, while allowing batches of one to four.

Recommended batching behavior:

1. Set the effective minimum batch size to one for both purchased trolley workflows if the requirement is “always use the purchased trolley.”
2. Keep a short, editable batching grace period so the bot can gather additional nearly-ready trays without visibly stalling service.
3. Never wait indefinitely for a full cart. Start after the grace period with the eligible trays already available.
4. Claim all selected trays atomically. If the final valid batch is empty, release every partial claim and return to normal task selection.
5. While a trolley batch owns a tray, suppress the matching manual/bot pickup bubble only through the existing task-claim system. Do not globally disable unrelated table tasks.
6. Load the tray into the authored next free slot, deliver/clean it, compact remaining slots, and never alter the tray's world scale.
7. Return the empty trolley to parking after the batch. It must not remain attached to the bot during bills, cash, card payments, orders, idle movement, or unrelated cleaning.

### Phase 7 — Failure and Retry Safety

Every trolley coroutine needs one cleanup path equivalent to a `try/finally` contract:

- If the route to the trolley fails: release batch claims; do not start the carry pose.
- If a tray pickup fails: release that tray's claim and continue safely with the remaining batch.
- If table/sink navigation fails: detach/release the affected trays at a safe reachable position for retry.
- If the bot or trolley is disabled: release all trays, clear `SetUsingTrolley`, and park when possible.
- If the shift ends during a batch: finish only the allowed closeout work, then return the trolley and clear claims.
- If parking navigation is blocked: the final authoritative park operation may snap the empty trolley to its parking point, but never teleport loaded trays through normal service.
- Never leave a bot permanently `IsBusy`, permanently carrying, or holding a task claim after an aborted batch.

### Phase 8 — Protect Manual Edits From the Authoring Installer

`Assets/_Project/Editor/CardPaymentAndUpgradeAuthoring.cs` currently uses an authoring version and can regenerate trolley structure. A careless version bump can reset the root, `VisualPivot`, and all tray slots to defaults.

Before increasing `TrolleyPrefabAuthoringVersion`:

1. Change the trolley migration to be additive and idempotent.
2. For an existing prefab, find and reference the user's existing `VisualPivot`, tray slots, and `HoldingPoint` rather than recreating or resetting them.
3. Create only missing objects/references.
4. Never reset an existing slot position, visual scale, material, or holding-point transform during an automatic editor load.
5. Only new prefabs may receive default layout values.
6. Bump the session/version key only after the preservation migration is safe.
7. Reopen Unity twice and confirm automatic installation does not change either prefab on the second import.

### Phase 9 — Expand Validation Without Making Tests Rewrite Assets

Update `CardPaymentTrolleyEquipmentSmokeTest.cs` so it validates rather than repairs:

- correct effect for each prefab;
- exactly four non-null tray-slot references;
- one non-null `VisualPivot` and visible trolley renderers;
- one non-null `HoldingPoint`;
- no enabled solid collider on `HoldingPoint`;
- stable gameplay-root transform contract;
- positive usable visual scale and bot-relative bounds;
- correct role-specific material/color;
- upgrade assets exist in `EquipmentManager.AllEquipment` with the expected IDs;
- both parking points exist in `Lobby1`;
- authoring installer can run twice without changing prefab YAML.

Adjust the visual-bounds expectation to the final user-approved cart size. Do not “fix” the user's larger cart by forcing the old `0.75–1.5` height range if that range no longer matches the bots.

## Files Expected to Change During Implementation

Primary files:

- `Assets/_Project/Gameplay/AutonomousService/Lobby/BotTrolleyCarrier.cs`
- `Assets/_Project/Gameplay/AutonomousService/Lobby/LobbyAutonomousService.cs`
- `Assets/_Project/Resources/Upgrades/WaiterTrolley.prefab`
- `Assets/_Project/Resources/Upgrades/BusserTrolley.prefab`
- `Assets/_Project/Editor/CardPaymentAndUpgradeAuthoring.cs`
- `Assets/_Project/Editor/CardPaymentTrolleyEquipmentSmokeTest.cs`

Possible supporting files, only if runtime observation proves they are needed:

- `Assets/_Project/Gameplay/AutonomousService/Core/AutonomousStaffBot.cs`
- `Assets/_Project/Restaurant/Items/WaiterHands.cs`
- `Assets/_Project/Lobby/Roles/BusserHands.cs`
- `Assets/_Project/Scenes/RoleBased/Lobby1.unity` for parking or explicit grip targets
- `Assets/_Project/Office/Manager/EquipmentUpgradeService.cs`
- `Assets/_Project/Office/Manager/EquipmentManager.cs`

Do not modify unrelated UI, card payment, TMP font, skybox, save, customer, or restaurant systems as part of this focused fix unless a reproduced trolley failure directly proves the dependency.

## Verification Matrix

### Asset/Edit Mode

1. Both prefab roots follow the agreed gameplay-root contract.
2. Both visuals are upright and correctly sized beside their bot.
3. Both prefabs expose editable `VisualPivot`, four slots, and `HoldingPoint` references.
4. Each tray preview lies flat and fully supported by a shelf.
5. The hidden holding marker has no gameplay collider.
6. Running the authoring installer twice creates no prefab diff on the second run.

### Waiter Play Mode

1. Before purchase: no waiter trolley is visible; normal one-tray delivery still works.
2. Purchase with `upgrade(2)`: exactly one trolley appears at the waiter parking point.
3. With one ready tray: waiter takes the trolley if “always use” behavior is selected.
4. With four ready trays: all four occupy separate slots and reach their correct tables.
5. The trolley stays upright, in front of the waiter, at the corrected scale.
6. The handle aligns with the bot's grip target throughout turns and movement.
7. The waiter returns the empty trolley and performs later non-food tasks without it.

### Busser Play Mode

1. Before purchase: normal one-tray cleanup still works.
2. Purchase with `upgrade(1)`: exactly one busser trolley appears at its parking point.
3. Dirty trays load flat into separate slots.
4. The busser pushes the upright trolley to booths and then the sink.
5. All collected trays are disposed/registered once, with no duplicates or lost claims.
6. The empty trolley returns to parking and is released from the bot.

### Lifecycle and Regression

1. Purchase during preparation, then start the shift.
2. Purchase, reload `Lobby1`, and confirm one trolley returns from saved purchase data.
3. Deactivate/reactivate the employee role without duplicating or losing the purchased trolley.
4. End a shift during a batch and confirm cleanup completes without stuck tasks.
5. Obstruct a route temporarily and verify retry/claim cleanup.
6. Verify waiter bills, cash/card payments, orders, busser single-tray fallback, and task UI remain functional.
7. Confirm no repeated trolley, NavMesh, missing-reference, duplicate-claim, or animator warnings.

### Build Parity

Run the same purchase, one-tray, four-tray, scene-reload, and shift-end checks in:

- Unity Editor Play Mode;
- Windows build;
- Android build at the target thesis device aspect ratio.

The feature is not complete if it only works in the Editor.

## Definition of Done

The trolley fix is complete only when all of the following are true:

- the user's corrected waiter look is preserved;
- the busser trolley has an equivalent editable and correct setup;
- both purchased trolleys reliably appear in normal game flow;
- waiter and busser visibly use their own trolley for eligible tray work;
- the trolley is upright, correctly scaled, in front of the bot, and aligned through `HoldingPoint`;
- up to four trays remain flat and correctly placed;
- both trolleys park when idle and are never permanently attached;
- purchase/save/role changes create no missing or duplicate instances;
- all failure paths release claims, trays, animation state, and bot busy state;
- automatic authoring no longer overwrites manual prefab edits;
- validation passes in Editor, Windows, and Android builds without new gameplay errors.

## Explicit Non-Goals

- Do not redesign the equipment shop UI.
- Do not change trolley prices or unlock days.
- Do not redesign global staff navigation.
- Do not rewrite waiter/busser service architecture beyond the focused trolley lifecycle.
- Do not change card payment behavior.
- Do not introduce a general-purpose vehicle framework.

Keep the solution focused, prefab-editable, deterministic, and safe for the already-working single-tray workflows.
