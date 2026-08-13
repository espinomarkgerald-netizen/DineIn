# Autonomous Staff System

## Scope

This system runs the no-player Casual Dining simulation in `Lobby1`.
It currently automates the Host, Waiter, Busser, Chef, and Barista while
the Cashier remains stationary. Existing customer, kitchen, finance,
tray, bill, and day systems continue to own gameplay state.

The planned Overcooked-style food and beverage production is not part of
this pass. Chef and Barista movement is visual and synchronized with the
current `KitchenManager` cook timer.

## Folder Layout

```text
Assets/_Project/Gameplay/AutonomousService/
|-- Core/AutonomousStaffBot.cs
|-- Kitchen/KitchenWorkerBot.cs
`-- Lobby/LobbyAutonomousService.cs
```

## Staff Lifecycle

Every moving staff member follows this lifecycle:

```text
IdleAtHome -> Reacting -> MovingToTask -> Working -> ReturningHome -> IdleAtHome
```

`AutonomousStaffBot.RunTask` wraps every assigned task. It adds a short
random reaction delay, runs the task coroutine, and returns the character
to its configured home. Lobby staff then resume randomized idle facing and
occasional `Happy Idle` animation playback.

## Booth Navigation

`Booth.Awake` creates a carving `NavMeshObstacle` from the booth's solid
root `BoxCollider`. This removes tables and seating geometry from the
runtime walkable area even though the scene NavMesh was baked without
including obstacles.

Customers walk to a sampled aisle formation around `BoothApproachPoint`.
Once they reach that legal point, `CustomerGroup.SeatMembersFlow` snaps each
customer to the authored seat anchor. Seats are not NavMesh destinations
because they intentionally sit inside the carved booth footprint.

Both `CustomerAgent.WalkTo` and `AutonomousStaffBot.MoveToInternal` sample
destinations before calling `SetDestination`. Staff booth tasks use the
sampled approach position and then face the table, avoiding the old extra
rotation caused by `MoveTo(Transform)`.

## Core Movement

### `AutonomousStaffBot`

| Method | Responsibility |
| --- | --- |
| `ConfigureHome` | Assigns the home transform and NavMesh avoidance priority. A missing home uses the character's scene-start position and rotation. |
| `ConfigureIdlePresentation` | Enables randomized facing toward real restaurant targets and schedules idle animation variation. |
| `StartTask` | Starts one task only when the character is not already busy. |
| `MoveTo(Transform)` | Moves to an authored point and adopts that point's rotation on arrival. |
| `MoveTo(Vector3)` | Moves to a world position without forcing an arrival rotation. |
| `MoveWithin` | Routes toward a sampled NavMesh point but measures service arrival against the original authored marker. Interaction travel defaults to a six-second cap, with a per-task override for longer valid routes such as dine-in payment collection. |
| `LastMoveSucceeded` | Reports whether the latest NavMesh move actually reached its destination; task interactions must check it before changing item state. |
| `FaceTowards` | Smoothly turns the character toward a customer or world target. |
| `WorkFor` | Holds the working state for a slightly randomized duration. |
| `ReturnHome` | Moves to the configured or captured home and starts idle presentation. |
| `MoveToInternal` | Validates the NavMesh, samples a legal destination, accepts either NavMesh remaining distance or planar distance, detects invalid paths, and enforces a travel timeout. |
| `FaceMovement` | Rotates the character root toward NavMesh velocity every frame. |
| `UpdateIdlePresentation` | Turns idle lobby staff toward changing restaurant targets and controls `Happy Idle` timing. |
| `StartHappyIdle` / `StopHappyIdle` | Cross-fades the added animator state without allowing it to override movement. |
| `RunTask` | Owns reaction timing, task execution, automatic home return, and busy-state release. |

The Animator is driven through the existing `Speed`, `IsMoving`, and
`IsCarrying` parameters. `Player.controller` also contains a `Happy Idle`
state that references the imported `Happy Idle.fbx` clip. Only Host, Waiter,
and Busser enable this randomized idle layer in the current pass.

## Lobby Task Ownership

### `LobbyAutonomousService`

| Method | Staff | Task |
| --- | --- | --- |
| `TryStartHostTask` | Host | Finds the front waiting group and an available booth. |
| `SeatGroup` | Host | Stops at `hostCustomerClearance` outside the group, greets them, walks to the booth, and assigns seating without entering the customer cluster. |
| `TryStartWaiterTask` | Waiter | Prioritizes held items, ready takeout bag delivery, dine-in payment, takeout payment/order, then dine-in food, bills, and orders. Finished tables cannot be starved by newly arriving work. |
| `TakeOrder` | Waiter | Faces the table, records the order, carries the ticket to the cashier station, and starts kitchen processing. |
| `ProcessDineInOrderAtCashier` | Waiter | Completes a held dine-in ticket at the cashier and can be retried without losing the customer's order. |
| `TakeTakeoutOrder` | Waiter | Walks to the front takeout customer, records the order, and carries its ticket to the cashier station. |
| `CompleteTakeoutPaymentAtCashier` | Waiter | Validates the active takeout phase, moves to `CashierBoothInteractable.StandPoint`, performs the handoff, and requests local automated payment. An unreachable stand point uses one validated fallback attempt instead of retrying forever. |
| `DeliverTakeoutBag` | Waiter | Collects the matching kitchen bag, returns to the front customer, and completes the existing queue departure flow. |
| `DeliverFood` | Waiter | Collects the matching tray, faces the group, and serves the correct booth. |
| `DeliverBill` | Waiter | Collects one printed bill, delivers it, and blocks duplicate delivery. |
| `DeliverPaymentToCashier` | Waiter | Walks to the paying group's booth with the serialized payment-route timeout, confirms arrival, faces the group, and only then collects its money. |
| `CompleteHeldPaymentAtCashier` | Waiter | Returns money to the cashier station with the same payment-route allowance and requests validated payment completion. |
| `TryStartBusserTask` | Busser | Prioritizes used trays before dirty empty booths. |
| `CleanTrayAtSink` | Busser | Walks to the tray's source booth, confirms arrival before pickup, then carries it to the sink and records cleanup. It does not erase a separate customer mess. |
| `CleanBooth` | Busser | Faces an empty dirty booth and starts its existing visible hold-to-clean controller. |

After each complete coroutine, `AutonomousStaffBot.RunTask` returns that
staff member home before another task can start.

## Takeout Flow

Takeout remains locked until the configured campaign unlock day. Once enabled,
the autonomous path uses the existing state owners instead of duplicating them:

```text
TakeoutQueueManager
  -> TakeoutFlowManager.WaitingForOrder
  -> Waiter takes order
  -> CashierRegisterUI.CompleteAutomatedPayment
  -> KitchenManager creates matching bag
  -> Waiter delivers bag
  -> TakeoutQueueManager releases customer to exit
