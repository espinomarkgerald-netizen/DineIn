# Restaurant ranking reporting — local implementation handoff

Status: implemented and compiled locally on `codex/restaurant-leaderboards`; **not deployed and not Unity/live-PlayFab verified**. No website files or scene/prefab assets were changed. Existing uncommitted work was retained. The configured title is `1021C5`; its published CloudScript revision, statistics and client API policy were not inspected through authenticated administration.

## Six all-time boards

These are Classic player statistics, requiring **Max aggregation and no automatic reset**. Website code should read the statistic, not a latest-run summary or campaign `currentDay`.

| Restaurant | Single-player statistic | Multiplayer statistic |
| --- | --- | --- |
| Casual Dining | `CasualDiningSoloLongestRunV1` | `CasualDiningLongestRunV2` |
| Fast Food | `FastFoodSoloLongestRunV1` | `FastFoodLongestRunV2` |
| Fine Dining | `FineDiningSoloLongestRunV1` | `FineDiningLongestRunV2` |

Existing multiplayer statistic names, scores, result rules, host/guest eligibility and backend handler source remain unchanged. Source/reference tracing confirms each authored multiplayer gameplay binding reaches the shared day results, `CompleteDay`, result outbox and publish/claim handlers. This is source evidence, not observation of deployed handlers or a running room.

Classic leaderboard read example for the website agent (authentication belongs to its own implementation):

```json
{"StatisticName":"FineDiningSoloLongestRunV1","StartPosition":0,"MaxResultsCount":20}
```

Use Classic `Client/GetLeaderboard` or the website's existing trusted server equivalent. Do not assume any new statistic currently exists or contains real data. No website implementation or sample-data replacement is included here.

## Score meaning and continuity

Multiplayer keeps its existing completed-day credit: fully settled days include a terminal failed shift; unfinished days do not count. Reconnection/provisional credit, frozen roster, departures and host loss follow the existing rules.

Solo counts distinct logical days settled during a newly ranked run, including a terminal failed day. It does not count attempts. With two credited days:

| Event | Score |
| --- | --- |
| Complete Day 3 normally | 3 |
| Finish Day 3 with a terminal failed outcome instead | 3 |
| Abandon unfinished Day 3 | 2 |
| Retry an already-counted Day 3 | Remains 3 |
| Paid continue from failed Day 3 to Day 4 | Remains 3; becomes 4 only when Day 4 settles |
| Retry failed Day 30 repeatedly | That logical day counts once |
| Advance into a genuinely new endless day after Day 30 | Adds one when settled, even though the HUD remains Day 30 |
| Load a save/report or reconnect | Adds nothing |
| Open an existing career without ranking metadata | Playable but unranked until an explicit new run |

The explicit clean post-tutorial career creation also initializes zero ranking credit; tutorial completion itself never scores. Only explicit run reset or genuinely fresh career creation initializes a ranked identity for an established account. Guest careers stay unranked. Known account-owned careers can earn offline credit without an active session. Paid continues keep the identity; service restart and same-day retry keep the ordinal. Existing next-day transitions advance the reporting-only ordinal, without changing gameplay day numbers, finance, approval, balance, progression or multiplayer protocol.

## Optional save block and failure behavior

Campaign schema stays **3**. The sole new root field is optional `soloRankingV1`:

```json
{"soloRankingV1":{"runId":"11111111111111111111111111111111","dayOrdinal":4,"lastCreditedDayOrdinal":3,"completedDays":3}}
```

- `runId`: persistent ranked-run identity; replaced on an explicit new run.
- `dayOrdinal`: reporting-only logical day, advanced by the real next-day transition rather than service attempts.
- `lastCreditedDayOrdinal`: duplicate and checkpoint replay prevention.
- `completedDays`: actual settled-credit count, independent of displayed day or an uncompleted ordinal.

The existing account-owned career container supplies ownership; the upload freezes that account separately. Reporting metadata contains no GC, credentials or replacement gameplay state. Tutorial and multiplayer runtime state cannot produce solo credit.

Settlement updates the block only after existing day settlement. Upload enqueue follows a successful existing results/save write. Loading a persisted count can reconstruct its pending submission; it does not infer a newly completed day. Same-account, same-run checkpoint recovery preserves settled high-water credit while restoring the replayed ordinal. Different runs are not merged. Missing/invalid reporting state disables ranking instead of resetting gameplay.

The existing cloud account envelope carries career JSON strings. No account-envelope version, legacy checksum serializer or hashing algorithm changed. The normal existing snapshot hash includes the new field as part of that career string.

The inspected pre-change build accepts schema 3 and ignores the unknown block on read. Its typed save writer drops the block on its next save. Returning to this build with no ranking metadata leaves that career unranked until a new run; existing server personal bests remain intact. **Lossless ranked continuity through an older writer is not guaranteed.** Historical releases other than the inspected schema are not certified.

