# Waiter and Busser Trolley Gameplay Recovery — Implementation Handoff

**Status:** PLAN ONLY — the current trolley feature does not pass gameplay acceptance.

**Date audited:** 2026-08-28

**Scope:** Repair trolley ownership, bot pickup, visual grip alignment, four-tray batching, tray state transitions, recovery, and Editor/build parity. Do not redesign unrelated gameplay or UI.

**Important:** This document replaces the earlier handoff that described the trolley feature as implemented. Infrastructure exists, but the feature is not complete: the busser is visibly misaligned and still cleans trays one at a time, while the waiter has not been observed using its trolley.

## Instructions for the Next Codex Account

1. Read this entire file before editing.
2. Inspect `git status` and preserve every user change. Do not reset, revert, regenerate, or overwrite dirty assets.
3. Treat findings marked **confirmed** as current source/prefab facts. Treat runtime-only items as tests to prove, not assumptions.
4. Execute the implementation phases in order. Do not begin by adjusting random scene offsets.
5. Keep every visual value editable in prefabs/Inspector. Do not hardcode trolley model, grip, slot, or parking coordinates in gameplay code.
6. Do not report completion after compilation alone. The feature is complete only after the deterministic waiter and busser batch tests in this document pass in Play Mode and a standalone build.
7. Preserve the normal one-tray workflows as the fallback for an unpurchased trolley, a single eligible tray, or a genuinely unrecoverable trolley route.

## Player-Facing Contract

### Idle behavior

- A purchased waiter trolley is parked near prepared-food pickup.
- A purchased busser trolley is parked near the sink/dirty-tray work area.
- Trolleys are equipment, not permanent bot attachments.
- An idle trolley is upright, empty, visible, and left at its authored parking point.
- Bots do not carry it while handling bills, cash, card payments, orders, takeout bags, or unrelated tasks.

### Waiter trolley behavior

1. Two or more valid prepared trays become ready within a short batching window.
2. The waiter reserves up to four valid trays as one batch.
3. The waiter walks to the trolley, reaches its accessible approach point, takes the handle, and starts the existing carrying/pushing animation.
4. The trolley stays upright and in front of the waiter; its handle aligns with the waiter's authored grip.
5. The waiter pushes it to the food pickup point(s) and places every reserved tray flat into a different authored slot.
6. The waiter visits each correct table and unloads the matching tray.
7. The waiter returns the empty trolley to parking and releases it.
8. If exactly one tray is ready after the grace window, normal one-tray delivery is allowed. The upgrade must not make one order slower.

### Busser trolley behavior

1. Two or more dirty trays become ready within a short batching window.
2. The busser reserves up to four valid dirty trays as one batch.
3. The busser walks to the trolley, takes the handle, and starts the existing carrying/pushing animation.
4. The trolley stays upright and in front of the busser; the busser must not appear beside, inside, or behind it.
5. The busser visits source tables, loads one dirty tray into each free slot, and does not switch to one-by-one sink trips while the batch is valid.
6. After collecting the batch, the busser makes one sink trip and unloads/disposes all trays exactly once.
7. The empty trolley returns to parking and is released.
8. If exactly one dirty tray remains after the grace window, normal one-tray cleanup is allowed.

### Capacity behavior

- Capacity is four.
- With five or more eligible trays, the first route carries four. Remaining trays are reevaluated after return.
- A trolley batch never steals a tray already owned by the player or another bot.
- A reserved tray does not continue showing its pickup bubble.
- One invalid tray must not corrupt the rest of a valid batch.

## Current Status — Audited 2026-08-28

