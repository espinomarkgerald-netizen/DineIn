# Waiter and Busser Trolley Behavior — Implementation Plan

**Status:** IMPLEMENTED AND PLAY-MODE VERIFIED — 2026-08-29.

The implementation keeps all tuning editable in the existing service component and trolley prefabs. The deterministic Unity test now covers four-tray waiter/busser routes, forecast-aware two-tray batching, same-day reuse, isolated one-tray fallback, contextual one-tray-plus-booth busser work, grip alignment, boost activation, and exact speed restoration. Static trolley/equipment asset validation also passes.

**Date:** 2026-08-29

**Scope:** Improve waiter and busser decision-making so purchased trolleys feel like meaningful upgrades. The plan adds kitchen-ready forecasting, deadline-aware task priority, opportunistic work bundling up to the four-tray capacity, trolley-only movement boosts, safe fallbacks, editable tuning, and PC/Android build verification.

## Goal

The trolley must make restaurant service more convenient without making the bots reckless or breaking existing tasks.

- The waiter should anticipate when prepared food will appear and avoid separate trips when a second order is known to be almost ready.
- The busser should combine tray pickup and booth cleaning into one visit, then carry every currently useful tray to the sink together.
- Four trays is the maximum capacity, **not a number either bot must wait for**.
- Both bots should receive a movement-speed boost only while actively pushing their purchased trolley.
- Urgent customer tasks must not be ignored merely to wait for a perfect batch.
- The trolley route should measurably reduce travel or task time compared with the non-trolley route.
- All thresholds, time windows, speed multipliers, and routing points must remain editable in the Inspector or prefabs.
- Unpurchased, unavailable, or unreachable trolleys must safely fall back to the normal one-tray behavior.

## Current Project Status

The trolley foundation already exists and should be extended rather than replaced.

| Area | Current state | Next requirement |
|---|---|---|
| Waiter trolley | Four-slot trolley carrier and batch-delivery route exist. | Add kitchen-ready forecasting and smarter task selection. |
| Busser trolley | Four-slot carrier and multi-tray cleanup route exist. | Improve batch selection and ensure one sink trip per valid batch. |
| Trolley ownership | Trolleys can be parked, reserved, attached, returned, and recovered. | Preserve these state transitions while adding speed modifiers. |
| Batch grace windows | Waiter and busser currently have simple second-tray grace behavior. | Let only the waiter wait for a reliably forecast second tray; the busser should process current cleanup work immediately. |
| Kitchen timing | `KitchenManager` has a cook duration and order start/finish events. | Expose a read-only per-order predicted ready time. |
| Staff speed | `AutonomousStaffBot` combines base speed with employee speed. | Add a separate temporary movement modifier for trolley use. |
| Regression testing | A deterministic trolley smoke test covers four-tray waiter and busser routes and same-day reuse. | Extend it for forecasting, boost activation/restoration, and fallback cases. |

## Player-Facing Behavior Contract

### Waiter without the trolley upgrade

- Continue using the normal one-tray workflow.
- Do not wait for batch opportunities.
- Preserve current payment, bill, order-taking, takeout, and delivery behavior.

### Waiter with the trolley upgrade

1. The waiter evaluates ready food, soon-to-be-ready food, active customer deadlines, and already-held work.
2. If two or more valid trays are ready, the waiter uses the trolley immediately. It does not wait for the trolley to reach four.
3. If one tray is ready and another dine-in tray is reliably forecast very soon, the waiter may wait only long enough to gain that second tray.
4. As soon as the second tray becomes ready, the route can begin. A third or fourth tray may be included if already ready by then, but the waiter does not continue waiting just to fill empty slots.
5. The waiter must not wait if the current tray or customer is becoming urgent, the predicted tray is canceled, or the forecast is outside the allowed wait window.
6. The waiter takes the trolley, loads the currently useful trays up to capacity, and delivers every tray to its correct table.
7. A new compatible tray that becomes ready while the waiter is still loading may be added if it is unclaimed, capacity remains, and doing so does not delay the committed route.
8. The waiter receives the trolley speed boost only after taking control of the trolley.
9. The boost ends before the trolley is released or parked, including every failure and cancellation path.
10. If only one tray is ready and no second tray is imminent, the waiter delivers it normally instead of waiting or fetching the trolley unnecessarily.

### Busser without the trolley upgrade

