# Dine In visual and audio polish

Updated: 2026-11-01. Style: cozy, restrained. Scope: all three restaurants, Campaign, Tutorial and Multiplayer (including guests). This document reports repository implementation and static checks, not live gameplay acceptance.

## Completed implementation

- [x] Shared GameplaySfx asset retains the existing SFX mixer, master mute and original/local clips. Added editable per-cue levels and retained the repetition interval. Music is unchanged.
- [x] Serving and payment cues moved out of GameDayManager statistics counters. Actual accepted meal/takeout delivery, additional Fine Dining drinks/refills, cash/card settlement and special tutorial handoffs produce feedback even when the service-stat clock is not running.
- [x] Surface cleaning plays on dirty-to-clean completion. Tray washing uses the existing ReportHygieneWashed disposal hook (waiter, busser, trolley and multiplayer sink paths), guarded against repeat playback. Day reset and observed booth cleanup do not produce completion sounds.
- [x] Existing MultiplayerServiceActions transport extended with uncached event 178. Authority alone emits; host and guests consume the same run/day/action-ID deduplication path. Host identity, payload size, cue range, current run/day and timestamp are checked. Guests ignore recovery, unready and stale messages. No state snapshot contains feedback and receipts never re-execute gameplay just to play sounds.
- [x] Runtime button coverage reuses InGameButtonAnimationInstaller. It skips existing UISubtlePressFeedback, Animator and authored UIButtonEffects targets. Dynamic tutorial and purchase controls request the same installer. No extra global UI scanner was introduced.
- [x] ButtonAnimator, UIButtonEffects and UISubtlePressFeedback respect reduced motion and original transforms. Confirmed actions briefly emphasize an existing progress/money indicator. Purchase controls emphasize the persisted unlocked state. Error text remains visible; exact-change failure and rejected multiplayer actions explicitly request a soft attention cue.
- [x] WarningSlideUI.Show(message) is now informational/quiet. Show(message, true) opts into attention. Typed notification success/warning/error cues remain local; Info remains quiet.
- [x] AnimatedPanel closing keeps its raycast shield until inactive, restores transforms on interruption, uses restrained scale motion and substitutes short fades for reduced motion. Restaurant mode/purchase UI now uses this existing component without changing layout.
- [x] Existing queued unlock and Big Boss 100% presentations receive a soft milestone cue. Existing scene/modal/coaching/results gates and acknowledgement/seen flags remain their owners. Fast Food and Fine Dining multiplayer unlock presentation are included alongside Casual Dining.
- [x] Restaurant purchase milestone feedback is downstream of successful ownership persistence; completed-order reconciliation does not celebrate again. No economy, receipt validation or payment behavior was changed.
- [x] Day results reveal existing settled text briefly and use subtle star motion. Reduced motion shows final text/stars immediately. Restored reports and joining/recovering observers show immediate results without celebration; observed live service can present a live result. Hiding results cancels presentation and restores text/star state.
- [x] Installed $dine-in-polish with project reference and invocation metadata at C:/Users/User/.codex/skills/dine-in-polish.

## Coverage matrix

Wired = source/scene integration inspected; it does not mean playback or visual behavior was observed.

| Restaurant / mode | Serving & payment | Surface clean & wash | UI / notices | Results / milestones |
| --- | --- | --- | --- | --- |
| Casual Dining Campaign | Wired | Wired | Wired | Wired; existing campaign milestone rules |
| Fast Food Campaign | Wired, including takeout | Wired | Wired | Wired; existing campaign milestone rules |
| Fine Dining Campaign | Wired, including additional drinks | Wired | Wired | Wired; existing campaign milestone rules |
| Casual Dining Tutorial | Wired, including special cash handoff | Wired when the lesson completes those actions | Wired | Existing lesson presentation retained |
| Fast Food Tutorial | Wired through accepted restaurant actions | Wired when used by lesson | Wired | Existing lesson presentation retained |
| Fine Dining Tutorial | Wired through accepted meal/drink actions | Wired when used by lesson | Wired | Existing lesson presentation retained |
| Casual Dining Multiplayer | Authority emits; host/ready guests receive | Same transient route | Local UI notices | Live results; no personal 100% milestone |
| Fast Food Multiplayer | Authority emits; host/ready guests receive | Same transient route | Local UI notices | Live results; room unlock presentation |
| Fine Dining Multiplayer | Authority emits; host/ready guests receive | Same transient route | Local UI notices | Live results; room unlock presentation |

Events older than two seconds and messages received while unready/recovering are intentionally silent. Very close cues of the same type may be throttled by the editable audio interval. This is presentation loss tolerance, not a gameplay or progression change. Other-player/AI successful actions are audible to the room; local error notices are not broadcast.

## Compatibility and deployment

Room protocol is shared-session-23 so older clients cannot join rooms that promise this feedback behavior. Use matching updated clients for all multiplayer participants. This pass adds no backend service or deployment. Previously documented restaurant purchase catalog/CloudScript dependencies and conservative payment recovery limitations remain unchanged.

No save fields, GC prices/balances, shared approval logic, day success rules, customer outcome validation, movement/AI timing or backend code were changed by this polish pass. Two pre-existing TMP font asset changes were left untouched.

## Changed integration points

All subsystem paths below are under Assets/_Project unless stated otherwise.

