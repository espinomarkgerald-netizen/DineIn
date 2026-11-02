# Daily reward slot: implementation and live-service dependency

The simplified daily-reward UI is implemented as an editable composition of existing game UI/button artwork and the exact existing food sprites. No generated preview screenshot is baked into the screen. The developer preview is operational in source; it does not award currency, write saves, consume an attempt, or call a server.

## How to test

Open Developer Mode, select the Gameplay page, then **Daily reward preview** (the row is added beside the existing card-payment preview). The command console also accepts `dailyreward()` through the normal developer authorization boundary. SPIN uses the configured random distribution. TEST CASH, TEST GC and TEST NO MATCH select a repeatable presentation case; press SPIN after selecting one. Close/Continue returns to the game. This can be opened without completing a day. No account roles or developer authorization rules were loosened.

## Implemented presentation

- Three masked animated food reels, staggered stops, quiet existing SFX with per-play pitch variation, warm win outlines, success/no-match state and Continue/Skip.
- Campaign-only pre-results hook runs after day settlement and only for successful, non-restored live days. Failed days, tutorials, multiplayer and save application are excluded. No provider is registered, so this hook currently proceeds directly to the existing results screen.
- Config: `Assets/_Project/Resources/DailyRewardSlot.asset`. Symbols, typeface, button art, amounts, weights, reel timing and sound levels are editable. Reduced motion uses immediate reel stops and fades. Opening uses existing SFX/feedback helpers rather than a replacement audio manager.
- Default outcomes: burger +100 cash (15%), fries +250 cash (10%), roasted chicken +500 cash (7%), chicken Alfredo +1,000 cash (3%), fried chicken +10 GC (4%), rib-eye steak +50 GC (1%), no prize (60%). Each winning result has three matching icons. Non-winning results are unequal, with no forced near-miss behavior.
- Developer previews are marked clearly. Live responses are context-checked and consumed at most once per open panel. Pending live resolution allows Continue after eight seconds and never starts another spin.

## NOT operational: live reward eligibility and payouts

**This is not a fully functional live daily-reward economy.** There is no authoritative daily-reward service in the repository or verified deployment. The client deliberately registers no backend and performs no currency writes. Do not enable a client random result, wallet debug command, client GC add, or ad hoc CloudScript grant as a substitute.

`IDailyRewardBackend` is an explicit future integration contract, not a receipt/grant implementation:
- `GetEligibleOffer(account, restaurant, rulesVersion, completed)`: return a server-issued eligible offer identifier, or no offer. The server must derive/validate the authenticated owner, trust its own completion ledger, and validate the rule version; the client account string is only correlation context.
- `ResolveSpin(offerId, completed)`: authoritatively select/persist one outcome and finish its award. Repeated requests for the same offer must resolve the same outcome without another award. A confirmed response means all required authoritative grants and recoverable persistence have completed, including campaign cash projection. A timeout is unknown, not failed.
- `Skip(offerId)`: forfeits the unspun offer; must be duplicate-safe. Skipping a pending/spun offer does not discard its award.
- `DailyRewardResolution`: confirmed flag, prizeIndex (-1 for no match; otherwise index in versioned configuration), exact amount, gold flag and message. This is an integration response, not a locally minted or server-validated receipt.

Minimum external capabilities required before real rewards can be enabled:
1. A trusted, account-bound campaign run and logical-day ledger. It must distinguish successful days from failures/retries, preserve identity across save/resume, checkpoints, cloud/account changes and actual endless-day advancement, and exclude debug completions. Display day/currentDay and optional ranking metadata are not proof of eligibility. The current game/backend does not supply this authoritative ledger.
2. A proven idempotent Classic GC credit capability with recovery for unknown responses. A client transaction ID or read-before-write grant handler alone is not sufficient. Current repository CloudScript does not provide that capability. Classic GC and PlayFabWalletManager remain the currency system; Economy V2 is not substituted.
3. A recoverable cash award projection into the correct restaurant/campaign save that cannot be duplicated or erased by checkpoint/old-cloud restoration. No cash is written by this UI until such a provider exists; campaign save format is unchanged by this pass.
4. An authenticated deployed adapter implementing the interface, with concurrent/multi-device claim tests, failure-injection tests around grant/persistence, exact rule configuration, and server-side audit logging. No backend handler or service was published here. Register the provider only after these guarantees are implemented and verified.