- Continue the normal one-tray-to-sink cleanup route.

### Busser with the trolley upgrade

1. The busser treats each dirty booth as a **work bundle**: remove its dirty tray, put the tray on the trolley, and clean the booth during the same visit.
2. The busser does not take one tray to the sink and then return to the same booth to clean it. That old double-trip pattern is specifically eliminated when the trolley is available.
3. If additional dirty trays or dirty booths already exist, the busser continues to them and loads their trays until there is no immediately useful stop or the trolley reaches capacity.
4. The busser never waits around for four dirty trays. It works on the useful mess that exists now.
5. Every collected tray is placed in a different trolley slot and remains reserved during the route.
6. After the collection-and-cleaning sweep, the busser makes one sink trip and unloads all collected trays exactly once.
7. The busser receives the trolley speed boost only while controlling the trolley.
8. With five or more trays, the first trip carries four; the remainder is reevaluated after the trolley returns.
9. With only one dirty booth, the trolley may still be worthwhile because tray pickup and booth cleaning become one visit before the sink trip. With one loose tray and no booth work, use the normal route unless the trolley route is demonstrably faster.

## Recommended Tuning

All values below should be serialized and editable. These are starting values, not hardcoded rules.

| Setting | Recommended default | Editable range | Reason |
|---|---:|---:|---|
| Trolley capacity | 4 trays | 1–4 | Matches the upgrade promise and authored slots. |
| Waiter trolley start count | 2 ready trays | 2–4 | Start as soon as two are ready; never wait specifically for four. |
| Busser trolley start rule | 2 trays, or 1 tray + dirty booth | Contextual | Allows the trolley to remove the old tray-to-sink-then-return cleanup pattern. |
| Waiter near-ready window | 3.0 seconds | 0–5 seconds | Long enough to combine imminent food without visibly idling. |
| Waiter maximum deliberate wait | 3.0 seconds | 0–5 seconds | Hard cap prevents forecast waiting from starving other work. |
| Busser future-tray wait | 0 seconds | 0–2 seconds | Default behavior is to clean current work immediately, not wait for four dishes. |
| Scheduler reevaluation | 0.2–0.25 seconds | 0.1–1 second | Responsive without querying the whole scene every frame. |
| Trolley speed multiplier | **1.35×** | 1.0–1.5× | Noticeable upgrade with safer navigation than an immediate 1.5× default. |
| Trolley acceleration multiplier | 1.20× | 1.0–1.5× | Helps the cart reach the boosted speed without abrupt motion. |

### Recommendation about the requested 50% boost

A 50% speed boost should remain available as an editable maximum, but **1.35× is the recommended default**. A full 1.50× default can cause overshooting at tables, sharper cart turns, animation foot sliding, and more crowd collisions in narrow restaurant paths. If playtesting shows the navigation remains stable, the prefab value can be raised to 1.50× without code changes.

### Efficiency acceptance target

- A waiter trolley route with two or more trays must remove repeated returns to the food pickup point.
- A busser route with two or more trays must use one sink visit rather than one sink visit per tray.
- A busser handling one tray on a booth that also needs cleaning must finish both booth tasks before leaving for the sink, eliminating the later return to that booth.
- The bot should never delay an urgent task merely to increase the load count.
- If trolley acquisition and parking would make a single isolated task slower, the bot should use the normal route. The upgrade's intelligence includes knowing when **not** to fetch the trolley.

## Task Priority Model

Do not replace the current waiter logic with a rigid list that blindly chooses food. Use a small deterministic scoring layer when the bot is free.

### Rules that always come first

1. Finish or safely cancel whatever the bot is already physically holding.
2. Do not interrupt an active trolley route halfway through a valid batch.
3. Do not steal a task reserved by the player or another bot.
4. Do not begin a new task when the role is disabled, the shift is inactive, or the relevant hands are not free.

### Candidate scoring

For each eligible task, calculate a score from:

```text
task score = base role priority
           + deadline pressure
           + customer-state urgency
           + trolley batch value
           - travel cost
           - task-switch penalty
```

The exact weights must be Inspector-editable in one `BotTaskPrioritySettings` section or ScriptableObject. Equal scores use stable tie-breakers: oldest ready time, then order number or instance ID. This prevents bots from changing their mind every frame.

### Suggested waiter priority order