- Audio/GameplaySfx.cs and Resources/Audio/GameplaySfx.asset (plus original WAV assets and metadata from the preceding audio addition).
- Customers/Customer/CustomerGroup.cs and CustomerGroup.FineDiningDrinks.cs; Customers/Booth/Booth.cs; Restaurant/Items/FoodTrayInteractable.cs; Lobby/Roles/CashierRegisterUI.cs.
- Networking/Multiplayer/MultiplayerServiceActions.cs, new MultiplayerServiceActions.Feedback.cs (+ meta), and MultiplayerSessionManager.cs (room protocol).
- UI/UISubtlePressFeedback.cs; MainMenu/NewDesign/Animations/Button Animation/ButtonAnimator1/ButtonAnimator.cs; UIButtonEffects/UIButtonEffects.cs; MainMenu/NewDesign/Animations/Panel Animations/AnimatedPanel.cs.
- MainMenu/GameMenu/Scripts/UI/GameModePopupController.cs and RestaurantAccess partial; WalletManager/PlayFabWalletManager.Restaurants.cs (presentation after persistence only).
- Gameplay/GameManager/GameManager.cs and WarningSlideUI.cs; NotificationPopupController.cs; Gameplay/UnlockCelebrationManager.cs and UnlockCelebrationUI.cs; Gameplay/RestaurantBossTips.cs.
- Tutorials/TutorialDialogueUI.cs, TutorialWaiterCashHandoff.cs and TutorialWaiterRegisterGuard.cs.
- C:/Users/User/.codex/skills/dine-in-polish/SKILL.md, references/project.md, agents/openai.yaml; this document.

## Verification actually performed

- C# compilation using installed Roslyn, existing Unity reference response file and current sources: passed, with existing warnings. Output/log: G:/Unity Projects/DineIn/.codex-checks/GamePolish.dll and GamePolish.log. This is not a Unity Editor build.
- Static scene/audio/skill reference checks: 23 passed. All nine scenes reference GameDayManager; MultiplayerServiceActions is ensured by the existing runtime bridge (it need not be serialized directly in every scene). Audio GUIDs resolve and the three original PCM WAV headers are valid. Log: .codex-checks/GamePolish.references.txt in the workspace root.
- Installed skill frontmatter, reference link, UI invocation prompt and unfinished-scaffold checks: passed using Node. Bundled Python quick_validate.py was not run because Python is unavailable on this host.
- Targeted diff whitespace checks: passed. No runtime test suite was run.
- Visual verification: NOT RUN. Listening verification: NOT RUN. No desktop control, Play Mode, automated playtesting, screenshots, heavy builds, live payments, backend deployment or Git mutations performed.

## Manual acceptance (pending)

- [ ] In each restaurant, complete one serve, payment, table clean and sink wash. Include Fast Food takeout and Fine Dining drinks. Reject/cancel an action and confirm there is no success cue.
- [ ] In each tutorial, complete the same applicable actions with and without its service clock running; informational lesson notices should remain quiet.
- [ ] With a host and two guests, perform actions as each role and through an AI worker. Check one cue per live completion on ready clients, no extra host cue and no late-join/reconnect replay. Test next-day transition/reset silence.
- [ ] Check SFX volume at zero/half/full, master mute, multiple close actions, dialogue readability, and relative music volume. Tune clip levels in the existing asset rather than gameplay code.
- [ ] Check touch/keyboard press feedback, narrow phone layout, panel opening/closing/reopening, and no click-through during fade-out. Confirm original scale returns after interruption.
- [ ] Check reduced motion for ordinary buttons, custom buttons, panels, result text/stars and unlock notices.
- [ ] Check unlock and Big Boss milestone priority behind coaching/results/modal UI. Acknowledge, restart/reconnect, and confirm no replay. Purchase presentation requires a confirmed ownership save; do not use a live payment just for a visual check.
- [ ] Finish a successful day and a failed day, reopen a saved report, and interrupt results by advancing/closing. Results must remain readable and real settlement numbers must remain unchanged.

Runtime acceptance remains pending. Do not call the feature visually or audibly verified until these checks are observed.


## Supplied SFX integration — 2026-11-02

Implementation complete in source; compilation/static checks passed. Visual verification and listening verification are NOT RUN. This section supersedes the earlier flat 2D audio defaults. No desktop control, Play Mode, automated playtesting, service publication, Git mutation, gameplay/save/GC/ranking change, or music replacement was performed.

### Imported assets and mapping

All 12 MP3s were copied byte-for-byte from G:/Downloads/Dine In sfx/ into Assets/_Project/Audio/Supplied/. Originals were left unchanged. The ambience file is approximately 20 minutes (48 MB); its importer uses streaming, no preload, and background loading. Short effects preload; the positioned serving tone imports mono. No audio trimming or loudness normalization was claimed. Assignments are based on supplied names and inspected durations, not an audition.

| Clip | Owner/use |
| --- | --- |
| Click | Accepted button onClick, pointer activation, keyboard/controller submit; one shared sound component, frame deduplication |
| Panel Open Effect | AnimatedPanel begins a new opening, not repeated Open calls on an already-open panel |
| Info | Typed informational popups and normal Big Boss coaching introductions |
| Notification | A consumed delivery-ready notice |
| Neutral Ting! | Confirmed serving, including takeout and Fine Dining drink delivery |
| Success | Typed positive UI outcomes |
| Excellent | Existing unlock/100%-approval milestones and successful purchase presentation |
| End Day Resuts | New ordinary/successful results only, under the existing restore/recovery guard |
| Blocked | Attention warnings and rejected/unreachable actions |
| Declined | Typed error popups; pending payment and cancellation paths were not reclassified |
| Bad Ting! | Confirmed incorrect cash entry; general error cue |
| 20-Minute Loop Of Restaurant Noise | Quiet restaurant ambience in the nine supported restaurant/mode contexts |

Original payment, table-cleaning, tray-washing, cooking, paper/page and wine minigame effects remain where appropriate. Their one-shots now use the shared voice pool/pitch policy. Existing cooking loops retain their owner and change pitch only on restart or a detected loop boundary. No new hover sounds were added. Legacy custom button click fields are retained as hidden compatibility data; shared GameplaySfx.click owns activation sound.

### Playback/configuration

