# Multiplayer human service parity repair

Implementation handoff: **2026-09-16**, Unity **6000.0.40f1**.

## Status

Source implementation and opt-in external regression runners are prepared. **Compilation, multiplayer gameplay acceptance, and device performance have not been run. This is not a verified-playable sign-off.** No Unity launch, build, GUI test, gameplay test, account operation, or backend deployment was performed on the user's computer.

### Cashier follow-up

The multiplayer scene authors `CashierRegisterUI` inactive. The earlier payment reply used only its not-yet-initialized singleton, silently discarding an accepted register-open action. The follow-up resolves and initializes the authored inactive register, including when a guest pays before the host has ever opened it. Arriving with cash now requests the register once per payment/visit, and cashier interaction keeps the player at the counter. Explicit clicks can retry; cancelling does not trigger continuous reopening. Clicking the cashier with empty hands can collect an available printed bill through the existing bill claim path. Gameplay ownership text says **staff**.

An existing guest log also exposed wallet UI cleanup calling a destroyed wallet manager during scene unload and cash destruction trying to reparent the dying cash object. Cleanup now checks the wallet's Unity object and clears cash holder references without reparenting during destruction. Normal cash transfers and returns still restore their intended parent. No new networking protocol or service architecture is introduced by this follow-up.

## Changes and repaired failure paths

### Guest smoothness and post-cashier consistency follow-up

- Existing guest logs show a completed payment followed by an occupied-hands rejection. The claim guard previously trusted raw held-item fields even when an item had been delivered or reparented. Local interaction checks and the authority now reconcile proven-stale references before checking occupancy, preserving real carried items and current owners. Remaining genuine blockers identify the item and its next destination (cashier, customer, or sink).
- Guest pose interpolation now accounts for packet transit time while retaining 100 ms of interpolation history. Transit allowance is bounded, render time does not run backwards, and long gaps recover without an accumulating delay. The sender rate and message layout are unchanged; interaction distance still uses the latest authoritative position.
- Guest customer procedural animation now advances in waiting, ordering, and billing states as well as eating. Old movement packets cannot overwrite newer animation state. Guest customer navigation and service simulation remain disabled.
- Approval-only economy updates no longer rebuild an unchanged guest transaction ledger or refresh all money UI subscribers. Inventory signatures serialize only inventory fields; ledger changes with an unchanged balance still apply.

Prepared external regressions cover delayed movement, stale versus genuine carried items, and unchanged versus updated ledgers. Repeat **cashier completion → next customer task → dirty-tray pickup/wash → next task** independently on host and guest, including 100–300 ms transit and jitter. Compilation, these fixtures, gameplay, and FPS remain unexecuted locally under the user's restriction.

| Area | Implemented behavior |
| --- | --- |
| Human order confirmation | Single-player and multiplayer authority use `ReviewedOrderSubmission`. Requests contain selected menu IDs and quantities; catalog prices, recipes, and stock rules stay authoritative. Normal cooking starts immediately after confirmation, regardless of whether a bot or human seated the customer. |
| Rejected submission | Ingredient deductions commit together. A rejected kitchen acceptance restores the original quantities and stock batches, including age/storage identity, plus review state and remake order identity. Retries cannot consume stock twice. |
| Prepared food | Normal kitchen output registers its canonical tray and pickup slot. Completed forecasts remain queryable while that tray exists, so human pickup validation accepts completed jobs. Guests project the same prepared result; they do not cook another order. |
| Food pickup/delivery | Commands carry action, actor, order, stage, and lease identity. Authority rechecks arrival for up to 400 ms using the latest received position, before interpolation. Claims remain attached to carried food until successful delivery or recovery. Delayed replies cannot warn the wrong actor or complete a replacement action. |
| Bill pickup/delivery | Printer paper remains canonical. Successful pickup waits for actual matching holder projection before enabling delivery. Interrupted delivery retains the bill and lease. Customer-body clicks route a held bill to bill delivery, and clicks over UI do not also trigger that world action. Explicit abandonment/disconnect uses the retained printer slot. |
| Cash/card | Payment commands validate stage/lease and recheck arrival for up to 400 ms. Cash retains one identity through collection, transfer, cancellation, and return. A live carried payment does not expire merely because 90 seconds elapsed. Cashier input made while a pickup attachment is arriving is queued only when explicitly clicked. Settlement remains once-only. |
| Input and feedback | Hand-state changes refresh carrying presentation without replaying the last click. Redundant legacy proximity receivers remain disabled; the cashier itself opens once when the local player arrives with cash. Actor/action-matched replies provide specific rejection reasons; shared simulation does not emit another actor's order-processing or stockout action toast. Shared tray availability is independent of another player's held item. |
| Recovery | Physical holder lookup includes inactive ancestors, allowing disconnect/disabled-avatar cleanup to clear the real prior holder. Guest bill/cash projection cannot complete a gameplay claim or issue a service command. Legacy single-player virtual money clearing is retained. |