1. Resolve an item already held by the waiter: money, ticket, tray, bill, or takeout bag.
2. Finish time-critical payment work that is already waiting and blocking table turnover.
3. Deliver prepared food whose freshness/customer deadline has become urgent.
4. Start a trolley batch immediately when at least two trays are ready.
5. Wait briefly for a forecast tray only when it will create the second tray inside the near-ready window; never wait merely for the third or fourth.
6. Deliver one ready tray when waiting is not beneficial or safe.
7. Deliver bills, take takeout orders, then take dine-in orders according to their age and customer patience.
8. Return home or use a non-blocking waiting point.

This order preserves current commitments while allowing timers to influence which **new** task starts next.

### Suggested busser priority order

1. Finish a tray already held or a trolley batch already in progress.
2. Start a trolley route when two or more dirty trays exist, or when one dirty tray belongs to a booth that also needs cleaning.
3. At each booth, load its tray and clean that booth before leaving for the next useful stop.
4. Prefer booths needed for waiting customers, then the oldest dirty booths/trays.
5. Make one sink trip after the current collection-and-cleaning sweep; do not wait to fill all four slots.
6. Use the single-tray fallback for one loose tray with no related booth work or whenever the trolley is unavailable.

## Phase 1 — Add an Authoritative Kitchen Forecast

The waiter cannot reliably predict the next tray from `cookSeconds` alone. Each cooking order needs its own snapshot because cooking speed can be affected by assigned staff and may change later.

### Data to record when an order starts

- Customer group reference.
- Order number.
- Whether it is dine-in or takeout.
- Scaled start time.
- Preparation delay snapshot.
- Cook-duration snapshot.
- Predicted earliest spawn time.
- Current state: cooking, waiting for a free spawn slot, completed, or canceled.

### Read-only API

Add a small read-only forecast structure and query methods to `KitchenManager`, such as:

- Get the next valid dine-in tray forecast.
- Copy all active dine-in forecasts into a caller-provided list without allocations.
- Try to get remaining time for a specific order.
- Notify listeners when a forecast starts, changes state, completes, or is canceled.

### Important timing rules

- Snapshot the order's effective cook duration at cooking start. Do not recalculate an active order from a later staff change.
- Use the same scaled-time basis as the cooking coroutine, so pausing the game freezes both cooking and its forecast.
- A predicted spawn time is not a guarantee if all tray spawn slots are occupied. Mark that order as `WaitingForSlot`; do not promise an exact countdown after that point.
- Remove or complete forecast entries in the coroutine's `finally` path so exceptions and canceled orders cannot leave stale predictions.
- Forecast dine-in trays separately from takeout bags because only dine-in trays can join the waiter trolley batch.

## Phase 2 — Make Waiter Waiting Forecast-Aware

Replace the current blind one-tray grace decision with these conditions:

### Wait for the next tray only when all are true

- A purchased, configured, reachable waiter trolley exists.
- Exactly one eligible dine-in tray is currently ready.
- At least one additional valid dine-in tray is forecast inside the near-ready window.
- The ready tray is not already urgent.
- No higher-scoring payment, bill, takeout, or customer task requires immediate action.
- The trolley has capacity and both predicted/current orders remain unclaimed.
- The waiter has not exceeded the maximum deliberate wait.

The wait ends when the second tray appears. It is not extended to search for a third or fourth tray. Capacity four only means that extra trays which are already available can join the same route.

### While waiting

- Move the waiter to an editable `WaiterBatchWaitingPoint` near the prepared-food counter, not into the trolley or food spawn path.
- Reevaluate on forecast events and the normal scheduler interval.
- Do not reserve a tray that does not exist yet.
- Reserve ready trays atomically only when starting the actual route.
- Cancel the wait immediately if the forecast is canceled, delayed by a full spawn area, or superseded by an urgent task.

### When the predicted tray appears

- Rebuild the candidate list from current valid trays.
- Sort primarily by urgency/oldest ready time and secondarily by route cost.
- Reserve up to four.
- Start the existing trolley route.
- Do not restart or extend the waiting timer because slots three and four are empty.

## Phase 3 — Add a Safe Trolley-Only Speed Modifier

Do not set `NavMeshAgent.speed` directly from `BotTrolleyCarrier`. The bot already derives movement speed from base agent speed and the assigned employee's speed stat.

### Required speed model

```text
effective movement speed = base agent speed
                         × employee speed multiplier
                         × temporary trolley multiplier
```