- Main tuning remains Assets/_Project/Resources/Audio/GameplaySfx.asset. Defaults: global .45; serve .94, payment 1, clean .81 (about 25% stronger than their earlier near-camera gains); clicks .6, panels .4, info .45, notification .6, success .8, Excellent .9, results .85. These are numerical starting gains, not perceived-loudness measurements.
- Eight reusable short-effect voices do not steal/retune active voices. UI is centered and camera-independent. World effects use the active scene camera, viewport position, and MainCameraController.NormalizedZoom. Authored distance keys are center 1, screen edge .55, farther off-screen .1, then 0; zoom-out multiplier .6; pan limited to +/-.35. Missing cameras, hidden storage context, invalid positions and unloaded scenes cannot produce full-volume world cues.
- Reused/pooled sources route through the existing SFX mixer, SFX slider, master volume and mute. Music sources/settings were not edited.
- Short pitch range .985–1.015; consecutive repeats differ slightly. Ambience range .995–1.005, sampled only for a new loop. Randomness uses a separate System.Random policy, never the gameplay random stream.
- The existing SFX root owns two streamed ambience sources with 1-second scene fades and a 3-second loop crossfade. Ambience fades out in menus/storage and suspends during pause, tutorial time freeze and reconnect recovery. Confirmed tutorial action cues do not require a running service clock. World cues suspend for actual pause/recovery.
- Truck horn gain is .15, applied once rather than to both source.volume and PlayOneShot scale. The authored outdoor pose, arrival timing, collection state, horn clip and two-beep interval were preserved. Each beep gets its own positioned voice.
- Optional hasPosition/position fields extend transient feedback event 178. Existing cue IDs 0–4, world-only wire IDs 0–2, host checks, run/day/age checks, bounded deduplication and uncached delivery remain. Each client applies its own camera mix; legacy messages without position remain 2D. No room protocol or authoritative state changed.
- Saved report/reconnect restoration does not replay results. The real-day results cue uses the existing per-presentation pending flag rather than a displayed-Day-30 milestone key. Big Boss acknowledgement/seen flags and purchase ownership persistence were not changed.

### Coverage record

The shared action hooks and enabled scene contexts were checked statically. Each cell below is source/reference coverage only, NOT observed playback.

| Restaurant | Campaign | Tutorial | Multiplayer host/guest |
| --- | --- | --- | --- |
| Casual Dining | Shared serve/payment/clean/wash + UI/results | Shared applicable actions + UI | Shared accepted-action transport + optional position |
| Fast Food | Same, including takeout and existing cooking cues | Same, including clock-independent confirmations | Same, including shared takeout/counter outcomes |
| Fine Dining | Same, including drink/refill and wine UI | Same applicable interactions + UI | Same accepted food/drink/payment/clean feedback |

### Verification run and remaining acceptance

- Runtime C# assembly compiled with installed Unity references: PASS. This is not a Unity editor import/player build.
- node Backend/Tests/run-sfx-checks.cjs: 54 source/asset/reference checks PASS and 20,114 source-linked C# pitch/view-math assertions PASS. Checks include original byte equality, all twelve assignments, streaming metadata, nine enabled contexts, near/edge/off-screen/zoom/pan math, finite positions, repeated pitch bounds, UI/voice-pool source invariants, mixer settings and authority/replay safeguards.
- Targeted diff/whitespace checks: PASS. The ranking implementation and its backend files were not modified by this pass.
- Voice-pool timing, streamed decoding/crossfade behavior, actual event order, audible balance, real host/guest panning and source lifecycle remain source-checked rather than runtime-tested.
- Listening: NOT RUN; the interface cannot audition local audio. Visual/phone verification: NOT RUN. Actual Unity/Android import/playback: NOT RUN. No claim of listening-verified clip suitability or final loudness balance.

Manual checks (pending):

- [ ] Listen to each supplied clip and adjust assignments/levels if its actual timbre does not match the named purpose.
- [ ] In each of the nine contexts, complete serving/payment/table cleaning/washing; include Fast Food takeout/cooking and Fine Dining drinks. Confirm rejected/cancelled/unfinished actions emit no success cue.
- [ ] Pan and zoom over near, edge and off-screen actions; UI remains centered. Check on phone speakers and headphones, including comfortable pan width.
- [ ] Verify the truck remains outdoors and is noticeably quieter than ordinary action confirmations; delivery notification remains understandable.
- [ ] Test zero/half/full SFX, master mute, pause, time-frozen tutorial action steps, storage visits, loading and reconnect.
- [ ] Test pointer, touch, keyboard/controller submit, buttons that disable/close themselves, and rapid clicks for duplicate/stuck sound hooks.
- [ ] Listen through an ambience loop boundary, crossfade and pitch change; ensure no duplicate beds after scene changes and that music/voice remain clear.
- [ ] With host and guests, verify one live positioned cue, local camera-dependent mixing, and no snapshot/late-join/reconnect replay.
- [ ] Check real new results (including another endless Day 30), restored results silence, failed-results restraint, and one-time Big Boss/unlock/purchase celebration.

### Source changes for this audio pass

Presentation code: GameplaySfx, GameplaySfxPlayback (new), GameplaySfxPolicy (new), GameplayButtonSound (new); existing button installer/UIButtonEffects/UISubtlePressFeedback/AnimatedPanel; typed notifications/warnings, normal coaching and results; existing cash-error feedback; shared multiplayer presentation transport/rejected-action feedback; truck/restock notices; existing cooking/wine/paper/almanac/cleanable/purchase presentation owners. Configuration: GameplaySfx.asset and RestockDeliveryTruck.prefab horn gain. New audio assets: 12 MP3s plus importer metadata in Audio/Supplied. Tests: Backend/Tests/SfxPolicyHarness.cs and run-sfx-checks.cjs. No restaurant scene files or gameplay/economy/ranking/backend handlers were changed.

## 2026-11-02 — Pouring, UI families, storage and tips