### Compatibility

- All participants need the repaired code: room protocol is **`casual-session-4`**.
- Result rules remain **`casual-session-2`**; pending longest-run receipts retain their existing contract.
- Session-only progress, Day 31+, host-loss termination, 90-second guest rejoin, and result recording retain their agreed behavior.
- Existing room connectivity and restock repairs are preserved. No new authentication/dashboard requirement, website change, backend deployment, or additional gameplay feature is included.

## External regression runners

Use a disposable external checkout and isolated external OS profile with Unity 6000.0.40f1. The fixtures use isolated scenes and no Photon room or production account. Importing their scripts does not start a test. Run each entry point in a separate Unity process:

| Entry point | Coverage | Artifact |
| --- | --- | --- |
| `MultiplayerOrderRegressionTest.RunBatch` | Actual cooking coroutines after both seating flags; one prepared tray and completed forecast; selection quantities; duplicate confirmation; missing stock; exact inventory/review/remake rollback after rejected kitchen acceptance | `Artifacts/MultiplayerOrderRegression.txt` |
| `MultiplayerInputRegressionTest.RunBatch` | Hand notifications cannot dispatch commands/cancel targets; local feedback ownership; authoritative movement ahead of interpolation; stale/reset samples; food reply actor/action isolation and immutable completed receipts | `Artifacts/MultiplayerInputRegression.txt` |
| `MultiplayerRepairRegressionTest.RunBatch` | Earlier claim/bubble/registry checks plus bill projection readiness, action context isolation, physical holder transfer, same cash object return, presentation without claim completion, legacy virtual payment clearing, and authored-inactive cashier initialization/first opening without resetting payment input or multiplying listeners | `Artifacts/MultiplayerRepairRegression.txt` |

Example for the **external runner only**:

```powershell
& $ExternalUnityEditor -batchmode -nographics `
  -projectPath $ExternalCheckout `
  -executeMethod MultiplayerOrderRegressionTest.RunBatch `
  -logFile $ExternalLog
```

Do not add `-quit`: the runners enter Play Mode and exit themselves with status 0/1 when checks finish. Preserve the full Unity import/compile log and each result artifact. A source delimiter/whitespace check is not compilation, and fixture success does not replace real network controls.

## Required player-control acceptance

Run this on external host/guest clients with the same protocol. Repeat the service paths as **each player**, with bots prevented from substituting for the steps under verification.

1. Seat one group with a bot and another with a human. Each human reviews and confirms an order for both groups. Verify normal preparation/cooking delay, one stock deduction, one prepared tray on both screens, pickup, and delivery to the correct customer.
2. Request multiple bills, including a full printer. Pick up each paper, deliver via its bubble and customer-body click, interrupt delivery movement, then retry. Verify the same paper/owner and correct next action on both clients.
3. Starting with the register UI inactive and without first opening it on the host, complete cash pickup, arrival at the cashier, exact change, and card payment as each player. Click the cashier while a successful pickup reply precedes hand attachment. Stay at the counter while the register opens. Cancel/retry without continuous reopening or repeated warnings; verify one financial settlement. Pick up a printed bill by clicking the cashier with empty hands, then deliver it.
4. Race two humans and a bot for a task. Try another task while carrying. Disconnect during printing, paper pickup, bill delivery, cash carrying, and card/register interaction. Verify recoverable items and exactly one valid owner.
5. Introduce latency, jitter, loss, and duplicate/out-of-order action delivery. Valid arrivals within the 400 ms recheck succeed; genuinely distant actors fail. Old replies do not show warnings, steal items, reopen paid tasks, or affect another actor's panel.
6. Apply repeated snapshots while the pointer remains over an old target. Expect no new movement, pickup, service command, or action warning without new input. Confirm shared task state while each player holds different items.
7. Complete two days and a fresh second run; check saved single-player isolation and the established disconnect/result rules. Repeat single-player reviewed orders, bills, cash/card, and existing restock/bubble controls.
8. Retain the earlier busy four-player Android performance gate: sustained 30 FPS target, no growing network backlog, recurring synchronization stalls, multiplying bubbles, or log flood. Record hardware, build configuration, and CPU/GC/network traces.

## Sign-off

Record compile/import result, runner artifacts, host/guest control results, network conditions, and device measurements separately. Any failed control path blocks playable sign-off. Source changes alone cannot guarantee perfect runtime behavior.
