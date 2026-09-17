# Shared stockout patience

Implemented in source; external Unity 6000.0.40f1 compilation and gameplay acceptance remain pending. No local Unity launch, build, or test execution.

- Shared catalog/ingredient availability controls automatic arrival speed (one third during a global shortage). Existing shift caps and scripted spawns are unchanged.
- Each affected group gets one 50% patience roll. Patient groups retain their location for a cumulative 30 scaled seconds, with ordinary ordering/queue timers paused. Takeout's order-phase timeout is also paused.
- Existing thought/leave bubbles, mood sprites and patience UI are reused. Waiting grants no outcome. Stockout departures count once as unsatisfied and failed service, never completed orders.
- Recovery generates an available selection and returns to normal service; it does not spend stock, confirm orders or start cooking automatically. Regenerated orders receive fresh identities. A later shortage cannot reroll patience or replenish its allowance.
- Availability is cached for up to 0.5 seconds and invalidated by inventory/menu changes. Insufficient stock for just one group retains the existing refusal behavior and does not throttle all arrivals.
- Multiplayer authority decides waiting/outcomes. Waiting state and remaining time travel in existing service snapshots. Room protocol is casual-session-8; result rules remain casual-session-2.

## External acceptance required

Run the existing opt-in repair runner, including StockoutRegressionCases. Then validate real controls on single-player and 2–4 multiplayer clients for every authored restaurant catalog:

1. Empty stock, disabled menu, missing required drinks and bundles with shared ingredients: no false availability; arrivals take three times their normal interval.
2. Force both patience outcomes externally: correct existing happy/unhappy mood assets, singular/group wording, retained seats/queue position and exactly one unsatisfied result on departure.
3. Restock before timeout: waiting UI clears, ordinary controls resume, one kitchen submission/stock deduction occurs only after confirmation. Restore and exhaust stock repeatedly: no renewed 30 seconds or extra patience roll.
4. Staff and human ordering, including human-seated readiness-only groups and stock lost during a human review: no stuck claim, notepad, staff callback or duplicate order.
5. Competing groups and only enough stock for one meal: no double consumption; an unserviceable larger group cannot wait forever.
6. Takeout order timer and line patience do not expire during the extra allowance; existing timers resume without resetting on recovery.
7. Pause/speed changes, closing, next day, fresh run, guest reconnect and delayed snapshots: no stale waiting UI, guest outcome mutation or settlement blockage.
8. Recovery restores normal arrival speed without a catch-up burst; tutorial-scripted spawns retain tutorial control.

No FPS or runtime playability claim is implied by source checks.