Implemented (source inspection; no listening or Play Mode):
- Imported `Pouring Water Into Glass.mp3` unchanged. Water-service audio runs only while liquid flows; release, completion, Back, disable, pause and reconnect interruption stop it. Dedicated 2D source uses the existing SFX mixer. Wine retains existing completion feedback; it does not reuse the water recording.
- Created nine original mono PCM clips: three low menu thocks, three crisp computer clicks, three softer HUD taps. Shared configuration exposes all families; selection avoids the immediately previous variant. Existing 0.985–1.015 per-clip pitch variation remains, without changing voices already playing. Computer desktop/window ancestry and explicit desktop installation cover authored and dynamic buttons. Rebinding preserves audio after UI listener replacement.
- Disabled interactable-button attempts use supplied Blocked.mp3, with existing rate limiting and per-frame deduplication. Inactive controls and raycast-covered UI stay quiet. Panel/info/notification/success/milestone/result cues continue to use the supplied MP3 collection; the old universal Click.mp3 is only a fallback for an empty family.
- Successful stock-room placement gets an editable soft thock and a brief steady green shelf tint. Repositioning stored boxes also confirms placement. Failed drops and initialization/restoration remain silent. Multiplayer feedback is local to the placing player after an accepted authority receipt; duplicates are suppressed by the existing request completion path. No new network messages. A steady tint replaces movement/pulsing for everyone, including reduced motion.
- Created an original short coin chink for positive VIP dine-in/takeout and Fine Dining pour tips at their actual award paths. Appended Tip cue 14; existing cue IDs remain unchanged. Existing uncached authority feedback carries it to guests with the normal run/day/age/dedup checks; older clients safely ignore the unfamiliar presentation cue. No balance/snapshot hooks, economy or save changes.

### Sound-shopping list (targeted source audit, not an exhaustive listening audit)

| Interaction | Current coverage | Suggested dedicated recording |
| --- | --- | --- |
| Taking/reviewing/submitting an order | Generic button feedback; no dedicated sound in OrderFlowManager/ReviewedOrderSubmission/OrderChecklistUI/OrderTicketUI | Pencil scribble, ticket tear |
| Carrying/picking up a ready food tray | No dedicated pickup in FoodTrayInteractable; wash completion already has Clean | Light tray/crockery clink |
| Trolley loading/movement | No dedicated audio in BotTrolleyCarrier | Restrained tray placement; optional very quiet wheel roll |
| Reception interaction/seating | No dedicated audio in FineDiningReception owner | Short paper/clipboard handling; soft chair movement |
| Wine actively pouring | Existing completion sound, no active pour recording | Wine pouring into glass |
| Delivery crate impact | Soft synthesized thock now assigned | Optional real cardboard/wood crate placement to replace placeholder |

Serving, payments, table cleaning, washing, cooking, paper/page handling, truck horns, UI notifications, results and milestones already have feedback. Do not buy replacement clips solely on the assumption these are silent. The above list is based on inspected action owners; runtime audibility remains unverified.

### Verification and manual acceptance

- Runtime C# compilation against installed Unity references; source/asset/reference and PCM validity checks; pure pitch/view policy harness. Exact outcomes reported in the completion message. These are not a Unity player build or runtime acceptance.
- Manual: compare menu/computer/HUD families, click all computer tabs/cards/close/confirm and dynamically created hire controls, attempt disabled controls, test mouse/touch/submit without double playback, verify SFX/master mute.
- Manual: hold/release water pouring, cancel/Back, pause/reconnect while flowing, complete a refill. Confirm no lingering liquid sound.
- Manual: stock a crate, reposition it, reject an occupied-cell drop, restore stock room, repeat as multiplayer host/guest. Only accepted live placement should sound/highlight for the placing player.
- Manual: earn VIP and pour tips, finish/reload/reconnect; coin sound only on live positive award. Check volume on phone/headphones.
- NOT RUN: Play Mode, desktop control, listening, visual/device QA, live multiplayer, deployments or Git mutations. Existing unrelated font/prefab/skybox changes preserved.

Changed implementation files for this pass:
- Assets/_Project/Audio/{GameplaySfx.cs,GameplaySfxPlayback.cs,GameplayButtonSound.cs}
- Assets/_Project/Audio/Supplied/{menu-1.wav,menu-2.wav,menu-3.wav,computer-1.wav,computer-2.wav,computer-3.wav,hud-1.wav,hud-2.wav,hud-3.wav,Tip Coins.wav,Pouring Water Into Glass.mp3} and corresponding .meta files
- Assets/_Project/Resources/Audio/GameplaySfx.asset
- Assets/_Project/Restaurant/Items/WinePouringUI.cs
- Assets/_Project/Restaurant/RestockRoom/{RestockRoomController.cs,Prefabs/ShelvesScript/ShelfGrid.cs,Prefabs/BoxScripts/DraggableStorageBox.cs}
- Assets/_Project/Customers/Customer/{CustomerGroup.cs,CustomerGroup.FineDiningDrinks.cs}
- Assets/_Project/Networking/Multiplayer/MultiplayerServiceActions.Feedback.cs
- Assets/_Project/ManagementComputer/{ManagementComputerController.cs,ManagementEmployeeCardUI.cs}
- Backend/Tests/run-sfx-checks.cjs
- Documentation/Gameplay/GamePolishPlan.md

## 2026-11-02 — Delivery, world selection, payments and table conversations

