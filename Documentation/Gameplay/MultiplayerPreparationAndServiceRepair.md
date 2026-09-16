# Multiplayer preparation and service sequencing repair

## Status

Source implementation only. No Unity launch, build, gameplay, GUI or test execution was performed on the user's computer. Compile and runtime acceptance remain pending on an external Unity **6000.0.40f1** runner. Do not infer playable sign-off or measured performance from source checks.

Room protocol is `casual-session-5`; result rules remain `casual-session-2`. Both players need the same updated client. Session-only progress, Day 31+ counting, host-loss termination, 90-second guest rejoin, single-player saves, and pending result receipts retain their existing contracts. No website/backend changes.

## Implemented behavior

### Initial stock and newspaper

- The Day 1 stock came from `InventoryManager` granting starter boxes, not the restock label. Fresh multiplayer initialization now explicitly grants zero units and zero batches and skips campaign refill migration. Ingredient definitions and single-player starter stock are unchanged.
- Later days and guest rejoin apply actual purchased inventory. No daily stock reset was introduced. Ordering/delivering boxes continues through the existing physical storage rules.
- Newspaper display acknowledges the exact issue/day as soon as its content is visible. Finishing or interrupting an animation cannot mark another issue. Shared read acknowledgments use a retained, independently retried request alongside any pending management edit.

### Day start

1. Host uses the computer's Start Day and existing pre-open checklist. Missing stock/preparation still blocks starting. A stock-room visit must finish first.
2. Host creates a request containing the current day and loaded roster; host counts as ready. Every participant sees a centered input-blocking prompt. The guest button displays `READY n/N`, using the existing Blue/Double sprites, Anton font and ButtonAnimator press effects.
3. Preparation edits are rejected while the request is open. Pause remains accessible and owns exiting. Cancelling leaves the existing computer/newspaper panel available.
4. All participants ready starts a three-second countdown anchored to Photon time. There is no waiting timeout or second host click. Withdrawing readiness resets the countdown; becoming ready again starts a full three seconds.
5. Host rechecks the normal checklist and starts once. Computer/newspaper close, and everyone receives `Day X has started!` through the existing warning UI.
6. Disconnect/load-roster change cancels the whole request. Old request IDs and vote sequences cannot affect its replacement. The host must explicitly request again for the reduced roster. Rejoin does not replay old announcements. Host loss still ends the run.
7. Next day requires a new host request and new votes. The bottom status/Leave Run bar is removed; actionable connection changes use existing warning feedback.

### Service feedback

- Tray snapshots carry their complete interaction mode. Carried/served trays cannot retain Delivery pickup UI. Current customer eating/billing state also suppresses a late stale delivery snapshot. Legitimate complaint/departed-customer cleanup remains available.
- Successful dirty pickup waits for the initiating actor's matching hand attachment, then queues that actor's sink trip once. Disposal retains its claim until acknowledgment; automatic return-home cannot race a pending disposal. Cancellation leaves the carried tray available for sink retry. Accepted disposal cannot be undone by a delayed replica.
- Greet hides on the initiating click/approach, authority confirms the actual greeting at arrival, then table selection follows. Rejection/cancellation restores availability. No old pointer click is replayed by these updates.
- Ownership text is removed from action bubbles, while action gating remains. One circle follows each working human/staff actor. It spins during travel/carry/unknown duration and fills only from actual timed work. It has no pointer input, and hides during idle, reset, despawn and local restock. Existing claim snapshots carry changes; no animation RPC loop was added.

## External verification

The explicit `MultiplayerRepairRegressionTest.RunBatch` runner now includes prepared inventory/newspaper, readiness, tray-mode and work-indicator fixtures. These are not executed on import. Retain the previous order/input/payment runners and acceptance cases from `MultiplayerHumanServiceParityRepair.md`.

| External check | Required result |
| --- | --- |
| Fresh two-account run and second fresh run | All host/guest ingredient quantities and batches zero; no phantom starter boxes. Single-player stock and saves unchanged. |
| Purchase, truck, storage, Day 2, rejoin | Only physically stored stock is usable; same counts/batch identities on host/guest, no reset/refill/duplicate units. |
| Newspaper from computer and HUD | Current issue counts once before animation finishes, including an interrupted opening; archive does not mark today. Another player's pending management edit cannot lose the read. |
| Readiness on 2–4 clients | Host checklist, centered Blue/Double guest Ready button, same fraction, 3–2–1, one start, and day-start message. No bottom bar. |
| Readiness interruption | Unready during countdown restarts at a full three seconds when ready again. Host cancel restores panels. Disconnect cancels, rejoin/reduced roster requires a fresh host request. No stale/double votes or duplicate start. |
| Input/pause while ready | Underlying preparation cannot change. Stock-room owner blocks request. Pause exit still works, including while another local panel is open. Start closes preparation panels correctly. |
| Food serving | Each human orders, picks up and delivers; guest pickup vanishes immediately on served/carried state. Reordered/duplicate tray/customer updates cannot restore it. |
| Cleanup on host and guest | Each human picks up dirty tray and automatically reaches sink, disposes once, releases hands/claim and can do the next task. Cancel/unreachable sink retains retry; disconnect returns tray safely; complaint cleanup still works. |
| Greet/seat | First-line customer shows greet; click hides it promptly; arrival greets and offers table choice. Rejection, occupied tables, cancellation, ownership races and delayed replies recover. No duplicate bubbles. |
| Staff/human circles | No ownership prose on bubbles; one noninteractive circle per working actor, truthful timed fill, no idle remnants or restock overlap. |
| Existing service regression | Cashier opens/settles cash and change; card, bill pickup/delivery, order review and food work through actual controls as both humans without staff substituting. No occupied-hands softlock. |
| Session and performance | Two days and second run; host loss/guest expiry and result isolation unchanged. Busy four-player Android run targets sustained 30 FPS, no growing backlog, log flood or multiplying UI. Capture CPU/GC/network traces and visual movement results. |

Record import/compile output, fixture artifacts, host/guest input results, latency/jitter conditions and Android hardware/build details. Any failed player-control path blocks runtime sign-off.