| Area | Current state | Evidence / impact |
|---|---|---|
| Upgrade data and resource prefabs | Present | Separate waiter and busser resource prefabs and purchase effects exist. |
| Runtime trolley carrier | Partly implemented | `BotTrolleyCarrier` supports parking, ownership, four slots, attachment, release, and return. |
| Batch coroutines | Partly implemented | Waiter delivery and busser cleanup batch coroutines exist in `LobbyAutonomousService`. |
| Runtime result | Failing | User sees the busser mis-hold the cart and continue one-by-one cleanup; waiter use has not been observed. |
| Bot grip authoring | **Confirmed broken** | Both hands scripts expose `trolleyGripPoint`, but both bot prefabs omit a serialized `TrolleyGripPoint` and silently use `TrayHolder`. |
| Handle alignment math | **Confirmed incomplete** | Runtime aligns handle position but derives root rotation from bot rotation plus an offset; it does not solve full grip-anchor rotation. |
| Busser holding marker | **Confirmed unsafe** | The dirty busser prefab contains an enabled, non-trigger `BoxCollider` on `HoldingPoint`. |
| Tray slot authoring | **Confirmed suspicious/invalid** | Current slots use scale `2`, `-90°` rotation, and two slots have negative local Y values. These are not neutral attachment anchors. |
| Batch threshold | **Confirmed contradictory** | Prefabs serialize `minimumBatchSize: 1`, `ConfigureAuthoring()` forces `1`, while the field default is `2`. |
| Waiter task priority | **Confirmed starvation risk** | Trolley batching is checked after several takeout/payment tasks, including starting a new takeout order. |
| Waiter candidate predicate | **Confirmed inconsistent** | Candidate selection is weaker than execution validation, so claimed trays can be rejected after a route starts. |
| Tray interaction lifecycle | **Confirmed split path** | Player pickup updates `FoodTrayInteractable`/`TrayPickupQueue`; direct bot/trolley attachment bypasses the same atomic transition. |
| Failure visibility | **Confirmed masked** | A route failure starts a retry cooldown and permits old one-tray service, making a broken batch look ignored. |
| Static hands lookup | **Confirmed fragile** | Autonomous selection uses global hands singletons instead of the hands component belonging to the assigned bot. |
| Static asset smoke test | Incomplete | It enforces batch size `1` and does not validate bot grip references, slot usability, or actual batch behavior. |
| Parking edit | User-owned; preserve it | `BusserTrolleyParkingPoint` has been manually moved in `Lobby1`. Validate it instead of restoring an old coordinate. |

The infrastructure is **present but not gameplay-complete**. The batch state, physical trolley ownership, tray lifecycle, and visual grip alignment must be repaired as separate contracts and tested together.

## Confirmed Root Causes

### 1. The bots do not have dedicated trolley grip targets

Relevant files:

- `Assets/_Project/Restaurant/Items/WaiterHands.cs`
- `Assets/_Project/Lobby/Roles/BusserHands.cs`
- `Assets/_Project/Lobby/Assets/Waiter/Waiter.prefab`
- `Assets/_Project/Lobby/Assets/Busser/Busser.prefab`

Both scripts contain an editable `trolleyGripPoint`, but the role prefabs omit the reference and omit a `TrolleyGripPoint` object. The property silently falls back to `TrayHoldPoint`, which was authored for a carried tray rather than two-handed trolley pushing.

**Required correction:** Create a dedicated `TrolleyGripPoint` transform in each bot prefab, align it with the existing carrying animation's two-hand pose, assign it to the matching hands component, and make missing grip data a development configuration failure instead of a silent visual fallback.

### 2. Position is aligned, but orientation is not solved anchor-to-anchor

Relevant file:

- `Assets/_Project/Gameplay/AutonomousService/Lobby/BotTrolleyCarrier.cs`

`FollowOperator()` derives trolley rotation from the bot plus `pushEulerAngles`, then translates it so `HoldingPoint` reaches the bot grip. This can align a point while leaving the cart rotated incorrectly.

**Required correction:** Solve the complete trolley root pose:

```text
target root rotation = desired grip world rotation
                     * inverse(HoldingPoint local rotation)
                     * editable rotation correction

target root position = desired grip world position
                     - target root rotation
                     * scaled HoldingPoint local position
```

The gameplay root must stay scale `1,1,1`; presentation scale belongs under `VisualPivot`.

### 3. The busser handle marker participates in physics

The current busser prefab contains a solid `BoxCollider` under `HoldingPoint`. It can push the bot, trays, cart, or environment despite being visually hidden.

**Required correction:** `HoldingPoint` must be an empty transform with local scale `1,1,1`, no MeshFilter, MeshRenderer, Rigidbody, or enabled collider. Draw an editor gizmo from `BotTrolleyCarrier` instead.