Implemented using the existing SFX configuration/playback (no new manager):
- Original short Delivery Handling, Hotbar Arrival, World Selection and Payment Register WAV assets. All use the existing SFX mixer/master mute. Delivery handling repeats with a new slight pitch each iteration; hotbar/payment/selection use the existing short-voice pool and pitch policy. Original supplied files are unchanged.
- Delivery hold starts handling only while actively held, stops on release/exit/disable/completion/pause/recovery, and never announces success itself. One batch arrival cue follows confirmed local collection or an accepted multiplayer receipt; ordinary hotbar rebuilds and snapshots do not call it.
- Click-resolved actionable 3D targets get one local selection tap after tutorial/UI targeting and initial path acceptance. Empty-ground movement is silent. Stock-room shelf taps have their own local raycast-only feedback in the existing controller, respecting UI capture, camera/room and multiplayer layer filters. No input/gameplay rules changed. Later authority/path rejection retains existing error presentation; selection is not a success notification.
- Manual and autonomous cash/card settlement reuse their actual `amountEarned`, after earnings credit. New payment sound is distinct from Tip Coins. `TipPopupUI.ShowPayment` displays `+₱1,250`; existing customer popup prefab/follow anchoring is reused. Tips retain their own presentation. Reduced motion uses stationary fades. Tutorial handoffs without credited earnings remain sound-only rather than inventing a payment amount.
- Existing transient feedback has optional paymentAmount/customerView fields. Only authority publishes; normal run/day/host/age/action checks and uncached delivery remain. Popup consumption occurs after deduplication. Missing amount (older sender) stays sound-only; older receivers ignore extra fields. There is no save/snapshot/balance event hook.
- Imported `Aliens talking.mp3` byte-for-byte. Two dedicated local conversation voices select visible occupied tables with at least two actually seated active members; takeout excluded. Position/pan is based on the assigned table, not party spawn position. Current eligible speakers remain selected; free voices choose nearest screen-center tables. Starts stagger by 0.3 seconds, with editable 2-second gaps and tiny per-recording pitch variation.
- Verified camera normalized zoom: 0 is close, 1 is far. Conversation enters at <=0.35, exits at >=0.45; 0.6-second gain fades and total cap 0.12 before global gain are editable. Modal/storage/menu transitions fade it out; pause/recovery stops it; scene loads clear sources. Invalid/missing camera and fewer than two seated customers stay quiet. Restoring occupancy can resume local ambient conversation, not replay action feedback.
- Existing streamed ambience retained and reduced to 0.025 before global gain (0.01125 with current global 0.45). Music unchanged. Perceived loudness is not listening-verified.

### Exact files changed in this pass

- Assets/_Project/Audio/{GameplaySfx.cs,GameplaySfxPlayback.cs,GameplaySfxPolicy.cs}
- Assets/_Project/Audio/Supplied/{Delivery Handling.wav,Hotbar Arrival.wav,World Selection.wav,Payment Register.wav,Aliens talking.mp3} and corresponding .meta files
- Assets/_Project/Resources/Audio/GameplaySfx.asset
- Assets/_Project/Restaurant/RestockRoom/{RestockHoldButton.cs,RestockFlowHUD.cs,RestockFlowCoordinator.cs,RestockRoomController.cs}
- Assets/_Project/Player/Movement/PlayerMovement.cs
- Assets/_Project/Lobby/Roles/CashierRegisterUI.cs
- Assets/_Project/Customers/Customer/CustomerGroup.cs
- Assets/_Project/UI/TipPopupUI.cs
- Assets/_Project/Networking/Multiplayer/MultiplayerServiceActions.Feedback.cs
- Backend/Tests/{SfxPolicyHarness.cs,run-sfx-checks.cjs}
- Documentation/Gameplay/GamePolishPlan.md

### Future sound-shopping checklist — do not implement automatically

| Interaction | Current coverage / next sound |
| --- | --- |
| Order writing/submission/tickets | Generic UI feedback; find pencil scribble and ticket tear |
| Food tray pickup/placement/dirty dishes | Serving/washing covered; find light tray and crockery clinks |
| Trolley loading/unloading/movement | Find tray placement and very quiet wheel roll |
| Customer seating/arrival/departure | Local group chatter added; find chair movement and restrained greeting/departure cues |
| Active wine pouring | Completion covered; find wine/glass pour recording |
| Storage doors/crate pickup/discard | Placement + delivery handling covered; find door, cardboard/wood pickup and disposal recordings |
| Expiry warnings | Generic warning available; audit notification owner before adding a distinct cue |
| Register keys/drawer/card terminal | Settlement now covered; find physical key taps, drawer and terminal beep for their actual stages |
| Equipment install/repair/staff assignment | Generic confirmation available; audit owners before adding placement/tools/assignment sounds |
| Rush-hour/low-stock/closing notices | Existing informational messages are often quiet; choose selective attention cues after auditing, avoid sounding every notice |

### Verification / remaining manual checks

Source/asset/reference checks and source-linked pure pitch/view/conversation gating tests run through `node Backend/Tests/run-sfx-checks.cjs`; runtime C# compilation against installed Unity references and whitespace diff checks also run. These do not simulate live Unity audio, streaming or networking.

Manual coverage required in Casual Dining/Fast Food/Fine Dining, Campaign/Tutorial/Multiplayer:
- Hold/release/exit/close collection, interrupt pause/recovery, reject collection; one hotbar batch cue only after success. Restore/reconnect must not replay it.
- Tap booths/cashier/computer/shelves versus UI/ground; reject/unreachable targets; check no duplicated click handlers.
- Settle cash/card/autonomous payments with different totals, zero/invalid totals, duplicate callbacks, paid groups and reconnect. Verify exact formatted credited amount, distinct tips, and host/guest one-time presentation.
- Zoom through thresholds with zero/one/two seated customers and several tables. Verify at most two retained speakers, quiet gaps, correct left/right position, fade-out on empty/offscreen tables and modal/storage/menu transitions. Missing camera and scene teardown must stay safe.
- Listen on phone/headphones; compare quiet ambience with music and chatter, check all SFX/master levels and mute, reduced motion and interruption cleanup.

NOT RUN: Play Mode, desktop control, listening, device/player builds, actual host/guest runtime playback. No website/backend deployment, saves/economy changes, or Git mutations. Previous uncommitted work preserved.

## 2026-11-02 — Stable computer views and close-first navigation

