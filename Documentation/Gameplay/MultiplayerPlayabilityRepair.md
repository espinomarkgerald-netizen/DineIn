# Casual Dining multiplayer playability repair

Implementation handoff: 2026-09-16. Unity version: **6000.0.40f1**.

The subsequent [human service parity repair](MultiplayerHumanServiceParityRepair.md) addresses order cooking, pickup/delivery, bills, payments, and unintended input. Its protocol is `casual-session-4`; the protocol-3 details below describe the preceding repair.

## Status and scope

The source repair and external regression runner are prepared. **Compilation, gameplay acceptance and performance measurement have not been run. This is not a verified-playable sign-off.** No Unity launch, build, gameplay test, GUI test, account operation or backend deployment was performed on the user's computer.

Single-player remains the authority for restaurant rules: recipes, stock use, service operations, printing delay, prices, cash/change, card payment, departure, cleanup and day settlement. Multiplayer transports player intent to the host and projects the resulting state.

Preserved: disposable run progress; Day 31+ counting; original-host termination; 90-second guest rejoin; longest-run receipts; the repaired Photon room connection; existing PlayFab result rules. Website work, backend deployment and new gameplay features are outside this repair.

## Implemented changes

| Problem | Source repair |
| --- | --- |
| Freezer overlap and wrong shelf | `MultiplayerRestockView` reserves layer 15 (`MultiplayerRestock`) for restock geometry, containers and previews. Restaurant cameras exclude it; the local restock camera excludes restaurant geometry. Physics isolation and a shared ray resolver validate the loaded scene and selected storage room. Existing-box mouse/touch selection checks that room. The view suppresses restaurant canvases; newly created world bubbles suppress themselves. Exit restores camera, collision and UI state. |
| False/stale bot ownership | Bots expose job generation, current approach and committed/carry state. Claims match that exact generation. Host takeover cancels eligible approaches and releases their reservations. Uncollected trolley reservations can be yielded while loaded trays remain with the bot. Cancelled approaches cannot later report success. Disabled staff, destroyed targets and finished human tasks release stale claims. |
| Human interaction races | Human claims have authoritative lease tokens, request generations and revisions. Switching installs the complete ownership change before notifying observers. Rejection keeps the prior task. Payment and dirty-tray controls acquire a claim before walking. Repeated approach clicks cannot cancel the replacement request. |
| Missing/doubled bubbles | `MultiplayerTaskPresentation` supplies shared ownership display and a registry keyed by run, day, target task and bubble type. Order, bill, bill-pickup, payment, greet and tray spawners reuse this path. Bill request and printer pickup are distinct types. Removal disables obsolete rendering/input before deferred destruction. Rebinding reuses components and handlers. Focus reuses the existing `CanvasGroup` or composes with `UIFollowWorldPoint`. |
| Billing | `BillManager` owns the paper registry and printer-slot reservations. Printed, carried and returned papers share one customer/order identity. Full printers queue; held bills retain a return slot. The customer snapshot is the guest paper projection path; replies no longer create or attach papers. Holder transfer supports every human and bot. Delivery/payment cannot reopen printing. |
| Delayed billing actions | Actions carry IDs, expected operation stages, order identity and the claim lease. The host rechecks its movement replica for up to 400 ms. Retries reuse the ID; replies must match the pending action. Cancelled print coroutines cannot acknowledge a replacement action. A bounded timeout retains the paper for recovery. |
| Payment controls | Cash/card bubbles enter the multiplayer service path before legacy role checks. Payment ownership is shared with other task claims. Cancellation, failure, expiry and completion release the claim/reservation. Recovery cannot respawn collectible payment for a paid customer. |
| Synchronization cost | Clock anchors no longer embed full restaurant saves. Money, stock and approval changes send smaller updates; management changes and explicit baselines send full state. Old management baselines cannot overwrite newer economy/finance data. Revisions prevent reapplying unchanged object/service snapshots; unresolved references are retried. Dynamic registries replace repeated discovery in the main replication paths. |
| Movement and measurement | Player/customer/staff/trolley movement uses 10 Hz updates and bounded history rendered about 100 ms behind network time. Old samples are rejected; packet loss does not extrapolate objects through the restaurant. Item ownership/lifecycle remains reliable. Animator parameter lookup is cached. Development diagnostics expose frame time, allocations, memory, traffic, queue size, ping, rejected actions and bubble count. Profiler markers identify replication work. Routine multiplayer money-save and greet-spawn log spam is removed. |

### Protocol and persistence

- Room protocol: `casual-session-3`. All peers need the repaired code and a fresh compatible room.
- Result rules: `casual-session-2`, separate from the room protocol. Existing pending receipts and the PlayFab result contract retain their version.
- Restock retains one visitor reservation. Other players, AI and the host clock continue during a visit.
- Result-service availability remains independent of room creation/joining. This repair adds no dashboard or authentication prerequisite.

## External automated regressions

Entry point: `Assets/_Project/Editor/MultiplayerRepairRegressionTest.cs`.

The runner is opt-in; import does not start tests. It uses an empty Play Mode scene and inactive fixtures to exercise production methods:

1. Old/equal/malformed claim snapshots, atomic replacement and claim-token removal.
2. An old bot generation attempting to release a later reservation.
3. Canonical bill identity, duplicate registration, holder transfer, retained printer slot, cancellation return and a remake's new order.
4. Delivered/paid state resisting older snapshots; a new order resetting those flags.
5. Repeated focus binding with an existing `CanvasGroup`, hidden input and restored visibility.
6. Movement ordering, interpolation, bounded history and explicit recovery.
7. Room/result version separation and the 90-second rejoin constant.

These fixtures do **not** simulate two Photon clients, render the authored rooms, prove human controls work, or establish device FPS.

### Run only on an external runner

Use a disposable checkout and an isolated external OS profile with no production account or pending result outbox. Use the pinned editor and the runner's normal licensed Unity setup. Do not run this command on the user's computer under the current restriction.

```powershell
& $ExternalUnityEditor -batchmode -nographics `
  -projectPath $ExternalCheckout `
  -executeMethod MultiplayerRepairRegressionTest.RunBatch `
  -logFile $ExternalLog
```

Do not add `-quit`: this asynchronous runner exits itself after its Play Mode check. Its exit code and `Artifacts/MultiplayerRepairRegression.txt` are the result. Keep the full Unity log for import/compile/runtime errors. Compilation must succeed before evaluating the fixtures.

The opt-in menu is **Tools → Dine In → Run Multiplayer Repair Regressions (External)**. It switches to an empty scene. Use the disposable checkout.

## Required multiplayer playability gate

Use separate accounts and the repaired code on both sides. Start with two players, then repeat concurrency/performance cases with four. Prevent the corresponding bot from completing each human-service step under verification. A bot-completed day is not a pass. Record which actor used the actual button/touch/drag control and capture both clients' state.

| Area | Required exercise and pass condition |
| --- | --- |
| Connection | Sign in, create, join, leave and create again. Old-protocol peers cannot join. A result-service outage does not block the room. |
| Restock | Alternate freezer/dry visits at least five times per actor. Place/remove correct-storage items, exercise real wrong-storage confirmation, interact with existing boxes and return to restaurant input. No restaurant geometry/HUD leaks; no hidden-room shelf receives a drop. Other players/timers continue; the visitor reservation cannot be stolen. |
| Human service | **Host and guest each** greet, seat, review/confirm, pick up/deliver a tray, request/pick up/deliver a bill, complete cash/change and card payment, collect/wash a dirty tray, and clean table mess through normal controls. Stock, sales, payment and cleanup rewards change once. |
| Human race/switch | Two humans click one task: exactly one owner. Reject a switch while the first task is held: it stays usable. Repeat rapid approach clicks, cancellation/retry, an unreachable approach and target destruction/change during approach. |
| Bot takeover | Click during reaction, approach, work and carrying. Approach takeover succeeds; committed work/carry remains with the bot. After the bot changes jobs, old targets are free. Repeat with empty/partly loaded trolleys, including the currently approached uncollected tray. Loaded trays cannot be stolen. |
| Bubbles | Matching phase/ownership on both clients, one bubble per run/day/target/type, owner-only actions. A private panel on one actor does not hide the other's available tasks. Repeat cancellation, phase changes, reset and rejoin. No duplicate-component error, accumulating labels/listeners or old action after payment/departure. |
| Printer/bills | Request more simultaneous bills than printer slots. They queue without stacking. Carry bills with different humans/bots. Cancel/disconnect at queued, printed, carried, delivered, payment-available and paid stages. Undelivered papers remain recoverable; delivered/paid orders never reprint. Remakes cannot inherit old bills. |
| Network recovery | On the external network introduce delay, jitter and loss. Repeat rapid clicks; deliver duplicate/out-of-order messages through the test harness. Rejoin within 90 seconds. No old lease, prior-day update, duplicate reply or stale baseline restores a completed action, duplicates an item or repeats a charge. |
| Session | Two consecutive days and a fresh second run. Check Day 31+, failure ending the run, host loss without migration, guest expiry retaining earlier credit, remaining-player continuation and longest-run receipts. Compare career saves before/after multiplayer. |
| Single-player | On external hardware repeat billing (including simultaneous bills), cash/card, bubble focus/dimming/animation, freezer/dry restock and normal save loading. Existing rules remain functional. |
| Performance | Measure at least five minutes of busy four-player service on the intended Android device, with orders, bills, bots and trolleys. Target sustained 30 FPS. Capture CPU/GC traces, packet bytes, queue size and bubble counts. No growing queue, recurring synchronization stalls, leaked bubbles or log flood. Record hardware/build configuration. |

Inspect `MultiplayerDiagnostics` in a development player. Profiler markers: `DineIn.Multiplayer.CustomerState`, `DineIn.Multiplayer.ObjectState`, `DineIn.Multiplayer.EconomyState`. Allocation value `-1` means the recorder is unavailable. The inspector allocation value is one sampled frame; use the trace to evaluate spikes.

## Sign-off evidence

The implementation handoff includes source-format and delimiter checks only. Compilation, the opt-in runner, both-player acceptance and Android measurements are **pending external execution**. Existing unrelated asset/scene edits were retained.

A playable sign-off requires the Unity log, regression output, completed matrix with host/guest evidence, and Android performance capture. Fix failures before release; source inspection alone cannot establish these results.