### 4. Tray anchors are not neutral attachment points

Both prefabs contain scaled and rotated slot transforms, including lower slots with negative local Y. Reparenting trays to these anchors can make them vertical, wrongly sized, below the cart, or detached from shelves.

**Required correction:** Re-author all four slots in Prefab Mode:

- local scale exactly `1,1,1`;
- tray-facing rotation that leaves an actual food tray horizontal;
- two lower-shelf and two upper-shelf positions inside trolley bounds;
- no tray-to-tray overlap;
- no dependence on the imported model transform;
- visible dummy tray previews during authoring, removed/disabled for the runtime prefab.

Do not guess final numbers in YAML. Calibrate against the actual tray prefab and the user's chosen trolley size.

### 5. The batching policy contradicts itself

`BotTrolleyCarrier` defaults to two, while the authoring method and prefabs force one.

**Required policy:**

- minimum trolley batch: `2`;
- capacity: `4`;
- editable grace window: approximately `0.5–1.0` real-time seconds;
- one tray after the window: original one-tray hands workflow;
- two to four: trolley is preferred;
- more than four: oldest four valid trays, then reevaluate.

### 6. The one-tray fallback hides trolley failures

When trolley acquisition fails, the system applies a cooldown and lets one-tray service continue. This prevents a frozen shift but also hides the defect.

**Required correction:** Keep the production fallback, but record one actionable failure reason and expose current trolley state in the Inspector. A configured, purchased trolley with two or more eligible trays must not fail silently.

### 7. Trolley pickup bypasses the complete tray transition

The player pickup route informs `FoodTrayInteractable` and `TrayPickupQueue`; trolley attachment directly reparents the tray and disables colliders. This can leave a tray logically pickable, queued, or showing stale UI while physically on the trolley.

**Required correction:** Introduce one atomic bot-pickup API on `FoodTrayInteractable` and use it for autonomous hands and trolley pickup. It validates, unregisters the queue owner, hides local UI, enters staff-carried state, and supports rollback.

## Required Runtime State Machine

| State | Meaning | Allowed exit |
|---|---|---|
| `ParkedIdle` | Empty trolley at parking, no owner | `Reserved` |
| `Reserved` | One bot owns trolley and batch jobs | `Acquiring`, `Recovery` |
| `Acquiring` | Bot walks to reachable trolley approach | `Collecting`, `Recovery` |
| `Collecting` | Trolley follows bot; trays load into slots | `Transporting`, `Recovery` |
| `Transporting` | Waiter goes to tables or busser goes to sink | `Unloading`, `Recovery` |
| `Unloading` | Trays leave slots and complete once | `Returning`, `Recovery` |
| `Returning` | Empty trolley and bot return to parking | `ParkedIdle`, `Recovery` |
| `Recovery` | Claims, UI, tray state, animation, busy state, and ownership restore | `ParkedIdle` or safe fallback |

Expose current state read-only in the Inspector. Keep it inside `BotTrolleyCarrier` plus a focused batch job; do not create a general vehicle framework.

## One-Pass Implementation Order

### Phase 0 — Preserve and reproduce

1. Record `git status`; do not touch unrelated dirty files.
2. Preserve the user's `Lobby1` busser parking edit and trolley sizing/orientation.
3. Start through normal Bootstrap/game flow with active waiter and busser.
4. Confirm both upgrades are purchased using existing developer commands or an isolated test save.
5. Reproduce four prepared trays and four dirty trays separately.
6. Record once per attempt:
   - purchase detected;
   - assigned bot/hands resolved;
   - trolley instance resolved/configured;
   - eligible tray count and rejection reasons;
   - batch size claimed;
   - approach/path result;
   - `BeginUse` result;
   - attached count;
   - completion or recovery reason.

### Phase 1 — Repair prefab authoring data