Implemented without changing artwork/layout, campaign save fields, authored lesson IDs or scene data:
- Desktop app icons remain visible but non-interactable while a window is open. A central OpenApp guard protects normal pointer, keyboard/controller and fallback dispatch. Same-app requests do nothing; attempts to switch get existing blocked feedback. The gate preserves original CanvasGroup state and leaves Button.interactable owned by tutorials/gameplay. Dashboard/checklist links use a separate deliberate close-then-open path.
- Routine data updates call RefreshActiveApp, never OpenApp. Shared notifications coalesce; closing clears pending refresh. Status changes update retained views. Actual opening alone owns window entrance/reset behavior.
- Staff retains role sections, employee/applicant cards by stable employee ID, portraits, current tab and independent rail/body positions. Hire/decline/fire/assignment updates reconcile cards and retain the minimum-applicant fix. Routine changes do not replay panel/card entrances. Updates defer until brief card feedback completes; reduced-motion synchronous completion clears action state correctly. Delayed staff mutations validate account-directory/run/day context.
- Menu retains selection/category and unsaved text, including while focused. Save/availability update bindings in place; requested prices do not replace the draft with an old network value. Restock retains categories/cart/review state, updates cart lines by stable ingredient ID and ignores stale order callbacks after view disable or account/run/day changes.
- Equipment sections/cards, dashboard/objective rows and Finance panel/history rows are retained. Purchases recompute ownership/affordability without reopening. Finance background updates preserve its tab and use immediate totals rather than replayed counting/entry effects. Existing reservation view already reconciles requests and preserves selection; controller refresh uses that existing method.
- Shared transient scroll geometry captures visible retained children and pixel offsets for all active nested ScrollRects; after synchronous layout, it anchors the nearest surviving captured item and clamps list boundaries. New deliberate tab/category navigation retains its existing start-position behavior.
- Casual Dining's management transitions use a transient close-guidance phase: existing dialogue explains X, then the actual X is highlighted, and normal lesson progression resumes after real closure. Existing lesson IDs/indices are not reordered. Early-closure recovery identifies X when a wrong app obstructs the expected app. Tutorial focus no longer forcibly reactivates existing computer modal ancestors after closure.
- Fast Food retains its explicit Restock-close lesson and invokes shared close guidance if another app obstructs its Restock desktop target. Fine Dining already explicitly teaches closing Reservations; its sequence and close action are retained. Office tutorial's task/close gates are retained.

Exact files changed for this pass (relative to the Unity project):
1. Assets/_Project/ManagementComputer/ManagementComputerController.cs
2. Assets/_Project/ManagementComputer/ManagementComputerController.StableView.cs (new)
3. Assets/_Project/ManagementComputer/ManagementComputerController.StableView.cs.meta (new)
4. Assets/_Project/ManagementComputer/ManagementComputerScrollAnchor.cs (new)
5. Assets/_Project/ManagementComputer/ManagementComputerScrollAnchor.cs.meta (new)
6. Assets/_Project/ManagementComputer/ManagementComputerHRPanel.cs
7. Assets/_Project/ManagementComputer/ManagementHRRoleSectionUI.cs
8. Assets/_Project/ManagementComputer/ManagementEmployeeCardUI.cs
9. Assets/_Project/ManagementComputer/ManagementComputerCatalogPanelUI.cs
10. Assets/_Project/ManagementComputer/ManagementFinancePanelUI.cs
11. Assets/_Project/Tutorials/TutorialSystem.cs
12. Assets/_Project/Tutorials/TutorialSceneBindings.cs
13. Assets/_Project/Tutorials/FastFoodTutorialBridge.cs
14. Backend/Tests/StableComputer.tests.cjs (new)
15. Documentation/Gameplay/GamePolishPlan.md

Checks actually run:
- C# runtime-source compilation with installed Unity references: PASS (not a Unity Editor/player build). Log: ../.codex-checks/StableComputer.log.
- `node Backend/Tests/StableComputer.tests.cjs`: PASS, 24 source integration checks plus 8 source-linked compiled navigation/scroll cases using Unity geometry doubles. Scenarios include wrong/same/new app, vertical/horizontal pixel anchors, removed anchor fallback, inactive views and independent nested axes. This does not exercise real Unity layout timing or touch input.
- `node Backend/Tests/ApplicantFloor.tests.cjs`: PASS, 33 source-extracted assertions plus four binding checks; existing recruitment repair remains covered.
- Whitespace diff checks: PASS. Pre-existing EmployeeManager/ApplicantFloor test changes and skybox edits preserved; they are excluded from the file list above.

Manual acceptance remains UNVERIFIED:
- In Casual/Fast Food/Fine Dining, tap left-side icons behind an app, use X/Back/Exit and deliberate dashboard/checklist links. Verify no accidental replacement or duplicate input/SFX.
- Near the bottom of every Staff tab/rail, hire/fire/decline/assign, including the last applicant; confirm stable position and new replacement availability. Repeat with reduced motion.
- Edit/submit a menu price, toggle availability, modify/review/order a cart, buy equipment/booths, and inspect Finance History. Verify stable nested scroll, selection, drafts, immediate totals and no repeated entrances.
- As multiplayer host and guest, test pending/rejected/duplicate requests, concurrent remote changes while typing, closing during pending requests, reconnect, and account/restaurant changes. Confirm late callbacks cannot alter a new view; verify real authoritative ownership/affordability stays correct.
- Complete each affected tutorial transition, including closing early, pause/resume, retry and restored lesson progress. Verify X guidance advances once, correct targets stay clickable, normal target changes may scroll intentionally, and no closed modal is resurrected.
- Rotate a phone near list boundaries, remove the first visible card, and shrink a list to zero. Verify clamping and pixel anchors under actual Unity layout/safe-area changes.

NOT RUN: desktop control, Play Mode, automated playtesting, visual/listening/device QA, actual multiplayer, full tutorial walkthroughs, deployment or Git mutations. Runtime verification is required before claiming the tutorial/mobile flows are fully verified.

### 2026-11-02 — Wine refill pouring sound

Wine refill now reuses the supplied Pouring Water Into Glass recording while the stream is actually flowing, alongside water service. Removed only the water-only audio gate in WinePouringUI; both modes share existing pitch/mixer settings and release/completion/Back/disable/pause/recovery cleanup. This supersedes the earlier note that active wine pouring remained silent. Updated the existing audio reference check. Files: WinePouringUI.cs, Backend/Tests/run-sfx-checks.cjs, this checklist. Source checks and runtime-source compilation run; listening and Play Mode remain unverified. Manual: start/stop a wine pour, finish it, press Back, pause and reopen; confirm no lingering audio.

## 2026-11-02 — Daily reward slot and developer preview

