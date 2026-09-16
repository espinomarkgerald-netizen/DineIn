# Multiplayer task tracking and day-start repair

## Implementation and verification status

Source changes only. No Unity launch, compile/build, gameplay tests, GUI automation or test execution on the user's computer. External verification requires Unity 6000.0.40f1. Protocol is now `casual-session-6`; result rules remain `casual-session-2`. Both clients must use the updated version and create a new room.

## Changes

- **Ticket ownership:** `TakeOrderFromWaiter` and `SpawnTicket` now require explicit receiving hands. Staff tickets cannot be assigned to the local human. Ticket UI retains its actual owner. Human reviewed orders still submit directly to the existing kitchen. Reconciliation removes accepted/completed or wrongly owned legacy ticket references without submitting anything or touching another actor's hands. An unsubmitted `OrderTaken` ticket remains valid.
- **Activity:** the HUD, claim eligibility and human activity-circle phase use a read-through view of hands, pending actions, movement and existing claims. No second task database was introduced. Pending confirmation/attachment is distinguishable from idle, and real held items retain their normal completion requirements. Existing leases, movement-command versions, cancellation and day/disconnect recovery remain in charge of transitions.
- **Day start:** snapshots explicitly identify whether readiness is active. Running/closing/results states discard leftover readiness data rather than rejecting the whole update, including JsonUtility's empty nested-object representation. Newer revisions clear the prompt and preparation lock together with the observed day state. Guests reread cached day state at one-second intervals and request a restaurant baseline at most every five seconds when stuck beyond countdown or missing day dependencies. Countdown alone cannot start a guest simulation. Unresolved waits display Synchronizing.
- **Stockouts:** genuine stock-related departures use food, drink or missing-order-ingredient explanations instead of generic unattended-order comments. Both players see a shared restock warning once per shortage episode. Repeated customer departures and snapshots do not repeat it. Availability recovery rearms it; rejoining an active episode shows the current warning once. Failed generated-order requirements are retained for recovery checks, including group quantities. Existing financial/outcome rules stay intact.
- **Circles:** cache humanoid head or body bounds, center the indicator above it, use a 24-unit diameter and 4-unit thickness at reference scale. Only arc geometry spins; the transform stays stationary. Existing progress timing, mobile scaling, noninteractive behavior and restock hiding remain.

## Prepared external checks

`MultiplayerRepairRegressionTest.RunBatch` includes `MultiplayerTaskTrackingRegressionCases`: explicit staff/human ticket isolation, legitimate unsubmitted tickets, cooking-stage stale ticket cleanup, idle eligibility, JSON readiness round-trips, invalid active readiness, cancelled/running readiness, stationary ring geometry and thickness. These fixtures are opt-in and were not executed.

External player-control acceptance:

1. With host idle, let staff take/process several orders; host must remain free, with no ticket UI or submit-ticket warning. Repeat while each human independently handles bills/cash/food.
2. Each human completes greeting, seating, reviewed order, food, bill, cash/change/card and cleanup, then immediately selects a new task. Real held items still block unrelated actions with matching HUD guidance. Delayed attachment/acknowledgement must not appear idle, reopen completed work, or override a newer command.
3. Race/cancel approaches, interrupt review, destroy or depart customers, disconnect/rejoin carrying, run a second day and fresh session. Check exactly one valid owner and no permanently occupied hands or claim.
4. On two clients, finish readiness/countdown. Guest leaves Starting, sees the running clock, receives one announcement and can interact. Inject delayed/reordered snapshots, an empty readiness object and delayed restaurant baseline; older data cannot reopen Ready or roll back a running day. Cancellation, host loss and next-day readiness remain correct.
5. Test no food, missing drinks, insufficient quantities for a group, and a genuine unanswered order. Verify distinct departure thoughts. Both players receive one restock warning across repeated refusals; restoring stock and exhausting it again creates one new warning. Rejoin sees the current active shortage once.
6. Inspect circle size/position during walking, turning and camera movement on host, guest and Android; no orbiting, input interception or idle remnants.
7. Repeat single-player staff tickets and human service; saved stock/progress, multiplayer receipts, Day 31+, 90-second guest grace and host-loss rules remain unchanged. Measure the existing busy four-player Android performance target externally; source checks do not establish FPS or playable sign-off.