1. Keep existing `Resources/Upgrades/WaiterTrolley` and `BusserTrolley` paths.
2. Keep each gameplay root at identity and scale `1,1,1`.
3. Keep art rotation, size, and grounding under `VisualPivot`.
4. Convert both `HoldingPoint` objects to non-physical unit-scale empty transforms.
5. Re-author all four tray slots with unit scale and flat dummy-tray validation.
6. Preserve waiter blue and busser role-specific material/color.
7. Add and assign `TrolleyGripPoint` in both bot prefabs.
8. Position each grip where the existing carrying animation's hands meet the handle.
9. Validate trolley and bot together in Prefab Mode.

Do not change the user's parking coordinates until a path test proves a problem. Visual parking and walkable bot approach are separate.

### Phase 2 — Make grip alignment deterministic

In `BotTrolleyCarrier`:

1. Replace point-only alignment with complete anchor-to-anchor position/rotation solving.
2. Keep serialized corrections for grip position/rotation, follow smoothing, parking offsets, and NavMesh approach.
3. Resolve grip from the actual assigned bot's hands component.
4. In development, reject `BeginUse` with one clear error when the dedicated grip is missing; retain safe one-tray fallback.
5. Start carrying animation only after the bot reaches and owns the trolley.
6. Stop it before releasing the trolley after parking/recovery.
7. Draw handle, desired grip, approach, and tray-slot gizmos.

### Phase 3 — Use the assigned bot, not global hands singletons

In `LobbyAutonomousService`:

1. Cache `WaiterHands` from the active waiter bot and `BusserHands` from the active busser bot.
2. Replace trolley checks using `WaiterHands.Instance` / `BusserHands.Instance`.
3. Refresh when employees activate, deactivate, respawn, or change roles.
4. Never let a manager/player hands component satisfy autonomous bot requirements.

### Phase 4 — Centralize tray eligibility and pickup state

1. Use one canonical delivery predicate during scanning, claiming, collection, and delivery.
2. Use one canonical cleanup predicate during scanning, claiming, collection, and sink cleanup.
3. Add a minimal atomic staff-pickup contract to `FoodTrayInteractable`:
   - validate expected mode and claim owner;
   - unregister `TrayPickupQueue` when applicable;
   - hide only this tray's UI;
   - enter staff-carried/on-trolley state;
   - disable interaction/colliders appropriately;
   - restore original mode/source on failure.
4. Call it before `BotTrolleyCarrier.TryAttach()`.
5. Roll back and release the claim if attachment fails.
6. Complete delivery/cleanup and release exactly once.
7. Never globally disable pickup UI.

A small internal `TrolleyTrayJob` containing tray, original mode, source booth, target group, claim state, and attachment state is enough. Do not build a generic inventory framework.

### Phase 5 — Make batch dispatch deterministic

#### Waiter priority

1. Finish anything already held or claimed.
2. Complete existing payment or deliver an already-ready takeout bag.
3. Dispatch an eligible trolley batch of two to four prepared trays.
4. Deliver one tray normally when no batch exists after grace.
5. Deliver bills.
6. Begin new takeout/order-taking work.

This prevents newly started orders from starving prepared food. Do not interrupt an active task because another tray becomes ready.

#### Busser priority

1. Finish anything already held or claimed.
2. Dispatch a trolley batch of two to four dirty trays.
3. Clean one tray normally when no batch exists after grace.
4. Perform table-only cleaning.

#### Selection rules

- Sort by ready time, then stable instance ID.
- Claim the complete candidate set before the coroutine starts.
- If fewer than two claims succeed, release them and return to grace/single-tray logic.
- Do not change batch membership mid-route.
- Reevaluate leftovers after return.

### Phase 6 — Complete waiter batching end-to-end

1. Reserve two to four prepared trays.
2. Acquire the parked trolley and prove `BeginUse` succeeds.
3. Move to each valid pickup approach.
4. Atomically transition and attach each tray to the next slot.
5. Keep stable tray-to-customer mapping even if slots compact.
6. Visit assigned booths deterministically.
7. Detach the matching tray, invoke existing delivery behavior, and complete state/claim once.
8. Safely handle one invalid customer without corrupting other jobs.
9. Return empty trolley, stop animation, clear owner, and mark waiter idle.

Acceptance is at least two correct deliveries in one trolley trip—not merely seeing the trolley move.

### Phase 7 — Complete busser batching end-to-end