### Implementation contract

- Add a temporary movement-modifier API to `AutonomousStaffBot`.
- The trolley acquires one named/tokenized modifier when `BeginUse` succeeds.
- The trolley releases that exact modifier in `EndUse`, recovery, disable, role reassignment, shift end, and scene teardown.
- Reapplying `BeginUse` must not stack another 1.35× boost.
- Releasing twice must be harmless.
- Changing employee assignment while the cart is active must recompute the full effective speed correctly.
- Optionally apply a smaller acceleration boost, but do not change braking/stopping distances without navigation testing.
- Expose separate waiter and busser speed multipliers on their trolley prefabs so the user can tune them independently.
- Show the active multiplier and resulting speed in a read-only runtime diagnostic field.

## Phase 4 — Waiter Batch Route Refinement

1. Gather eligible ready trays using one shared eligibility predicate for selection and execution.
2. Rank candidates by deadline pressure, oldest `ReadySince`, and estimated route cost.
3. Claim the two or more currently useful trays atomically, up to four. If fewer than two claims survive, release them and use the single-tray fallback.
4. Walk to the trolley approach point without a speed boost.
5. Call `BeginUse`; enable the speed modifier only after it succeeds.
6. Load each claimed tray into a distinct authored trolley slot.
7. Deliver trays to their matching tables. Never depend on list position to identify the destination.
8. If one tray becomes invalid, release only that tray and continue the rest of the valid batch.
9. Return the empty trolley to its parking point.
10. Release the speed modifier, carrying pose, trolley ownership, and claims in one guaranteed cleanup path.
11. Reevaluate remaining trays immediately after return. A route may carry two, three, or four trays; none of those valid routes should wait for a fuller trolley.

## Phase 5 — Busser Multi-Tray Route Refinement

1. Build a list of current **cleanup stops**, not merely a list of trays. Each stop records a booth, its removable tray if present, whether the booth still needs cleaning, its urgency, and its route position.
2. Gather only work not held or claimed by the player/another bot.
3. Use the trolley when either (a) at least two dirty trays are available or (b) one tray and its booth can be completed as one combined stop.
4. Prioritize stops for booths needed by waiting customers, then oldest dirty booths/trays.
5. Select useful stops whose trays fit in the four available slots. Do not wait for more stops to appear.
6. Walk to the trolley approach point, begin trolley use, then enable the boost.
7. Visit stops using a simple nearest-next route from the current position. With a maximum of four trays, this is sufficient and does not need a complex path optimizer.
8. At each stop: validate the claim, remove the tray, place it in a free trolley slot, perform the booth-cleaning action, mark that booth clean, then continue. The tray remains on the trolley while the booth is cleaned.
9. If another compatible dirty stop becomes available during the sweep and is nearby, unclaimed, and capacity remains, it may be appended without restarting the route. Do not idle or backtrack a long distance just to fill a slot.
10. Visit the sink when all selected stops are complete, the trolley is full, or no remaining selected stop is reachable.
11. Make one sink trip and complete each attached tray exactly once.
12. Return and release the trolley and boost through a guaranteed cleanup path.
13. Reevaluate remaining dirty work after parking.

### Busser efficiency rule

The trolley route must eliminate avoidable backtracking. For a booth containing a dirty tray, the expected route is:

```text
WITH TROLLEY:
parking → booth → load tray → clean booth → optional next booth(s) → sink → parking

WITHOUT TROLLEY:
booth → carry tray to sink → return to booth → clean booth → next task
```

The busser is not required to fill four slots. The upgrade succeeds whenever it combines work and reduces travel compared with the second route.

## Phase 6 — Failure and Recovery Rules

The upgrade must never make the bots less reliable.

- **Trolley route unavailable:** log one concise diagnostic, start the existing retry cooldown, and use normal one-tray service.
- **Parking point moved:** sample a nearby NavMesh approach point; never move the user's authored parking transform automatically.
- **Forecast canceled:** stop waiting and score current tasks again.
- **Spawn slots full:** treat forecast timing as uncertain and avoid indefinite waiting.
- **Tray destroyed or claimed:** remove only that tray from the batch.
- **Bot disabled/reassigned:** release all task claims, detach trays safely, remove speed modifiers, and park the trolley.
- **Shift ends/scene changes:** perform the same cleanup before destruction.
- **Sink unavailable:** do not destroy collected trays; recover them to a valid cleanup state and release claims.
- **Route times out:** end trolley use, restore normal speed, and enter the existing retry cooldown.