Solo and multiplayer share the existing persistent reporter and authentication-context handling, but keep separate journal files:

- Existing multiplayer: `multiplayer_results_v2.json`, multiplayer entries only.
- Solo: `solo_results_v1.json`, account/restaurant/run-bound pending absolute scores.

This prevents an older multiplayer reader from rejecting solo entries and discarding its own queue. Solo entries coalesce to the highest pending count per account/restaurant/run. File replacement uses the existing atomic-write helper and backup; corrupt originals are preserved. Disk failures retain in-memory entries and retry storage; committed career metadata can recover a lost pending upload. Logout/account changes do not rebind receipts. Responses from another account/session, mismatched receipts, or older in-flight counts cannot erase a newer pending result.

Reporting exceptions, local journal failures, SDK exceptions and upload failures cannot block day transitions or turn a successful campaign write into a failure. Existing campaign-save failure behavior is unchanged. No client can promise durability if all local writes fail before process termination. Solo acknowledgements do not award multiplayer successful-day experience.

## Submission functions and examples

All client submissions use an authenticated PlayFab context and the **Live Classic CloudScript revision**. No direct client statistic write is added.

### Solo: `DineInPublishSoloRunV1`

Implemented by `Backend/PlayFab/SoloRunsV1.js`:

```json
{
  "record": {
    "schema": 1,
    "restaurant": "FineDining",
    "sceneName": "Lobby3",
    "rulesVersion": "finedining-solo-1",
    "runId": "11111111111111111111111111111111",
    "completedDays": 3,
    "gameVersion": "<Application.version>"
  }
}
```

Mappings: `CasualDining / Lobby1 / casual-solo-1`, `FastFood / Lobby2 / fastfood-solo-1`, `FineDining / Lobby3 / finedining-solo-1`.

Successful reply:

```json
{"accepted":true,"runId":"11111111111111111111111111111111","restaurant":"FineDining","completedDays":3,"bestCompletedDays":12}
```

Rejected requests return `{"accepted":false,"message":"..."}`. Execution/API errors and failed/mismatched replies stay pending. Scores are integers from 1 through 1,000,000. The handler writes only `currentPlayerId`; requests cannot choose another account or include a multiplayer participant roster. Absolute Max writes make duplicate/shorter submissions harmless, including retries after a write succeeded but its response was lost. No per-upload increment or payment/receipt service is introduced.

Solo remains **authenticated client-reported gameplay**, not trusted simulation or competitive anti-cheat. An authenticated modified client can fabricate its own score. Multiplayer retains its existing **AuthenticatedPlayerHost** trust boundary and roster validation; the solo endpoint does not weaken those handlers.

### Multiplayer: existing `DineInPublishRunV2` / `DineInClaimRunV2`

Both retain the same payload; the host publishes as `AA`, and a participating guest claims as `BB`. Example:

```json
{
  "record": {
    "schema": 2,
    "revision": 5,
    "runId": "22222222222222222222222222222222",
    "restaurant": "FineDining",
    "sceneName": "Lobby3 Multiplayer",
    "rulesVersion": "finedining-session-1",
    "gameVersion": "<Application.version>",
    "hostAccountId": "AA",
    "hostActor": 1,
    "partySize": 2,
    "completedDay": 3,
    "highestDay": 4,
    "startedUtc": "2026-11-01T12:00:00Z",
    "endedUtc": null,
    "endReason": null,
    "participants": [
      {"accountId":"AA","actor":1,"completedDay":3,"highestDay":4,"successfulDays":[1,2,3]},
      {"accountId":"BB","actor":2,"completedDay":3,"highestDay":4,"successfulDays":[1,2,3]}
    ]
  },
  "final": false
}
```

Other mappings remain `CasualDining / Lobby1 Multiplayer / casual-session-2` and `FastFood / Lobby2 Multiplayer / fastfood-session-2`. Replies retain `accepted`, `bestCompletedDay`, optional `successfulDays`, and rejection `message`. Multiplayer's singular `completedDay` and solo's plural `completedDays` are different wire fields; website readers use the board statistic instead of translating these payloads.

## Deployment and required build changes — not performed