1. Reserve two to four dirty trays and source booths.
2. Acquire the busser trolley and prove `BeginUse` succeeds.
3. Visit source booths and load each tray into a flat slot.
4. Do not visit the sink after each pickup.
5. Travel to the sink once after collection.
6. Unload/dispose every collected tray exactly once through existing cleanup registration.
7. Release source claims and let booths become available normally.
8. Return empty trolley and clear animation, owner, and busy state.

Acceptance is at least two dirty trays collected before one sink trip.

### Phase 8 — Implement one reliable recovery path

Use one cleanup/finalization contract equivalent to `try/finally`:

- approach failure: release claims; cart remains/returns parked;
- bot disabled/role changed: restore trays and clear animation/ownership;
- individual tray invalid: release only that job and continue when possible;
- zero attached trays: return cart immediately;
- table route failure: restore affected tray at a reachable safe point with proper mode;
- sink route failure: restore dirty trays without duplicate cleanup;
- shift end: follow closeout policy, then clear cart and bot state;
- parking route failure: an **empty** trolley may snap to parking as final recovery;
- never leave task claims, task UI, bot `IsBusy`, carrying animation, ownership, tray colliders, or queue membership stuck.

Keep production fallback but record the reason once per batch attempt.

### Phase 9 — Make authoring migration safe

Relevant file: `Assets/_Project/Editor/CardPaymentAndUpgradeAuthoring.cs`

1. Bump authoring version only after migration preserves existing edits.
2. For existing prefabs, create only missing objects/references.
3. Do not reset user-authored `VisualPivot`, parking, material, or approved slot/grip transforms.
4. Remove physical components from `HoldingPoint` idempotently.
5. Stop `ConfigureAuthoring()` from forcing minimum batch size `1`; use approved value `2`.
6. Synchronize busser structure with waiter without copying waiter material/effect.
7. Add bot grip creation/assignment without moving an existing assigned grip.
8. Reopen/reimport twice; the second pass must produce no prefab/scene diff.

### Phase 10 — Replace misleading static checks with real validation

Update `CardPaymentTrolleyEquipmentSmokeTest.cs` to validate:

- correct role effect, capacity four, minimum batch two;
- identity gameplay root and visible `VisualPivot`;
- non-physical unit-scale `HoldingPoint`;
- exactly four unique unit-scale tray slots;
- slots inside visual bounds and above ground;
- real tray preview horizontal with no slot overlap;
- both role prefabs contain and serialize a dedicated `TrolleyGripPoint`;
- correct role-specific material/color;
- both parking point names exist in `Lobby1`;
- authoring migration is idempotent.

The smoke test validates only. It must not rewrite assets to pass.

## Development Diagnostics Required

Add a compact optional debug section, disabled by default in release:

- purchased/configured/visible;
- current trolley state;
- assigned bot name;
- eligible and rejected tray counts by reason;
- claimed batch count;
- loaded tray count;
- last approach/path result;
- last recovery/fallback reason;
- retry-until time.

Log state changes and one failure summary per attempt, never every scan/frame.

## Expected File Scope

Primary files:

- `Assets/_Project/Gameplay/AutonomousService/Lobby/BotTrolleyCarrier.cs`
- `Assets/_Project/Gameplay/AutonomousService/Lobby/LobbyAutonomousService.cs`
- `Assets/_Project/Restaurant/Items/FoodTrayInteractable.cs`
- `Assets/_Project/Restaurant/Items/WaiterHands.cs`
- `Assets/_Project/Lobby/Roles/BusserHands.cs`
- `Assets/_Project/Lobby/Assets/Waiter/Waiter.prefab`
- `Assets/_Project/Lobby/Assets/Busser/Busser.prefab`
- `Assets/_Project/Resources/Upgrades/WaiterTrolley.prefab`
- `Assets/_Project/Resources/Upgrades/BusserTrolley.prefab`
- `Assets/_Project/Editor/CardPaymentAndUpgradeAuthoring.cs`
- `Assets/_Project/Editor/CardPaymentTrolleyEquipmentSmokeTest.cs`

Only if proven necessary:

