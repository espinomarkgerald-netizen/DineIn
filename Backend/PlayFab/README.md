# Multiplayer run records v2

This folder contains game-account backend code. The website is outside this change. The handlers and configuration below have **not been deployed or tested against a live title**.

## External setup

1. Room connection defaults to the game's existing Photon setup, with the signed-in PlayFab ID supplied as the Photon user ID. Creating/joining rooms does not require the new backend handlers or a PlayFab–Photon custom-auth integration. For stronger identity binding, first configure the PlayFab title's Photon integration and Photon custom authentication, then enable `PhotonBootstrap.usePlayFabCustomAuthentication` on the authored bootstrap. In that explicit mode Unity obtains a PlayFab Photon token and sends `username` and `token`; failures do not silently fall back. No server secret belongs in Unity.
2. Merge `MultiplayerRunsV2.js` into the title's existing **Classic CloudScript** revision, preserving unrelated handlers, and publish the reviewed revision. It defines `DineInPublishRunV2` and `DineInClaimRunV2`.
3. Create the player statistic `CasualDiningLongestRunV2` with **Max** aggregation and **no automatic reset**. Keep client statistic writes disabled. Do not replace a differently configured existing statistic without reviewing its data.
4. On an external development runner, execute `node Backend/PlayFab/MultiplayerRunsV2.tests.cjs`. These are isolated backend contract tests, with mocked PlayFab storage. They do not verify title settings, Photon authentication, Unity behavior or live concurrency.
5. On external devices/runners, complete the acceptance matrix in `Documentation/Gameplay/MultiplayerImplementation.md`, including actual authentication and backend failures, before release.

Relevant official references: [PlayFab Photon token API](https://learn.microsoft.com/en-us/rest/api/playfab/client/authentication/get-photon-authentication-token?view=playfab-rest), [Photon PlayFab authentication parameters](https://doc.photonengine.com/pun/current/reference/playfab), [PlayFab player-data access levels](https://learn.microsoft.com/en-us/gaming/playfab/player-progression/player-data/), and [player statistics and aggregation](https://learn.microsoft.com/en-us/gaming/playfab/community/leaderboards/tournaments-leaderboards/using-player-statistics).

## Request and result contract

Both handlers accept `{ record: MultiplayerRunRecord, final: boolean }`. They return `{ accepted: boolean, bestCompletedDay: number, message?: string }` on success or `{ accepted: false, message: string }` for a retryable/rejected request. PlayFab execution/API errors also leave the client receipt pending.

The schema carries a random run ID, protocol/version, restaurant, frozen host/account/actor roster, party size, start/end timestamps, revision, highest day reached, completed days, and per-participant earned credit. It contains no restaurant inventory, equipment, employees, cash, campaign progress, authentication tickets or resumable save.

- **Completed day:** service and authoritative settlement finished, including a terminal failed shift. An unfinished day adds no completed-day credit.
- **Highest day:** the last preparation/service day reached. Finishing Day 12 then leaving during Day 13 yields `completedDay: 12`, `highestDay: 13`.
- **Guest absence:** completed days during the 90-second grace period remain provisional in the room. A timely rejoin credits them. A departed guest keeps their earlier earned completed/highest days.
- **Final:** departure, host loss, or a terminal restaurant outcome. Host loss does not migrate the run. The local guest end reason is descriptive; it cannot authorize extra score.

## Storage and writes

| Data | Location | Meaning |
| --- | --- | --- |
| `CasualDiningLongestRunV2` | Server-written player statistic, Max | Authoritative numeric personal best; future leaderboard score |
| `CasualDiningLastRunV2` | Private read-only player data | Latest run summary, including completed/highest days, outcome, versions and `AuthenticatedPlayerHost` verification |
| `CDRun2Receipt0` … `CDRun2Receipt7` | Host's internal player data | Eight bounded receipt slots, indexed by run-ID hash |
| `multiplayer_results_v2.json` | Local persistent-data directory | Account-bound pending result uploads; never used to resume a restaurant |

The authenticated host publishes earned credit directly to **every frozen participant**, including offline guests. The handler writes all participant statistics/summaries before committing its receipt. Partial failures can safely retry; Max aggregation prevents shorter or duplicate submissions from lowering a best score.

Same-run revisions cannot decrease progress, change membership/start identity, reopen an ended run, or increase a departed participant's credit. Duplicate/older host revisions use the stored receipt. Guest claims require a matching host receipt and cannot exceed its participant credit. After a receipt slot has been reused, a claim can only acknowledge an already-secured equal or better statistic; it cannot write a new score.

The latest-run summary is not a best-run summary. Future website code must read the statistic for the best score. Read-only data writes use read/compare/write, so concurrent sessions can race on descriptive metadata; the Max statistic remains the score source of truth. Account summaries and host receipts use a fixed number of keys rather than one key per day/run.

The client retains every unacknowledged host revision and the latest guest receipt. It binds retries to the original account, preserves unreadable files for recovery, uses an atomic replacement/backup when available, and prevents one pending claim from blocking later runs. Local pending storage grows while uploads cannot complete; it intentionally does not discard unacknowledged results.

## Trust and durability limits

This is a cooperative, player-hosted implementation. The backend authenticates the submitting host through PlayFab. With the default Photon connection mode, participant user IDs are client-supplied, not independently verified by Photon against PlayFab; custom authentication is an explicit deployment option. An authenticated host can fabricate gameplay, and Photon room properties are not a competitive anti-cheat boundary. These records must be labeled **AuthenticatedPlayerHost**, not server-verified gameplay. Trusted simulation/recording is a separate infrastructure change.

If the host crashes before a completion is uploaded, guests retain their result receipts, but an increased score remains pending until that host's outbox is uploaded. A host that never returns, loses its local storage, or cannot authenticate may leave that completion unverifiable. Abrupt termination can also omit a final stopped-day summary; previously acknowledged completed-day scores remain secured. This implementation does not claim crash-proof finalization from an unavailable player host.

External validation must cover authentication rejection, missing handlers, wrong statistic aggregation, duplicates, out-of-order revisions, offline guests, partial backend failures, corrupt local outbox recovery, account switching, simultaneous devices, receipt-slot reuse, and service/data quotas for long runs. No currency, campaign cloud files or website UI are written by these handlers.