## Phase 7 — Editability and Authoring

Keep these values editable and visible in their logical owner:

### `LobbyAutonomousService`

- Waiter near-ready window.
- Waiter maximum deliberate wait.
- Busser contextual trolley-use rules and maximum opportunistic detour distance.
- Priority weights and urgent thresholds.
- Scheduler refresh interval.
- Optional `WaiterBatchWaitingPoint` reference.

### Waiter and busser trolley prefabs

- Capacity, waiter start count, and busser contextual start settings.
- Trolley movement-speed multiplier.
- Optional acceleration multiplier.
- Grip, push, tray-slot, and parking approach offsets.

### `AutonomousStaffBot`

- Read-only runtime values for base speed, employee multiplier, temporary trolley multiplier, and effective speed.

Do not generate or overwrite prefab values every time Unity opens. Any migration tool must be explicit, idempotent, and preserve later user edits.

## Phase 8 — Diagnostics

Add development-only information that explains bot choices without filling release builds with warnings:

- Current task and task score.
- Reason the bot is waiting for a predicted tray.
- Next predicted dine-in tray and remaining seconds.
- Selected batch count and reserved order/tray IDs.
- Trolley state, active speed multiplier, and effective bot speed.
- Last fallback or recovery reason.

Suggested counters for balancing:

- Average trays per trolley trip.
- One-tray trips made while the trolley was purchased.
- Seconds deliberately spent waiting for a batch.
- Distance/trips saved compared with one-by-one service.
- Failed trolley acquisition and route counts.
- Booths completed without a return trip after tray removal.
- Average service-time improvement with the upgrade purchased.

These can stay as editor diagnostics; no player-facing UI is required for this change.

## Validation and Acceptance Tests

### Waiter deterministic tests

- One tray ready, second forecast in 2 seconds: waiter waits briefly and delivers both in one trolley route.
- One tray ready, second forecast in 8 seconds: waiter delivers the first without waiting 8 seconds.
- Two trays ready and no other forecast: waiter starts immediately with two instead of waiting for four.
- Three trays ready and no other forecast: waiter starts immediately with three instead of waiting for four.
- Four trays ready: one trolley trip loads and delivers all four.
- Five trays ready: first route carries four; the fifth is reevaluated after return.
- Forecast canceled while waiting: waiter resumes another valid task without a stuck claim.
- Spawn slots full: waiter does not wait forever for the predicted tray.
- Urgent ready tray plus a near-ready tray: urgent tray is not delayed beyond its configured limit.
- Payment already held: waiter finishes payment before considering a new trolley batch.
- Two trolley trips on the same day: trolley and speed boost are reusable and do not stack.

### Busser deterministic tests

- One dirty tray on a booth that also needs cleaning: busser loads the tray, cleans that booth before leaving, then visits the sink once; it does not return to that booth.
- Two dirty trays: busser starts immediately and does not wait for trays three and four.
- Three dirty trays: busser starts immediately and does not wait for a fourth.
- Four dirty trays: busser collects all four before exactly one sink visit.
- Five dirty trays: first route collects four; remaining tray is handled after return.
- One loose dirty tray with no booth-cleaning work: normal single cleanup remains available when fetching the trolley would be slower.
- One selected tray becomes invalid: remaining batch completes and all claims clear.
- Dirty trays at separated tables: busser visits sources without intermediate sink trips.
- Table urgently needed by waiting customers: its dirty tray receives priority.

### Speed tests

- Before trolley acquisition: effective speed equals base × employee multiplier.
- During trolley use: effective speed also includes the configured trolley multiplier.
- After parking: speed returns exactly to the non-trolley value.
- Failure, bot disable, role reassignment, shift end, and scene unload all restore speed.
- Calling begin/end twice never stacks or permanently lowers/raises speed.

### Regression tests

- Trolley not purchased: existing one-tray behavior remains unchanged.
- Player-owned and other-bot-owned tasks are never stolen.
- Payment, card payment, bills, order-taking, takeout, customer patience, and table cleanup still complete.
- No duplicate tray completion, duplicate sink cleanup, duplicate money, or hidden pickup bubble remains.
- Existing trolley prefab validation and deterministic smoke tests still pass.
- Repeat the full scenario in Unity Play Mode, a Windows build, and an Android build.
- Test at normal time, paused/resumed time, and the game's supported time-speed settings.