- `Assets/_Project/Restaurant/Items/TrayPickupQueue.cs`
- `Assets/_Project/Gameplay/AutonomousService/Core/AutonomousStaffBot.cs`
- `Assets/_Project/Scenes/RoleBased/Lobby1.unity` for measured parking/approach tuning
- a focused PlayMode integration test in the existing test assembly

Do not modify card payment, HUD, newspaper, settings, fonts, approval balance, customer AI, skybox, or equipment shop UI during this repair.

## Deterministic Test Matrix

### Edit Mode / prefab

1. Both roots are identity and scale `1,1,1`.
2. Both carts are upright and correctly sized beside their bot.
3. Dedicated grip and handle anchors are visible/editable as gizmos.
4. No holding/grip marker has an enabled collider or renderer.
5. Four real tray previews sit flat, separated, and supported.
6. Waiter/busser retain distinct materials.
7. Authoring installer run twice produces no second-run diff.

### Waiter Play Mode

1. Not purchased + two trays: old one-tray service works; no trolley use.
2. Purchased + one tray: after grace, one-tray service works.
3. Purchased + two trays: waiter acquires cart, loads/delivers two, returns it.
4. Purchased + four trays: four slots and correct tables.
5. Purchased + five trays: first route handles four; remaining tray is reevaluated.
6. Continuous takeout/order work does not starve a two-tray batch.
7. After return, bills/payments and later trolley batches still work.

### Busser Play Mode

1. Not purchased + two dirty trays: old cleanup remains functional.
2. Purchased + one dirty tray: after grace, one-tray cleanup works.
3. Purchased + two dirty trays: collect both, one sink trip, return cart.
4. Purchased + four dirty trays: four slots, every tray cleaned once.
5. Trays at different booths join one route.
6. No sink trip after each pickup during an active batch.
7. After return, booth cleaning and a later batch still work.

### Alignment and navigation

1. Bot approaches before trolley attaches.
2. Handle stays at grip through straight movement and turns.
3. Cart stays upright/in front; wheels remain near floor.
4. Bot does not overlap, push, or orbit the cart.
5. Moving either parking point remains supported without code changes.
6. A blocked approach recovers without a stuck bot or permanent silent fallback.

### State and regression

1. Player cannot pick a reserved/on-trolley tray.
2. Pickup UI hides only for owned trays and returns after recoverable failure.
3. No stale `TrayPickupQueue` entry after bot pickup/delivery.
4. No tray duplication, double destruction, vertical tray, or permanent non-interaction.
5. Deactivating an employee mid-route clears cart, claims, animation, and busy state.
6. Shift end/reload leaves exactly one purchased trolley parked.
7. A second order wave on the same day uses the trolley again.
8. No trolley, claim, missing-reference, NavMesh, or coroutine exception.

### Build parity

Run two-tray and four-tray waiter/busser cases in:

- Unity Editor Play Mode;
- Windows standalone build;
- Android build at the thesis device aspect ratio.

Verify identical purchase/role state, trolley count, batching, animation, and recovery. Editor-only success is not completion.

## Definition of Done

- Busser and waiter visibly hold their trolley through a dedicated grip.
- Two to four waiter trays are delivered in one trolley route.
- Two to four busser trays are collected before one sink route.
- Trays stay correctly scaled, flat, and in separate slots.
- One-tray fallback still works when appropriate.
- Trolley is parked/released whenever idle and works again later the same day.
- Failure restores claims, UI, tray mode, colliders, animation, ownership, and busy state.
- Grip, slot, visual, parking, threshold, timing, and smoothing values remain editable.
- User parking/presentation edits are preserved unless a measured test documents a required change.
- Static and PlayMode tests pass.
- Editor, Windows, and Android behavior match.
- No unrelated systems are changed or broken.

## Non-Goals

- Do not redesign the equipment window.
- Do not change trolley prices or unlock days.
- Do not replace animation unless the existing carrying pose cannot be aligned after correct grip authoring.
- Do not rewrite all staff AI/NavMesh behavior.
- Do not build a generic vehicle/inventory framework.
- Do not change unrelated UI or balance.

The recovery should remain focused: one explicit trolley state machine, correct bot/grip relationships, one atomic tray lifecycle, deterministic batch dispatch, safe recovery, and proof through real multi-tray gameplay.
