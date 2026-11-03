# Multiplayer return, guest pickup and solo day ending — 4 November 2026

## Implemented behavior

- A guest can return to the original live run through **Rejoin Session**. TTL-zero rooms use a normal exact-room join followed by the existing account/roster admission; retained inactive Photon actors still use actor rejoin. An actor-rejoin rejection with -3 or 32748 has one ordinary-join fallback. Missing/full/closed rooms do not create replacement sessions. Kicked/departed accounts and ended runs remain ineligible.
- Food tray projection retains its prefab pickup point when no kitchen override exists, matching the authority's tray creation. Casual Dining's multiplayer kitchen now references the same authored floor approach as its campaign kitchen. Fast Food's pickup override and Fine Dining's kitchen/bar approaches remain in place. The host still validates ownership, order, stage, actual received player position and occupied hands.
- A multiplayer shift already in progress can finish with one remaining player. Its successful completion and personal reward presentation finish normally. If fewer than two valid connected participants remain when results are ready, the host closes the run with `InsufficientPlayers`, closes the room and preserves the completed day. Big Boss explains that multiplayer cannot continue alone; acknowledging the existing dialogue returns to the menu. There is no Day 2 advance from a solo Day 1 finish.
- Starting/preparing another shift and committing Next Day both require at least two valid connected participants. Inactive, removed, departed and merely reserved seats do not satisfy the requirement. A withdrawn/disconnected participant cancels an in-flight countdown. A teammate returning before the day finishes permits normal continuation once fully loaded and ready.
- Rejected service actions now include operation/order/outcome diagnostics in release logs. No credentials or authentication values are logged.

## Changed files

Runtime and scene files under `Assets/_Project/`:

- `Restaurant/Items/FoodTrayInteractable.cs` and `Scenes/RoleBased/Lobby1 Multiplayer.unity`
- `Networking/Photon/MultiplayerMenuController.cs`
- `Networking/Multiplayer/MultiplayerLastRun.cs`, `MultiplayerSessionManager.cs`, `MultiplayerSessionManager.Recovery.cs`
- `Networking/Multiplayer/MultiplayerDayBridge.cs`, `MultiplayerDayReadiness.cs`, `MultiplayerSessionUI.cs`, `MultiplayerDiagnostics.cs`
- `Gameplay/GameManager/GameManager.cs` (refresh the existing ended-results action)

Regression coverage: existing Editor reconnect, readiness and service sequence cases; `Backend/Tests/MultiplayerReturnRegression.cs`; `Backend/Tests/MultiplayerServiceGuards.tests.cjs`; the existing reporting harness and PlayFab contract tests. The harness includes the current reward fields so its production ranking adapter compiles; test boundaries remain in memory/scratch files.

`Backend/PlayFab/MultiplayerRunsV2.js` accepts the new final reason. **Merge this change into the title's existing live CloudScript revision for online final-run reporting.** The local source update does not deploy it. Gameplay stops locally regardless; an older live handler rejects the new final report and leaves it queued for later acknowledgement. Do not replace other title handlers when publishing.

## Checks actually run

- Current runtime and Editor source compilation against the project's cached Unity reference assemblies: zero errors. Runtime emitted 36 warnings; Editor emitted zero. Output is isolated under `G:/Unity Projects/DineIn/.codex-checks/MultiplayerReturnPickupCompile/`; no player-build helper was called.
- `node Backend/Tests/run-headless.cjs`: 115 new source-linked return/admission/readiness assertions plus the existing 19 reporting scenarios / 115 assertions passed. Photon, Unity and PlayFab boundaries are test doubles.
- `node Backend/Tests/MultiplayerServiceGuards.tests.cjs`: five static scene/reference/routing checks passed, including all three restaurants' session/day bindings, pickup approaches, reward-before-ending guards and both reconnect entry points.
- `node Backend/PlayFab/RunReporting.tests.cjs`: 15 mocked scenarios passed, including all three restaurants' solo ending and refusal to add another day after finalization.
- `node Backend/PlayFab/MultiplayerRunsV2.tests.cjs`: 16 existing isolated backend contract checks passed.
- Targeted diff whitespace checks passed.

No Windows/Android player build, installation, desktop control, live room test, native Unity navigation/UI walkthrough or live CloudScript deployment was performed. Existing binaries do not contain these changes. Build profiles, account saves, purchases and earned unlocks were not edited by this task.

## Manual acceptance after rebuilding both players

Use separate accounts. Repeat in **Casual Dining, Fast Food and Fine Dining**, with Windows hosting Android and Android hosting Windows; include a four-player mixed team.

1. As the guest, greet/seat, take and confirm an order, pick up its numbered counter tray, deliver it to the matching table, handle bill/payment, and clear/wash its used tray. Confirm both devices show the same carrier, order and completion exactly once. In Fine Dining repeat at the bar; in Fast Food test table-delivery orders and normal customer self-pickup separately.
2. Have two people claim the same tray, cancel an approach, attempt pickup with occupied hands and approach from opposite sides. Invalid pickups must reject cleanly; a valid guest at the authored floor point must succeed. Retry after an actual rejection, with no duplicate food or order charge.
3. Disconnect a guest while the host remains. Return through **Rejoin Session** before the day ends; verify host admission, restored player controls, existing order state and no duplicate task/reward. Repeat Android background/restore. Kicked/departed accounts and a run whose host left must remain rejected.
4. Leave the host alone during service. The current shift must continue until the final customer exits. Finish/continue the personal reward presentation, then verify Big Boss's ending, no next-day increment, and return to menu. Run credit must retain the completed day without granting another. Rejoining this ended run must fail.
5. Repeat disconnection while results are open, during the next-day click, and during Ready countdown. A reserved/disconnected seat cannot authorize another day. With two or more valid, loaded players present, next-day preparation and Ready countdown must still work.
6. Check Big Boss framing and dialogue advancement on Windows and Android safe areas. If a valid service action still fails, keep both devices' logs from that attempt, including `[MultiplayerAction]` and `[Room]` entries.

Live cross-device gameplay acceptance remains **NOT RUN**; source compilation and mocked/static checks do not establish bug-free multiplayer.

## Follow-up: guest bill pickup

The reported bill-only failure exposed two billing omissions. `MultiplayerCustomerInteractionBridge.TryHandleBill` retained an owned claim but resumed only a bill already held; selecting that task while its printed paper remained on the counter silently did nothing. It now calls the existing `BeginBillApproach`, which selects request, physical pickup or delivery from the current paper/hand state. Pending operations and ongoing approaches remain protected against duplicate submissions.

`BillManager.PresentMultiplayerBill` now applies `billPickupApproach` to new and existing guest copies, matching the host's `PrintLoop`. Where no override is authored, the prefab's stand point is preserved. Claim leases, host distance checks, paper attachment confirmation and payment authority are unchanged. Both changes apply to the shared billing path used by all restaurants.

Follow-up checks: runtime and Editor source compilation passed with zero errors (36 runtime warnings, zero Editor warnings), six static scene/routing checks passed including the new billing case, and targeted diff whitespace checks passed. Compilation output is isolated in `G:/Unity Projects/DineIn/.codex-checks/GuestBillPickupCompile/`. No player build, native Unity fixture or live two-device billing test was run.

After rebuilding both devices, claim a bill as the guest, wait for printing, then test both entry points: the customer's owned bill task and the counter's PICK UP button. Verify the guest picks up the same numbered paper, delivers it, and the host sees each transition once. Repeat with the host as waiter and across all restaurants that expose the printed-bill flow. This bill fix does not require a backend change.