These are concrete blockers, not assumed guarantees. Adding a handler that does a Classic GC grant followed by writing a claimed flag would leave a crash/concurrency window and must not be represented as safe.

## Verification

- Runtime C# compilation against installed Unity references: PASS, `../.codex-checks/DailyReward.log` (not a Unity player build).
- `node Backend/Tests/DailyRewardSlot.tests.cjs`: PASS, 114 compiled source-linked odds assertions and 22 source/asset checks. Includes exact 35/5/60 distribution, bounded editable weights, real icon GUID resolution, developer gating, preview isolation, result-hook order, duplicate response and pending exit guards. No real grants or networking were tested.
- Diff whitespace checks: PASS.
- NOT RUN: Play Mode, desktop control, phone layout, listening/visual QA, real multiplayer or PlayFab, full results/tutorial walkthroughs. No Git mutation or deployment.

Manual: launch the developer preview in each campaign restaurant, random-spin and force all three states; test audio volume/mute, reduced motion, Back/Continue, repeated previews and pause interruptions. Confirm balances and saves do not change. Inspect safe-area/portrait sizing on device. The model and integration checks do not establish actual Unity animation timing or backend reward safety.

## Exact files changed in this pass

- `Assets/_Project/Gameplay/Rewards.meta`
- `Assets/_Project/Gameplay/Rewards/DailyRewardSlotSettings.cs` and `.meta`
- `Assets/_Project/Gameplay/Rewards/IDailyRewardBackend.cs` and `.meta`
- `Assets/_Project/Gameplay/Rewards/DailyRewardSlotUI.cs` and `.meta`
- `Assets/_Project/Resources/DailyRewardSlot.asset` and `.meta`
- `Assets/_Project/Debug/DevSettingsConsole.cs`
- `Assets/_Project/Debug/DeveloperSettingsView.cs`
- `Assets/_Project/Gameplay/GameManager/GameManager.cs`
- `Backend/Tests/DailyRewardSlot.tests.cjs`
- `Documentation/Gameplay/DailyRewardSlot.md`
- `Documentation/Gameplay/GamePolishPlan.md`

All prior uncommitted computer, applicant, audio and skybox changes were preserved.

## 2026-11-02 — Compact matte presentation correction

The earlier flat placeholder has been replaced with a generated transparent rounded-rim sprite and layered matte fills. The generated center is deliberately not rendered (`Image.fillCenter = false`); a separate opaque pale fill keeps the interior clean. The frame is imported with nine-slice borders. Existing flat blue button art replaces the glossy button reference; no global button/font asset is modified.

The 800 × 630 authored panel now fits within editable maximum safe-area fractions (0.68 width, 0.78 height for the complete presentation). Developer controls live in a separate smaller strip. Safe-area/resolution changes recompute a uniform fit, preserving artwork aspect ratios. Existing food sprites have editable per-symbol scale and offset, and the prize banner uses a cash icon or GC badge.

Reels use prebuilt symbol sequences and continuous index/offset wrapping, with acceleration, cruise, deceleration, staggered stops and small settling motion. Outcomes are chosen before presentation; no forced near misses or currency logic changes. A small original quiet reel-loop WAV uses the existing SFX mixer through GameplaySfx.SetUiLoop; it stops on pause, completion, closing and teardown. Stop/tick one-shots use the existing pitch-variable playback pool.

Confirmed wins have a brief banner/symbol emphasis, prize amount counting, warm animated rims and a capped pool of up to 24 UI star particles (default 16). Particles never capture input or allocate per frame. Non-winning outcomes use the quiet status fade. Reduced motion suppresses movement, settling, pulses and particles, showing immediate reel outcomes and short fades. Replay resets retained effects; popup closing keeps the gameplay blocker until the closing fade completes. The UI remains assembled by its existing component with art/layout/animation tuning in DailyRewardSlot.asset; no authored prefab or Unity screenshot is claimed.