```

`TakeoutFlowManager.SetAutomatedService` suppresses only the player-facing
cashier panel. Finance, order statistics, kitchen processing, bag validation,
customer result reporting, and queue departure still run through their normal
systems. `AreWaiterHandsFree` prevents takeout bags from sharing the waiter hold
point with tickets, trays, bills, or money.

Every active phase now has a bounded lifetime. `WaitingForOrder`,
`WaitingForPayment`, `WaitingForKitchen`, and `WaitingForBagDelivery` each record
their entry time and release the failed group if their configured timeout is
exceeded. `TakeoutQueueManager` separately limits travel to the front order point.
This prevents a bad destination, missing interaction, or destroyed runtime item
from reserving the waiter and queue indefinitely.

`KitchenManager.ProcessOrder` returns whether cooking actually started. A kitchen
slot wait is capped by `maxSlotWaitSeconds`, and a spawned bag must contain
`TakeoutBagInteractable` and be accepted by the active flow. `OrderFinished`
notifies `TakeoutFlowManager` about a failed cook or spawn. Any rejected payment,
missing register, unavailable kitchen, slot timeout, or invalid bag releases the
affected customer as a neutral result and advances the queue instead of leaving
the waiter or front customer locked.

## Takeout Queue Movement

`TakeoutQueueManager` assigns each customer group a queue center and orientation.
`CustomerGroup.MoveToTakeoutPoint` expands that center into individual positions:
one centered member, two side-by-side members, a three-member triangle, or two
rows for four members. Groups beyond the authored queue-point count continue
behind the final point using `overflowGroupSpacing`, so they never share one slot.

Every multi-row formation stays at or behind its authored point. No member is
placed forward of `OrderPoint` toward the cashier counter, where the nearest
NavMesh surface can belong to the unreachable side of the counter.

Each formation position is sampled onto the NavMesh before movement and the
resolved result is cached per `CustomerAgent`. Arrival checks use these cached
positions, not the original unsampled coordinates. This is required near the
cashier counter, where Unity may legally move a requested point onto the nearest
walkable surface. `CustomerAgent.TryWalkTo` also requires a complete calculated
path before accepting the destination. The queue retries one stalled front route;
a group that still cannot reach the front leaves neutrally without displaying an
unhappy patience reaction because service patience has not started yet.

`CustomerAgent.UpdateMovementCompletion` stops and resets its NavMesh path as soon
as the destination threshold is reached. `StopAtCurrentPosition` also writes zero
to the existing `Speed` animator parameter immediately. The queue calls this when
entering `AtOrderPoint` or `WaitingInQueue`, preventing the walk animation from
continuing while a customer waits.

Takeout waiter tasks use `AutonomousStaffBot.MoveWithin` with a wider NavMesh sample
radius around the customer, cashier stand, and prepared bag. Service completion is
measured with context-specific distances: 2.75 units at booth approach points and
1.75 units at counters, pickups, and takeout interaction points. This accounts for
the waiter agent's collision-avoidance clearance around booth and counter geometry
without allowing one nearby station to complete another station's task. Failed order
or bag routes call `FailTakeoutService`; held bags are destroyed and carrying state
is cleared before the bot returns home. They are never silently retried forever.

The waiter coordinator also resumes a held dine-in ticket, tray, or bill before
selecting new work. A failed route therefore preserves the current assignment for
the next attempt instead of leaving occupied hands that block every later task.

Takeout groups receive stable per-member avoidance priorities. The Waiter uses a
lower navigation priority than customers, stops three world units from the group
center, and ignores physical collision pairs with the active group while taking
an order or delivering a bag. This keeps queued customers fixed in their slots
instead of letting the Waiter's one-unit agent and collider push through them.

The normal lobby-line patience path explicitly excludes takeout groups. Takeout
order patience begins only through `BeginTakeoutOrderFlow`, after the front group
has reached `AtOrderPoint`; travel to the queue cannot drain that timer.

`GroupSpawner` writes one `[GroupSpawner] Routing ...` line per spawned group. The
line includes the takeout enable flag, queue assignment, random roll, configured
chance, and final `selectedTakeout` result. This distinguishes a failed takeout
flow from a test run where the 20 percent takeout chance selected only dine-in
groups.

## Messy Customer Cleanup

Blue customers continue to create booth mess through `Booth`. The Busser first
removes the used food tray at the sink. Once the table is empty,
`Booth.BeginAutomatedMessCleaning` starts `BoothMessCleanUI`, which keeps the
clean bubble visible, labels it `Cleaning...`, and advances the same radial timer
used by manual hold-to-clean. The UI calls `Booth.CleanMess` only when its timer
reaches completion.

## Day Closeout

`GameDayManager.ServiceActive` remains true while a shift is running and
while the restaurant is closing out. At zero time, customer spawning stops,
but `LobbyAutonomousService.ServiceLoop` continues assigning tasks until all
existing groups finish and leave. `ShowResultsWhenClear` then disables
closeout service and presents the day report, allowing the next-day button
to call `GameFlowManager.CompleteRestaurantDay` without deadlocking.

## Pickup UI Scale

Tray, bill, and takeout pickup prefabs retain their authored root scale when
instantiated. Runtime code no longer replaces that scale after
`UIBounceAnimator` caches it, so hover and click feedback always return to
the same size.

## Kitchen Presentation

### `KitchenManager`

`ProcessOrder` remains the cooking authority. It now publishes:

- `OrderStarted(group, orderNumber)` after a valid cooking coroutine starts.
- `OrderFinished(group, orderNumber, succeeded)` when that coroutine exits.

These events do not change cook duration, order validation, or tray spawning.

### `KitchenWorkerBot`

| Method | Responsibility |
| --- | --- |
| `BindKitchenManager` | Subscribes the worker to the active scene kitchen. |
| `HandleOrderStarted` | Adds the order number to the worker's active workload. |
| `HandleOrderFinished` | Removes the completed or failed order from the workload. |
| `Update` | Starts visual work only when orders exist and the worker is idle. |
| `WorkWhileOrdersAreActive` | Moves through that worker's exclusive work points while the kitchen has active work. |

Chef uses `ChefPrepPoint` and `ChefCookPoint`. Barista uses
`BaristaDrinkPoint` and `BaristaServePoint`. They no longer share points or
patrol while the kitchen is idle. With no serialized home assigned, each
returns to its own scene-start position and rotation.

## Navigation Priorities

Lower values have higher Unity NavMesh avoidance priority:

| Staff | Priority |
| --- | ---: |
| Host | 30 |
| Waiter | 80 |
| Busser | 50 |
| Chef | 60 |
| Barista | 70 |

The Cashier receives no autonomous movement component and remains stopped
at the register.

## Related Runtime Guard

`QuickOutline.Outline` now skips runtime normal/UV writes for meshes whose import
setting has `isReadable == false`. Those customer meshes can still render, but no
longer throw the repeated `uv4` exception seen when Blue customer models spawn.

## Validation Checklist

1. Start `Lobby1` and confirm all moving staff face their travel direction.
2. Confirm Host, Waiter, and Busser return to separate home points after tasks.
3. Confirm Cashier never leaves the register.
4. Confirm Chef and Barista remain home before the first order.
5. Submit an order and confirm both kitchen workers use separate routes.
6. Confirm both kitchen workers return home after all active orders finish.
7. Confirm the Waiter order, food, bill, and payment workflow is unchanged.
8. Let the timer reach zero with customers still inside and confirm bots finish serving them.
9. Confirm the day report appears after the final group leaves and advances to the next day.
10. Confirm bill and tray pickup buttons remain the same size before and after hover/click.
11. Use the day debugger to select day 20 or later and confirm a takeout customer queues at the cashier.
12. Force takeout chance to `1`, spawn groups of two to four, and confirm members use separate queue positions at or behind each marker and switch to idle after arriving.
13. Confirm the Host stops outside the group and does not push customers while greeting them.
14. Confirm the Waiter takes the takeout order, visits the cashier booth stand point, delivers the matching bag, and the customer exits.
15. Temporarily fill every takeout spawn point and confirm the failed order exits after the configured timeout while the next group advances.
16. Confirm each spawned group logs `selectedTakeout=True` or `False`; with the default `0.2` chance, enabling takeout does not force every group into that queue.
17. Temporarily move the cashier stand off the NavMesh and confirm the payment fallback advances or releases the group without repeated movement warnings.
18. Use the day debugger to select day 10 or later and wait for a Blue customer to leave a table mess.
19. Confirm the Busser removes the tray first, then the `Cleaning...` bubble and radial timer remain visible until the mess clears.
20. Confirm the Console contains no repeated NavMesh, UV4, takeout-flow, or autonomous-service errors.
21. Confirm a takeout group remains happy while walking to the line and only begins order patience after reaching the front.
22. Confirm the Waiter stops outside the takeout group and does not displace any waiting customer while taking an order or delivering a bag.
23. Confirm a Waiter already standing beside a booth takes the order or delivers the bill after the short work animation, without waiting for the customer patience or movement timeout.
24. Temporarily obstruct a waiter route after pickup, remove the obstruction, and confirm the held ticket, tray, or bill is retried instead of locking all waiter work.