## Files Expected to Change During Implementation

- `Assets/_Project/Restaurant/Items/KitchenManager.cs`
- A small forecast data type beside `KitchenManager`, if keeping it separate improves clarity.
- `Assets/_Project/Gameplay/AutonomousService/Lobby/LobbyAutonomousService.cs`
- `Assets/_Project/Gameplay/AutonomousService/Core/AutonomousStaffBot.cs`
- `Assets/_Project/Gameplay/AutonomousService/Lobby/BotTrolleyCarrier.cs`
- `Assets/_Project/Resources/Upgrades/WaiterTrolley.prefab`
- `Assets/_Project/Resources/Upgrades/BusserTrolley.prefab`
- `Assets/_Project/Editor/TrolleyGameplaySmokeTest.cs`
- Static trolley/upgrade validation tests if new serialized settings require validation.

Do not modify unrelated scenes, UI, card payment, customer visuals, or restaurant layout for this behavior update.

## Suggested Implementation Order

1. Record baseline behavior and run the current trolley tests.
2. Add the read-only kitchen forecast and its unit/deterministic tests.
3. Add the composable temporary speed-modifier API and test restoration first.
4. Replace blind waiter grace waiting with forecast-aware waiting.
5. Add deterministic task scoring and stable tie-breakers.
6. Refine the waiter route while preserving the existing trolley carrier.
7. Refine busser selection and guarantee one sink trip per batch.
8. Add recovery coverage and diagnostics.
9. Tune Inspector defaults in both trolley prefabs.
10. Run Play Mode, Windows build, and Android build acceptance tests.

## Additional Suggestions

### 1. Use a waiting point near the kitchen

Give the waiter an editable staging point where it can wait for a tray that is only one or two seconds away. This keeps the bot from blocking the food spawn, cashier, or trolley handle.

### 2. Give urgent tables priority without exposing more UI

Internally increase busser priority when a dirty table is preventing a waiting group from being seated. The player sees a faster restaurant, but the HUD does not gain another distracting message.

### 3. Make the upgrade measurable

Track trays per trolley trip, booths completed during the collection sweep, and trips saved in editor-only diagnostics. Target a noticeable reduction in travel and task completion time during multi-item scenarios. This also makes the 35% versus 50% speed decision based on gameplay evidence instead of appearance alone.

### 4. Use simple benefit rules instead of a complex planner

The bots do not need a heavy simulation to decide whether to use the trolley. Three clear rules cover the important cases:

- Waiter: use it for two or more ready trays, or wait briefly when a second tray is reliably imminent.
- Busser: use it for two or more dirty trays, or for one dirty tray when the same booth also needs cleaning.
- Otherwise: use the proven normal route.

This keeps the behavior predictable, editable, and easier to debug.

### 5. Allow opportunistic additions without delaying the route

If a compatible tray becomes ready while a bot is still beside the pickup area, or a nearby booth becomes dirty while the busser is already sweeping that area, the bot may add it when space remains. It should never reverse a nearly completed route or wait around just to fill the trolley.

### 6. Keep cart polish separate from logic

After the behavior passes all tests, optional wheel motion, light trolley sounds, and a slightly bouncier push animation can make the upgrade feel stronger. Do not add that polish until batching, task priority, and recovery are proven stable.

## Definition of Done

This feature is complete only when:

- The waiter uses forecasts to make sensible batch decisions without starving urgent work.
- The waiter begins as soon as two useful trays are ready and never waits specifically to fill four slots.
- The waiter reliably carries two, three, or four correct orders in one trolley route when those orders are available.
- The busser combines tray pickup and booth cleaning into one visit, carries the useful current load up to four trays, and makes one sink trip for that load.
- The busser never waits specifically for four dirty trays.
- Measured multi-item and dirty-booth scenarios complete faster or with fewer trips than the equivalent non-trolley workflow.
- Both bots receive an editable boost only while actively pushing their trolley.
- Every success, cancellation, failure, reassignment, and scene-exit path restores the correct speed and releases claims.
- Unpurchased/unavailable trolleys preserve normal one-tray gameplay.
- Inspector edits persist and are not overwritten by runtime or editor migration code.
- Deterministic tests pass in Play Mode and behavior matches in Windows and Android builds.