Changed files for this correction:
- Assets/_Project/Gameplay/Rewards/DailyRewardSlotUI.cs
- Assets/_Project/Gameplay/Rewards/DailyRewardSlotUI.Presentation.cs and .meta (new)
- Assets/_Project/Gameplay/Rewards/DailyRewardSlotMotion.cs and .meta (new)
- Assets/_Project/Gameplay/Rewards/DailyRewardSlotSettings.cs
- Assets/_Project/Gameplay/Rewards/Art.meta (new)
- Assets/_Project/Gameplay/Rewards/Art/RewardPanel.png and .meta (new)
- Assets/_Project/Gameplay/Rewards/Art/ReelLoop.wav and .meta (new)
- Assets/_Project/Resources/DailyRewardSlot.asset
- Assets/_Project/Audio/GameplaySfx.cs
- Backend/Tests/DailyRewardSlot.tests.cjs
- Documentation/Gameplay/DailyRewardSlot.md
- Documentation/Gameplay/GamePolishPlan.md

Verification: runtime-source C# compilation against installed Unity references passed; source/asset checks and compiled odds/motion tests passed; whitespace checks passed. Motion checks cover monotonic travel, exact endpoints, settling return and compact fit for landscape, narrow landscape and portrait dimensions. These are mathematical/source checks, not actual Unity layout/animation playback. The frame sprite was visually inspected as generated artwork; the assembled in-game screen and audio have NOT been observed.

Manual acceptance: Developer Mode > Gameplay > Daily reward preview. Compare its size with the visible restaurant behind it, test random/cash/GC/no-match outcomes, inspect symbol wrap/stop/settle, particle cleanup, closing input block, reduced motion, mute/pause and phone rotation. Live reward payouts remain disabled pending the documented backend capability. No desktop control, Play Mode, Git mutation or deployment occurred.

## 2026-11-02 — Currency grouping and distinct outcome effects

- Replaced the padded HUD money sprite in this popup with the existing tightly cropped `MainMenu/NewDesign/ShopItems/Data/Currency/money.png`. Winner currency now uses an editable 62-unit icon and measured text width with an 8-unit gap; icon and final amount form a centered group. The prize strip uses the same tight icon/text grouping.
- Generated/imported `Gameplay/Rewards/Art/GoldCoinGC.png` with actual transparent alpha, matte gold/navy outlines and GC lettering. It replaces the placeholder GC badge in both the result banner and prize strip; original game assets are untouched.
- Fixed the screenshot's yellow food overlay: outcome highlights are now four explicit edge bars controlled by a CanvasGroup. They cannot paint a solid fill across a food symbol.
- Wins now have a green/gold-tinted result banner, larger currency icon bounce/tilt, amount counting, a secondary pooled cash/GC particle burst and one quiet coin cue. No additional award/currency calls were added.
- No-match outcomes use a brief cool-blue edge pulse, a gentle 4-unit symbol dip, and five soft descending particles that fade away. Original symbol positions restore at completion. Reduced motion uses short fades with no movement, pulses or particles. Pools remain capped and reset on repeat previews.
- Exposed coin sprite, icon size/gap, loss duration/dip and coin cue in the existing settings asset. Default win presentation duration is now 1.05 seconds; loss is 0.55 seconds.

Exact files for this pass: DailyRewardSlotUI.Presentation.cs, DailyRewardSlotSettings.cs, Resources/DailyRewardSlot.asset, Rewards/Art/GoldCoinGC.png and .meta, Backend/Tests/DailyRewardSlot.tests.cjs, Documentation/Gameplay/DailyRewardSlot.md, Documentation/Gameplay/GamePolishPlan.md (paths otherwise relative to Assets/_Project).

Verification: C# runtime-source compilation PASS; 39 source/asset checks and 1,121 compiled source-linked odds/motion assertions PASS; whitespace diff checks PASS. The generated coin and existing money sprites were inspected directly. In-game icon spacing, actual win/no-match animations and perceived audio balance were NOT observed. Manual: Developer Mode > Gameplay > Daily reward preview; test cash, GC, no-match and repeats, confirm food remains untinted, icons sit beside their amounts, and reduced motion/cleanup/mute work. No desktop control, Play Mode, Git mutation or deployment. Live payout status remains unchanged (disabled pending backend).