Implemented the simplified three-reel panel with actual existing food sprites and button art, animation/SFX, reduced-motion behavior and developer preview. Developer Mode > Gameplay > Daily reward preview, or `dailyreward()`, opens it without completing a day; TEST CASH / TEST GC / TEST NO MATCH are presentation-only. Preview has no save, wallet or attempt mutations. Default odds: 35% cash, 5% GC, 60% no prize.

The pre-results hook is present after successful campaign settlement and skips protected/restored/failed/multiplayer days. **Live rewards are not enabled:** the repository lacks trusted run/day eligibility, proven idempotent Classic GC credit and recoverable campaign-cash projection. No backend provider is registered and no grant is fabricated. With no provider, the normal results screen appears as before. Full contract, exact file list, deployment blockers, actual checks and manual acceptance are in `Documentation/Gameplay/DailyRewardSlot.md`.

Checks: C# runtime-source compilation passed; 114 source-linked odds assertions and 22 source/asset checks passed. No Play Mode, desktop, actual payout, device/listening/visual or deployed backend verification. Manual: preview in all campaign restaurants, forced states, pause, reduced motion, master/SFX mute, repeat/close and safe-area sizing; confirm preview never changes balances or attempts.

### 2026-11-02 — Compact slot visual correction

Added actual nine-sliced rounded rim art, flat blue buttons, a smaller safe-area-limited popup, separate developer strip, larger normalized food symbols, prize/currency banner, continuous eased reels and stop settling, win emphasis/counting and pooled star particles. Added quiet reel-loop audio through the existing mixer. Reduced motion uses immediate outcomes/fades with no particles or pulsing; closing fades retain input blocking. See DailyRewardSlot.md for exact files and verification limits. Live payouts remain disabled. Source compilation and odds/motion/reference checks passed; actual Unity/device visual, listening and animation timing remain manual.

### 2026-11-02 — Slot currency/result feedback

Replaced padded cash artwork with the game's tight money-stack sprite, added generated transparent GoldCoinGC.png, and grouped larger currency icons closely beside measured labels in the prize strip/result banner. Removed the yellow filled reel overlay in favor of explicit border bars. Added win currency bursts/icon bounce and a distinct quiet blue no-match dip/fade, with capped pools, reset cleanup and reduced-motion fades. Compilation, 39 source/asset checks, 1,121 compiled odds/motion assertions and whitespace checks passed. Actual Unity visual/listening verification remains manual; see DailyRewardSlot.md for exact files and checklist. No live payouts enabled.

## 2026-11-05 — Start Day and End Day panel skin

Applied the approved cyan rim, cream paper, navy header, coral/gold ribbons and green button sprites from DayPanelSpritePack to the ten enabled day-panel scenes: Lobby1/2/3, their multiplayer and tutorial variants, and the shared Multiplayer scene. Existing scene roots, text references, button events and result-data owners are retained. Shared sprite assets and the explicit, idempotent DayPanelSkinAuthoring command keep the scene skins consistent without replacing the scene hierarchies. The management-computer installer reapplies the skin after its legacy day-loop setup.

The intro now has separate HOURS, TARGET and PREP rows, the actual configured hours/revenue target, a cash icon, and the existing service capacity/rate/reminders. The older shared Multiplayer scene's intro/result aliases and missing result-column bindings were corrected. Results retain their settled revenue, orders, customers, improvement advice, approval, tips, reservation summary and outcome/recovery copy. The cloned paid Continue button uses the secondary blue sprite. Missing peso/dash/bullet characters in the static SDF fonts fall back to readable PHP/ASCII presentation without changing amounts or editing shared font assets.

GameDayManager remains the sole sequence owner, with presentation/layout helpers in GameManager.DayPanels.cs. Its editable defaults are a 0.28-second panel settle, 0.18-second card reveal with 0.06-second stagger, and 0.24-second earned-star pops. Only the inner surface scales; the outer dimmer shields input throughout entry/dismissal. The accepted Play dismissal stays inside the existing 0.2-second pre-service transition, with duplicate-start and interruption cleanup. Newspaper, restock, tutorial and multiplayer readiness gates remain ahead of service. The existing reward-before-results and EndDay-cue guards remain in place; restores/reconnects show final results immediately. Failed outcomes avoid celebratory star motion. Reduced motion skips the movement. Layout fits the safe area, preserves the intro rows' native proportions, and stacks report cards in portrait with extra room for recovery copy.

Checks performed: runtime and Editor source compilation using Unity 6000.0.40f1 references; scene ID/reference and hierarchy checks across all ten scenes; original serialized object identities retained; three intro/reveal bindings, result bindings and button raycast targets verified; authoring script repeatability checked. Compilation writes only to Temp/DayPanelsCompile, without touching the running Editor's Library assemblies.

**Unity Play Mode, actual rendered appearance, audio listening, phone/device and live multiplayer acceptance are NOT RUN.** The supplied images are concept mockups, not evidence of runtime appearance. Manual checks:

- Open/confirm the intro in all three restaurants; exercise newspaper, stock reminders and tutorial/readiness gates. Confirm one service start, including rapid taps and an interrupted close.
- Finish 0–3-star days; inspect the full standard report, Fine Dining reservation summary, campaign victory and failed-run recovery. Check free restart, GC cost/confirmation and host/guest action labels.
- Check portrait/landscape safe areas, Large Text, Reduced Motion, rotation during entry, repeated open/close and no click-through during dismissal. Check long recovery copy stays readable inside its card.
- Check master/SFX mute, a single ordinary live EndDay cue, reward modal ordering, and no celebration replay after saved-report load or reconnect. Conduct the planned player-feel review after rendered layout/listening checks.

## 2026-11-06 — Matching V2 artwork, typography and report widgets

Replaced the day-panel presentation across all ten scene variants with DayPanelSpritePackV2: 40 transparent component PNGs, a named 4096 × 2048 atlas, explicit slice borders/pivots and matching illustrated icons and plump stars. The panel body and title are separate, and values are editable TMP widgets over blank artwork. Dedicated Lilita One and Nunito Sans 700/800 SDF assets include the peso and required punctuation. Static Nunito instances correct the variable source's thin default weight without changing shared fonts.