1. From a trusted administrative environment, export title `1021C5`'s published Classic CloudScript revision, statistic definitions and client API policy. Compare the existing multiplayer handlers against repository source; do not assume they match.
2. Verify the existing three multiplayer statistics use Max / no automatic reset. Preserve existing versions and scores. Stop deployment for review if existing configuration differs; do not reset or replace data automatically.
3. Create the three solo statistics with Max / no automatic reset. Keep direct `Client/UpdatePlayerStatistics` disabled. No administrator secret belongs in Unity or source control.
4. Merge `SoloRunsV1.js` into a reviewed Classic revision while preserving all existing handlers. Publish only in a separately authorized deployment phase. No new service is required.
5. Ship a new game build containing the three new C# sources and metadata assets plus the four edited integration files listed below. Application version tuning, player settings and protocol were not changed. Existing builds do not report solo scores.
6. Run the external Unity/PlayFab acceptance matrix before calling the integration operational. Unsupported/missing live handlers leave uploads pending; there is no fallback to direct statistic writes.

## Verification actually run

| Check | Result and boundary |
| --- | --- |
| Runtime C# compilation using installed Unity references | Passed; not a Unity editor import or player build |
| Existing `MultiplayerRunsV2.tests.cjs` | 16 mocked checks passed; original handler/test sources unchanged |
| New six-board backend suite | 12 mocked scenarios passed, including Fine Dining host/guest scoring and partial failures |
| Source-linked C# harness | 19 scenarios / 115 assertions passed; real ranking state, flow adapter and uploader sources, with Unity/SDK test doubles |
| Persistence/reference suite | 6 checks passed; optional field shape, simulated old writer, cloud-string/hash compatibility, real hook positions, and all three scene bindings |
| Targeted diff/source preservation checks | Passed; existing multiplayer server source and pre-existing unrelated work retained |

Headless coverage includes all three solo adapters, actual duplicate settlement, checkpoint replay, genuine endless advancement past 30, paid next-day continuation, same-day retry, no credit on load, new-run identity, legacy/guest exclusions, account switch/stale tickets, offline restart, rejected/mismatched replies, failed local writes, post-write recovery, corrupt journal backups, synchronous SDK exceptions, and the unchanged multiplayer host/guest dispatch beside solo uploads.

The **old-build round trip is a source-derived serializer simulation**, using the captured 62-field pre-change schema. It establishes the expected unknown-field loss and preservation of represented gameplay values; it does not execute Unity JsonUtility or an old player binary. The cloud test preserves raw career strings and hash inputs locally; it is not live Entity File synchronization.

Not run: desktop control, Play Mode, automated playtesting, device/player builds, actual old-build JsonUtility round trip, live cloud/account switching, live statistics configuration, published handler comparison, or live PlayFab submissions. No services were published and no real player data or transactions were used. Remaining acceptance: in each restaurant, test a newly ranked solo run and a host/guest room, save/restart/checkpoint, failed day/continue, solo Day 30 retry versus endless next day, offline completion/reconnect, account switch, and real upload failures on authorized test accounts. Verify actual old-build read/write and updated-build recovery separately.

Reproduce local checks from the Unity project root:

```text
node Backend/PlayFab/MultiplayerRunsV2.tests.cjs
node Backend/PlayFab/RunReporting.tests.cjs
node Backend/Tests/run-headless.cjs
node Backend/Tests/RunReportingPersistence.tests.cjs
```

The headless runner needs .NET 9 SDK/runtime, uses no NuGet packages/network, and writes artifacts under the workspace's `.codex-checks/RunReportingHarness`. A normal SDK project is also supplied. The initial SDK restore attempt was blocked by access to the user's NuGet configuration; the direct compiler runner avoids that dependency and passed.

## Exact task file report

Modified existing files (4):

- `Assets/_Project/Core/GameFlowManager.cs`
- `Assets/_Project/Networking/Multiplayer/MultiplayerRunRecords.cs`
- `Assets/_Project/Save/GameSaveData.cs`
- `Assets/_Project/Save/GameSaveManager.cs`

Added files (14):

- `Assets/_Project/Core/GameFlowManager.Ranking.cs`
- `Assets/_Project/Core/GameFlowManager.Ranking.cs.meta`
- `Assets/_Project/Networking/Multiplayer/MultiplayerRunRecords.Solo.cs`
- `Assets/_Project/Networking/Multiplayer/MultiplayerRunRecords.Solo.cs.meta`
- `Assets/_Project/Networking/Multiplayer/SoloRunRecord.cs`
- `Assets/_Project/Networking/Multiplayer/SoloRunRecord.cs.meta`
- `Backend/PlayFab/SoloRunsV1.js`
- `Backend/PlayFab/RunReporting.tests.cjs`
- `Backend/Tests/RunReportingHarness.cs`
- `Backend/Tests/RunReportingHarness.csproj`
- `Backend/Tests/run-headless.cjs`
- `Backend/Tests/RunReportingPersistence.tests.cjs`
- `Backend/Tests/Fixtures/PreSoloGameSaveFields.json`
- `Documentation/Gameplay/LeaderboardReportingContract.md`

All other dirty files predate this reporting task and are not included in this report. No commit, push or live publication was performed.