GameDayManager remains in its original GameManager.cs file and owns sequence timing, audio and action conditions. The earlier partial-script arrangement is no longer used. DayPanelView receives settled snapshots, lays out individual metrics and resets cosmetic state; it does not change gameplay, settlement or progression. Intro hours, target and preparation stay on one line; actual group capacity, arrival rate and time conversion have icon chips. Finances, Service and Guests & Approval retain the detailed metrics, including Fine Dining reservations. Coaching and recovery/purchase notices have their own regions. Portrait, Large Text and longer outcome reports use a scrollable middle with fixed title/actions and an interactive scrollbar. Large numeric reveals now retain the exact final integer instead of rounding it through float precision.

Checks actually run:

- Runtime and Editor C# compilation against Unity 6000.0.40f1 references passed.
- PNG/atlas/manifest audit passed for 40 assets, including dimensions, alpha padding, slice borders and matched star pivots/bounds. Dedicated font currency/punctuation checks passed.
- Unity rendered 14 isolated production-asset cases: ordinary intro/report, zero results, all 0–3 star counts, large values, long hours/coaching, failure/victory, recovery prompts, portrait, portrait bottom scrolling and Large Text. TMP overflow and unintended atomic wrapping checks passed. Saved renders and the exact report are in Documentation/UIConcepts/DayPanelV2.
- Partial metric/card/star updates followed by immediate finalization passed. These exercise interrupted presentation cleanup and the immediate-final path; they do not simulate live coroutine timing or toggle the user's accessibility preferences.
- All ten saved scenes reopened and passed original-controller/action, dedicated-font, sprite/star and scrolling-reference checks. Existing action instances/listeners were retained. Bootstrap stayed open, clean and out of Play Mode.

Not run: live Play Mode service transitions, animation feel/audio listening, phone/player builds or multi-client/reconnect gameplay. Manually check a complete shift in each restaurant, rapid Play/Next taps, Fine Dining reservations, recovery actions, Reduced Motion, portrait drag scrolling and interruption/rotation. Compilation and isolated UI renders do not establish these live behaviors.

### 2026-11-06 — Corrected undersized panels in the real scene hierarchy

The live screenshot exposed an ancestor missing from the original isolated preview: DayIntro's legacy wrapper was fixed at 100 × 100 in all ten scenes. V2 normalized the background and panel but stopped before this wrapper, so its 1280-wide design fitted to only 95 canvas units (scale 0.07421875). The shared Multiplayer results wrapper had the same constraint.

DayPanelView now stretches the complete modal ancestor chain up to the nearest Canvas, then updates canvas geometry before measuring. The Canvas and its scaler remain untouched. Saved scenes now have full-size modal wrappers and corrected initial surface scales. The preview uses the real three-level nesting, including the originally constrained wrapper, and asserts the fitted size. Saved-scene validation also checks all ancestors.

Checks: runtime and Editor compilation passed; static sizing validation passed for all 20 surfaces across ten scenes; 14 isolated Unity renders passed the nested-wrapper fit, text and presentation-reset assertions. The landscape intro render was visually inspected. Reports and representative renders are in Documentation/UIConcepts/DayPanelV2/SizingFix. The user's running scene was not controlled or tested in Play Mode. Stop/restart Play Mode after importing to verify the full-size intro and results in Lobby3; live timing/audio and multiplayer remain unverified.

### 2026-11-06 — Centered compact headers and larger stars

DayPanelView now sizes the navy tab to its native sprite aspect, centered at 108 units high instead of 140. The prior wider RectTransform used a top-left pivot, so Unity's preserve-aspect drawing aligned the visible tab left of the centered text. The new matching aspect fixes that shift. Lilita title text uses geometric midline alignment within the visible pill body. The three star cells are square at 112 units instead of the former 76-unit visible size, with a centered row and repositioned sparkle accents. Cards, actions, data and animation ownership are unchanged. This shared runtime layout applies to all scene variants without rebuilding their artwork or hierarchies.

Checks: runtime and Editor compilation passed. All 14 isolated Unity cases passed the existing fit, overflow, wrapping and presentation-reset checks. Landscape intro/report, long failure title and portrait Large Text renders were visually inspected. Representative renders and the check report are in Documentation/UIConcepts/DayPanelV2/HeaderStars. Live Play Mode animation feel/audio, device and multiplayer behavior were not run. Refresh/import and restart Play Mode to inspect the updated header and earned-star pops in the game.

### 2026-11-06 — Report card finishing details from the existing sprite pack

DayPanelView now adds missing decorative artwork once when an existing authored panel is first laid out. It keeps the existing widgets and bindings. Reused RowShell, ValuePill, ProgressFill, AccentRays, coins and the chef/cloche outline sprites for cream row wells, target-state badges, reservations heading/well, cyan status flanks, rating accents and footer doodles. Thin tan UI dividers separate reservation columns, guest moods, approval/grade and finance sections. Guest labels now sit between the face icons and their counts. Extra rating rays are omitted in portrait to avoid duplicating the corner accents. Corrected the mirrored accents' anchors so they stay outside the stars. All added artwork ignores raycasts and sits behind metric text/icons.

All ten saved scenes now include the three additional existing pack sprite references, appended after the original 37 slots. No source artwork was replaced. Existing states still choose a success check for perfect cash, a warning for errors, a neutral target-gap badge when short of the target, and the reservation or non-reservation content as appropriate. Editor validation explicitly permits the intentional solid-color divider Images.

Checks: runtime and Editor compilation passed; all ten scenes' 40 ordered sprite references matched their metadata, with the first 37 slots unchanged. All 14 isolated Unity render cases passed fit, overflow, wrapping and interrupted/immediate-final presentation checks. Final landscape, portrait Large Text and zero-result images were inspected after softening row wells and correcting accent overlap. Representative renders and reports are in Documentation/UIConcepts/DayPanelV2/Details. Live Play Mode, animation/audio listening, device and multi-client testing were not run. Refresh/import and restart Play Mode to verify the details and original actions in the loaded game.
