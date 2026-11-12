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

### 2026-11-07 — Action sprites, outlined labels and held-food Serve guidance

Implemented the approved action-button plan using ActionButtonSpritePackV1 and RestaurantControlsSpritePackV1. GameplayActionButtonSkin retains each existing Button and its callbacks, using separate sliced frames, illustrated icons, editable labels, number badges and amount fields. GameplayActionButtons.asset contains 51 sprite references, fonts, dedicated outline materials and tuning values. Lilita headings and Nunito amounts have cream faces and visible black per-letter outlines; the existing font assets were left untouched by this work.

Connected Take order/drink order, Pour water/wine prompts, tray/takeout pickup, Ask/Give bill, Collect bill and cash/card payment controls through their existing controllers. The existing button installer handles kitchen, reservation, cleaning-choice and pouring Back controls. Whole clickable reservation cards retain their party details. Pouring remains a drag interaction; bottle/glass/liquid artwork and timing are unchanged. Kitchen status hints use their existing stage values. Pressed/disabled/focused sprites use native Button state handling. A short alpha transition owns no positioning/scale and emits no success sound. Existing button/confirmed-action SFX owners remain unchanged. Kitchen feedback now reuses the installed press animator instead of adding a competing one.

Table-bound delivery guidance turns into Serve food only for the active local waiter's successfully held matching tray/customer/order/booth. Multiplayer also requires existing local-carrier guidance. The action retains an attached number, receives existing Fine Dining focus priority, and uses the existing deferred movement API. Arrival revalidates eligibility before calling one delivery handler. Put-down, role/target/order changes and recovery remove eligibility; passive tray identifiers never become Serve actions. Serving owners now reject a different customer identity even when the numeric order matches. Expanded floating controls reuse the bounded world-bubble clearance pass, with authored scales accounted for.

Verification:
- Runtime and Editor source compilation against Unity 6000.0.40f1 passed. Existing unrelated compiler warnings remain.
- Isolated native Unity checks passed 324 assertions: sprite/font/material references and glyph coverage, explicit slice borders, held-food eligibility and stale-arrival guards with gameplay boundary doubles, native Button callback/state preservation, dynamic panel labels and reservation-card exclusion.
- Four production-asset native renders passed TMP overflow checks: landscape, Large Text, portrait and nine crowded neighboring controls. Crowded graphic rectangles passed pairwise overlap checks. Real OrderAlertUI, TrayUi, BillAlertUI and TableNumber prefab ancestry plus production UIFollowWorldPoint/layout sources were used. Corrected a Large Text amount-height failure and residual ColorTint during these checks. Final rendered labels/outlines were visually inspected.
- Verification sources are in Tools/Verification/ActionButtons. Saved results and renders are in Documentation/UIConcepts/ActionButtons/ImplementationChecks. Boundary doubles do not exercise real pickup, billing, pathfinding, authority or network transport.

NOT RUN: live Play Mode flows in the user's scene, pointer/touch and keyboard timing, actual sound listening/mute, phone builds, host/guest/reconnect acceptance, all saved-scene variants and live kitchen/reservation/pouring layouts. No desktop automation, main-editor scene replacement, build/install, save migration or Git commit/push was performed.

Manual check: restart Play Mode after import; complete order -> pickup -> correct-table Serve -> full bill/payment -> cleaning in all three restaurants. Try a wrong tray, put-down/re-pickup, cancellation while walking and a departed/reused table. Check Fine Dining cash/card, reservations and water/wine; Fast Food cooking/takeout; portrait/Large Text/Reduced Motion; one click plus confirmed action cue, SFX mute and pouring loop cancellation. Multiplayer still needs host, acting guest, other guest and reconnect tests. UI correctness in these live paths is not established by compilation or isolated renders.
### 2026-11-07 — Larger gameplay controls, customer comment artwork, reservation exclusion

The earlier skin used a small world-space scale, while Fine Dining's independent fixed-width rule bypassed that scale. Floating action controls now use worldVisualScale 1.1 (previously 0.8, about 38% larger); Fine Dining uses an action-only 1.4 readability multiplier. Both remain editable in GameplayActionButtons.asset. Existing outlined labels/icons and native states are retained.

The previous shared installer had also opted the management reservation panel into the gameplay skin. The entire ManagementReservationPanel ancestry is now excluded. The skin branch was removed from ManagementReservationPanel.StyleButton; that file now has no diff from its original checked-in source. Reservation prefabs, layouts, labels, tabs, actions and data are not redesigned. Restart Play Mode to discard previously styled runtime instances.

Generated CustomerCommentSpritePackV1: nine transparent blank components (three sliced mood panels, three separate tails, three circular badges), a named 1024px atlas, Unity metadata and rectangle/border manifest. Existing DayPanelV2 illustrated happy/neutral/angry faces are reused. CustomerCommentBubble styles the existing CustomerComments prefab from both normal and observed/custom thought paths. It reuses the original message field and follow/reveal owner, preserves actual text, mood decisions, tip amounts, duration and outcome/audio code, and disables decorative raycasts. Nunito text stays readable on cream; long messages grow the frame vertically and large currency tokens receive enough width to remain intact. No second motion or audio owner was added.

Larger controls exposed a crowded-layout limit. Their existing clearance search now considers free space below an anchor, begins inside the HUD-safe rectangle, and considers occupied edges for variable-height comments. Extra below/edge candidates are scoped to controls opting into readable action/comment clearance. No new positioning manager was created.

Checks actually run:
- Runtime and Editor compilation against Unity 6000.0.40f1 passed.
- Isolated native Unity verification passed 493 assertions, including 63 sprite references, the nine imported atlas sprites and slice borders, complete reservation-panel exclusion, existing serving guards, native button state/callback preservation, text overflow, comment mood/long-text/currency coverage, atomic currency wrapping and nonblocking comment raycasts.
- Seven native render cases passed: four action layouts (landscape, Large Text, portrait, nine crowded controls) and three comment layouts (landscape, portrait Large Text, long-message/currency portrait). Pairwise graphic bounds did not overlap in the crowded action and comment fixtures. Final comment images, long tip text and larger action render were visually inspected. These fixtures use real prefab hierarchies and production follow/layout sources with gameplay boundaries doubled.
- ManagementReservationPanel.cs has no remaining diff. Relevant whitespace/diff checks passed.

Evidence: Documentation/UIConcepts/ActionButtons/ReadabilityAndComments. Art source/generator: Tools/Art/BuildCustomerComments.cs and .ps1. Isolated checks: Tools/Verification/ActionButtons/Run.ps1.

NOT RUN: the user's live Play Mode scene, actual customer timing/animation feel, sound listening, device input/safe areas, all scene variants or multi-client/reconnect gameplay. The open editor was not controlled or replaced. After import, stop/restart Play Mode, inspect normal/compact/Serve controls at several zooms, check positive/neutral/angry and hygiene/tip comments, and confirm reservations retain their original appearance. Existing live audio/network verification requirements still apply.
### 2026-11-07 — Target attachment, reception actions and patience bar

The live screenshot showed controls displaced from their tables/customers and trapped along the HUD-safe boundary. The cause was the action/comment skin explicitly enabling screen-space clearance. Those opt-ins now call the existing KeepAttachedToTarget API. The added below-target/neighbor-edge clearance candidates were removed; unrelated authored clearance retains its previous implementation. World controls follow their original anchor, preserve same-target stacking and hide when the target leaves the viewport. The screen-space follow path also hides offscreen attached targets instead of leaving an edge control. This replaces the earlier cross-target packing requirement; world attachment is the requested behavior.

CustomerGreetBubbleUI now applies the same outlined action skin to its existing Greet Customer and Seat Table states, using its original hand/table icons and original label reference. Styling runs only when the state changes. The existing click handler, reception claims, movement callbacks, table assignment, multiplayer routing and exact tutorial labels were left unchanged. The management reservation UI remains excluded.

Patience uses its existing Slider and green/yellow/red color owner. Its height is now 20 units (previously 8), positioned above the action with editable height/gap and space for an optional identifier badge. The existing neutral rounded track artwork is reused so the color tint remains accurate. Fill-area geometry now occupies the thicker track. The bar ignores pointer/keyboard interaction and decorative raycasts, and retains its real normalized value without adding hold mechanics or SFX. Layout refresh keeps it above a resized button.

Verification: runtime and Editor compilation passed. The isolated Unity suite passed 481 assertions, including target movement through the HUD zone, camera pan/zoom, offscreen hiding/input removal and return; existing serving guards; reservation exclusion; original caption/callback/state preservation; patience placement, thickness, value and tint retention. Eight native render cases include the actual reception prefab, action and comment layouts. Reception and the final above-button patience bar were visually inspected. Evidence is in Documentation/UIConcepts/ActionButtons/AttachedReception. The native fixture checks reception presentation; it does not execute the live greeting/seating handler.

NOT RUN: the user's live scene, mouse/touch, actual HUD raycast interaction, animation/audio listening, phone builds, host/guest/reconnect or complete reception-to-seating flows. The open editor was not controlled. After import, stop/restart Play Mode; pan tables behind the HUD and offscreen/back, greet a customer and select a seat, then check patience at green/yellow/red levels and with Large Text. Source compilation and isolated native checks do not establish those live paths.

## Compact customer drawer and task panel — 2026-11-07

Implemented the approved CustomerDrawerSpritePackV1 through the existing shared LobbyHUD branches. The customer drawer starts closed and retains the existing visibility/tutorial gates. CustomerDrawerView is a local component under LivePanel; CompactTaskPanelView extends the existing PlayerTaskHUD message branch. Main HUD counters and reservation management were not changed by this integration. Existing unrelated worktree changes were preserved.

The drawer caches customer rows in arrival order, scrolls list/details in the same compact frame, preserves list scroll on Back, and clears selection on departure, scene changes, modal hiding and recovery. Real CustomerTypeProfile portraits, actual group sizes, validated booth ownership, service stage, last recorded reaction and existing timer fractions supply the display. Untimed and unavailable/syncing data have explicit placeholders. Large Text grows scroll content; narrow layouts put the table badge on its own line. Find uses MainCameraController's bounded centering without changing zoom, restores temporary follow suspension, and restores the existing Outline after one restrained emphasis.

CustomerGroup.Inspection.cs is a read-only projection. FastFoodRestaurant.Network.cs exposes its existing seat/ready deadlines for inspection; it does not change them. Fine Dining reads the real request/service timers and existing claim/pour pause conditions. Multiplayer extends the existing revisioned service snapshot with only known/active/normalized patience fields, sampled no faster than 4 Hz and rounded to 1%. Existing authority/run/day/revision validation remains in place. Older payloads do not fabricate patience or reaction data.

World inspection gates precede commands in PlayerMovement, RoleBasedAssignController, HostAssignController, WaiterAssignController and CustomerGroupClickable; TapOutlineSelector shares the gate. A plain table-number bubble inspects while open; its explicit Serve action retains the delivery owner and all eligibility/authority checks. UI-origin presses, scrolling and camera gestures are excluded from world inspection. Tutorial-required world actions retain priority. No inspection method submits service, movement or claim requests.

Task guidance accepts optional icon, short action, destination and actual table fields. Producers provide these directly; no display-string parsing is introduced. Full action/detail text remains the fallback and long supporting content scrolls. The old additional mobile task scale is bypassed for the compact presentation. The popup docks beside the existing clipboard with an editable gap; when that cannot fit in a narrow safe area, it moves above it. The existing task owner recaptures docking on viewport changes and releases interrupted motion. Task disappearance/cancellation is no longer inferred to be success by the new view; confirmed multiplayer completion keeps its existing owner.

Feedback uses the existing LobbyHUDRedesign slide, PlayerTaskHUD animation owner, ButtonAnimator and GameplaySfx mixer. The drawer toggle owns one quiet configurable panel cue and suppresses its generic click. Rows/navigation use the existing button sound; world inspection uses the selection cue. Passive refresh, scrolling and snapshot restoration are silent. Reduced Motion skips task slide/bounce/badge pops, drawer movement and outline emphasis. Artwork input remains independent of decorative animation.

Editable artwork, fonts, dimensions, content padding, patience thickness, timing and panel cue/gain are in Assets/_Project/Resources/UI/CustomerDrawerArt.asset. Reference viewport sizes are 345x345 list, 345x380 details and 276x138 task in the existing 1920x1080 HUD coordinate system (approximately 300x300, 300x330 and 240x120 at the approved 1670x940 concept viewport).

Verification:
- Runtime and Editor C# compilation passed, using the production sources and existing Unity response-file references.
- 196 source/resource checks passed: 40 sprite bindings, fonts, script metadata, input guards, snapshot sampling/validation order, shared HUD branches, mobile scale and all nine existing restaurant scene variants.
- Isolated Unity production-assembly suite passed 477 assertions: actual snapshots/timers, observer unavailable data, ordinary service stages, stock timer, table reuse, cached rows/order, departure, Back scroll restoration, long labels, fixed task sizing, clipboard separation and native TMP layout checks. Desktop, portrait Large Text and 320-unit narrow layouts rendered with the production sprites/fonts/portrait. These are empty-scene fixtures, not the live restaurant. The fixture's precompiled-assembly API updater emitted an initial ugui-resolution diagnostic; subsequent native loading, compilation and all assertions succeeded.
- Existing isolated action-button suite passed 499 assertions, including production TableNumberUI serving eligibility/revalidation and unchanged normal delivery behavior with the drawer absent. Its gameplay boundaries are doubled.
- Native desktop/portrait/narrow images were visually inspected. Evidence and detailed limits are in Documentation/UIConcepts/CustomerDrawerV1/Integration. git diff --check passed.

NOT RUN: live mouse/touch/EventSystem dispatch, real camera follow interruption, all restaurant-specific stages in running scenes, tutorial step execution, live host/guest/late-join/reconnect transport, animation playback, phone/notch layouts or audio listening. No device build was made, and the open Unity editor/scene was not controlled.

Manual acceptance: after import, restart Play Mode. In each restaurant, open the drawer with zero/many groups; inspect a waiting group and occupied table without moving/claiming; verify explicit Greet/Seat/Serve/Pour/payment buttons and closed-drawer world clicks still work. Scroll, open details, Back, reassign/reuse a table and let the selected group leave. Check Find, pan afterward and close; camera follow must resume normally. Test long labels, Large Text, Reduced Motion and rapid open/close. During a required tutorial action, confirm its normal world targeting wins. On host/guest, late join and reconnect, check syncing placeholders and no restored feedback. Listen at normal/muted SFX levels for one cue per click and silence on passive refresh/scrolling; check phone safe areas and overlap with shortcut controls.

## Landscape newspaper and reception actions — 2026-11-07

Implemented the approved game-accurate newspaper through DailyNewspaperPresenter and a local NewspaperLandscapeView. The existing polish settings now hold all 27 original/matching icon references, seven scalable surface exports, paper size, text sizes and column spacing. Desktop uses three columns, with the longer approval story in Restaurant News; narrow layouts stack the same sections within one scroll region. Headers/footer remain fixed and the scrollbar indicates additional content. Five rounded rating slots use the original filled/empty/half sprites. Snapshot metrics use cloche, order, guest and reaction icons. Boss advice is highlighted; actual supplier changes, quotes and staff details remain in the saved article.

The presentation reads persisted renderedContent rather than regenerating an issue or adding a new save/network schema. Known saved headings are separated for layout; legacy unrecognized content remains readable through the article fallback. A recognizable complete snapshot gets icon/value rows; unfamiliar formats retain their original text. Edition day and source shift day are distinct. Existing archive order, viewed acknowledgement, host/guest newspaper transport and tutorial visibility rules remain authoritative. The original paper animation owner now uses a short 0.3-second opening and small rotation; Reduced Motion fades without scaling/rotation. Close is available during opening. Archive page-turn audio retains ownership with its redundant generic click suppressed. The existing HUD shortcut and management reservations screen were not redesigned.

Confirmed reception cause: FineDiningReservationIndicator had null caption/icon/Greet/Seat sprite fields, so CustomerGreetBubbleUI.Refresh returned before applying the new action skin. Its serialized caption and four presentation bindings are repaired, the root uses the cyan action shell, and the authoring code preserves those bindings on future regeneration. Runtime binding repair handles older variants. Approved waving-hand/table icons replace the old balloon presentation. The same original action handler, claims, approach/movement callbacks, multiplayer routing and tutorial actions remain in place. Reserved parties have a non-clickable tag below the action, sized from the real button scale and actual text height. Booking/replicated name, time and size are read through ReceptionPartyCaption; missing details read syncing/unavailable. Font size and minimum tag height are editable on the existing component.

Checks actually run: runtime and Editor C# compilation passed. Static prefab checks verified unique object IDs, the caption parent and non-null presentation bindings. The new icons retained their earlier 509 pixel/bounds/source-integrity checks; seven surfaces have explicit slice borders. Isolated Unity checks passed 554 assertions including the prior drawer coverage plus newspaper content preservation, parsed/legacy articles, desktop/portrait Large Text layout, exactly five rating slots, metric rows, unavailable archive navigation, Greet/Seat presentation, actual party caption, non-clickable tag and no presentation-driven seating/state change. Production PNGs, metadata and fonts were used. Native desktop/portrait newspaper renders were visually inspected. This fixture uses synthetic report values solely to exercise layout; the running game still reads its real saved issues. Evidence is under Documentation/UIConcepts/Newspaper/Implementation.

NOT RUN: live restaurant Play Mode, actual greeting/seating clicks or guest authority transactions, archive paging/viewed-save behavior in a real run, tutorial execution, phone/notch layouts, multiplayer host/guest/reconnect, animation playback and audio listening. The user's open editor and scene were not controlled. Main save data and Git history were not altered. The isolated precompiled-assembly importer can emit ugui resolution diagnostics before its package compilation; subsequent native loading/compilation and assertions passed.

Manual acceptance after scripts import: restart Play Mode. Greet a reserved party, confirm the pill changes to Seat Table and retains its party tag, select a valid table, and check ordinary walk-in actions. Open today's newspaper and old issues; verify actual day/source-day, market prices, rating and incidents, disabled archive ends, scrolling and long text. Check Large Text, narrow viewport, Reduced Motion, closing while opening, and SFX/master mute. Repeat the relevant actions on a multiplayer guest without changing claims or service outcomes.

## Cleaner newspaper reference implementation — 2026-11-07

Replaced the shared article scroller with three independent, masked column scrollers under stationary LAST SHIFT, NEXT SHIFT and MARKET WATCH headings. Desktop column weights are editable (29/30/41 percent); the wider right column shows item, cost per box and change in measured table cells, with supplier notes below each row. Long product names wrap within their item cell; exceptionally large values switch to a stacked row instead of overlapping. Stable prices and unfamiliar archived formats retain their saved text. Narrow screens stack bounded section viewports. The shared outer scroll component is disabled, so scrolling prices cannot shift results or editorial headings. Header, footer, issue selection and original action/audio owners remain in place.

Matched the approved composition with a larger masthead, roomier 1560x930 paper, original five rounded stars, vertical ink rules, paired mood icons and a compact coral incident strip. Full incident details are preserved in supplementary Service Notes below the primary editorial blocks. The original issue string remains unchanged; no sample products or prices were added to runtime code. The native comparison fixture uses the archived Day 8/Day 7 figures and Martini/Cream/Lemon entries supplied in the user's screenshot, solely for layout testing.

Found that the existing regular Atkinson font asset uses a legacy TextCore script binding and could silently fail a TMP-font reference, leaving heavier fallback text. Added a dedicated, correctly bound TMP copy of that existing regular atlas in the sprite pack, with the existing day-panel font as a currency/symbol fallback and the original OFL license. Original font assets were preserved. Copy remains regular weight; emphasis uses existing rich-text bold styling. Paper dimensions, typography and column weights are editable in the existing polish settings. Surface regeneration preserves those authoring values.

Verification: runtime and Editor compilation passed. The isolated production-assembly suite passed 754 assertions including independent scroll/heading positions, three supplier rows, formatted peso values, large prices/long labels, stable-price state, unrecognized market fallback, regular font loading and peso fallback, saved-content preservation and the existing drawer/reception cases. Desktop, portrait Large Text and long-value native renders were inspected. Evidence: Documentation/UIConcepts/Newspaper/CleanLayout. Native rendering is an empty-scene fixture, not an observed live restaurant session. git diff --check passed. Live pointer/touch, actual archive paging/viewed saves, host/guest/reconnect, phone safe areas, animation playback and audio listening remain unverified. The open editor/scene and unrelated worktree changes were preserved.

Manual acceptance: after import, restart Play Mode. Open the same archived issue, confirm all three headings start at the top, and scroll only Market Watch; the left and middle columns must remain stationary. Check supplier name/cost/change/notes, then test a longer report and old/stable-price issue. Check middle-column Service Notes and customer quote by scrolling that column. Repeat with Large Text, narrow viewport and on a multiplayer guest; verify archive navigation, close, mute and Reduced Motion retain their existing behavior.

## 2026-11-07 — Cross-restaurant action UI consistency repair

Cause: generic action styling included FastFoodCookingView descendants, including inactive controls whose skin Awake had not run. UseCaption then dereferenced an uninitialized caption. Kitchen startup called the button installer before hiding station/selection controls, so an exception could leave authored kitchen UI visible over the dining HUD. The old notice errors were downstream of incomplete initialization. Generic styling now excludes the entire kitchen hierarchy; its original saved artwork, labels and callbacks remain authoritative. Skin initialization is explicit and idempotent for inactive actions. Startup hides station/selection/help/notices before optional feedback setup; incomplete required kitchen references disable the kitchen canvas and report the existing authoring error. Notice input is restricted to an initialized, open kitchen.

Approved action artwork now opts into a shared explicit 1080p screen-width path in UIFollowWorldPoint instead of relying on the Fine Dining-only size correction or multiplied prefab/canvas/mobile factors. Editable defaults: 200-wide actions, maximum 280, minimum height 60, number bubbles 72, patience height 12. Labels use 24, comments use 300 width and 20-point supporting text with 100 minimum height. Long labels/details grow within the existing layout. Actual sprite transparency required a finer slice scale and additional inner padding; native render inspection caught this after TMP overflow checks passed. Comments wrap within their width, retain atomic currency values, follow their actual customer and remain nonblocking. Fine Dining compact behavior is retained. No reservation management styling was changed.

The Serve focus fix exempts only the existing strictly validated localDeliveryAction from the competing-task presentation disable rule. A tray claim is a different object from its target customer; it no longer dims a valid held-tray delivery control. TableNumberUI also refreshes focus/input when eligibility stays unchanged. All existing role/order/tray/customer/booth/tutorial/delivery-owner and multiplayer validation remains in place, including click/arrival revalidation. No claim, distance, hand or authority check was removed. Task docking watches settled clipboard bounds in addition to parent size and waits for the existing animation owner to settle, without adding another animator or cue owner. WarningSlideUI validates required references before revealing a banner, restores the real message label when shown, and hides interrupted notices on disable.

Changed runtime/config files: GameplayActionButtonSkin.cs, GameplayActionButtonArt.cs, GameplayActionButtons.asset, UIFollowWorldPoint.cs, CustomerCommentBubble.cs, PlayerTaskBubbleFocus.cs, TableNumberUI.cs, FastFoodCookingView.cs, PlayerTaskHUD.cs and WarningSlideUI.cs. Regression fixtures were extended in tools/Verification/ActionButtons and tools/Verification/CustomerDrawer. Existing unrelated work and the open Unity editor were preserved. No scene regeneration, save mutation, commit or push was performed.

Verification: runtime and Editor compilation passed (existing unused-field warnings only). All three Fast Food scene variants passed serialized kitchen reference checks. The isolated action suite passed 669 assertions, including inactive styling, kitchen exclusion, shared/Fine Dining sizing with multiple authored scales and camera zooms, number sizing, offscreen input suppression, delivery focus and existing eligibility/revalidation cases; gameplay boundaries in this suite are doubled. The isolated production-assembly drawer/layout suite passed 763 assertions, including task re-docking after clipboard movement/scale without viewport resize and stable unchanged docking. Native action/comment/portrait previews were visually inspected. Evidence: Documentation/UIConcepts/ActionButtons/ConsistencyRepair. The first restricted native runner launch exited before producing a fresh report; subsequent isolated runs completed, and only fresh passing outputs were retained as evidence.

Final preview check: the portrait gallery now uses four representative, separated sample anchors instead of crowding nine independent world targets into a narrow view. All action-gallery renders now assert pairwise control bounds as well as text overflow. The final action suite passed 758 assertions; the production-assembly layout suite passed 763. These sample layouts do not guarantee that arbitrarily close world targets cannot overlap in a live restaurant; target attachment remains intentional and live placement still needs checking.

NOT RUN: live restaurant serving, scene startup/entry/exit, desktop/touch dispatch, multiplayer host/guest/late join/reconnect, phone safe areas, interrupted live animations or audio listening. Static and isolated success does not establish live service acceptance. No new audio events were added.

Manual acceptance: restart Play Mode after import; enter Fast Food normally and confirm kitchen controls/notices are hidden with no Console exception. Enter each kitchen station, switch/back/exit, and confirm the original HUD and readable labels. In each restaurant, pick up a matching tray and verify Serve stays clickable, approaches the correct table, and actually delivers; check wrong tray/order, cancelled/replaced target and Fast Food self-pickup remain rejected. Pan/zoom and check buttons/comments stay attached and consistent; offscreen controls must hide. Open the task popup after scaling/viewport changes and check clipboard clearance. Repeat with Large Text/Reduced Motion and multiplayer host/guest; listen for a single existing action cue with mute respected.

## 2026-11-07 — Hotbar, newspaper, inspection identifiers and Find corrections

Implemented the four follow-up corrections through existing shared runtime and authoring owners. No scenes were replaced, no saves or network identities were changed, and the unfinished action-feedback/staff-delivery/reward work was preserved.

FastFoodCookingView.RemoveHotbarBackdrop previously cleared only the scroll root. It now also clears the actual viewport Image, preserving its raycast flag and existing mask/ScrollRect. The kitchen authoring palette also preserves transparency on Supplies scroll/Viewport instead of repainting it blue. Individual item graphics and drag targets remain owned by the kitchen. This shared runtime initialization applies to campaign, tutorial and multiplayer kitchen variants; live station switching and drag behavior remain unobserved.

NewspaperLandscapeView now sets the final width, wrapping, font size and an unconstrained measurement height before forcing TMP mesh generation; section and market-row heights use the maximum of generated bounds and preferred height. Subsequent blocks are positioned from these measured heights. Typography/font-atlas changes invalidate the cached layout in addition to size and Large Text, and the existing independent column scrolling is retained. The test suite now checks actual generated text heights, section containment and adjacent section bounds, including long market entries and an increased font-size layout refresh. Native desktop/long and portrait renders use production assets and were inspected; these are isolated fixtures, not live gameplay screenshots.

CustomerInspectionSnapshot and its existing task producers use CustomerGroup.InspectionTableLabel, matching currentOrderNumber shown by the floating bubble. Takeout, Not seated and Awaiting number remain explicit real states. Lobby interaction labels and cleaning task labels no longer derive a customer-facing number from the booth asset name. Order numbers and physical table/network identities are unchanged internally.

CustomerGroup.TryGetInspectionPosition frames the live member centroid for unseated groups, excludes inactive/destroyed members, and uses the actual assigned table once seated. Assigned groups still walking toward a table remain targeted at their live position. CustomerDrawerView disables Find with Position unavailable when there is no valid target, grows that button for wrapped Large Text, and preserves camera zoom, bounds, follow suspension restoration and existing highlight/Reduced Motion behavior.

Changed source: FastFoodCookingView.cs, FastFoodCookingView.Authoring.cs, NewspaperLandscapeView.cs, CustomerGroup.Inspection.cs, CustomerDrawerView.cs, LobbyHUDRedesign.cs, LobbyTaskTracker.cs. Extended tools/Verification/CustomerDrawer/UnityCheck.cs.txt. Evidence: Documentation/UIConcepts/ConsistencyFixes.

Verification: runtime and Editor compilation passed; git diff --check passed. The isolated production-assembly Unity suite passed 1,454 assertions including the new label, missing/live/inactive member, assigned-but-walking and seated-target cases, newspaper generated bounds/containment and existing drawer/reception regressions. Existing unused-field compiler warnings remain. No live gameplay scene, pointer dispatch, hotbar dragging/scrolling, device safe-area behavior, host/guest/reconnect transport, animation playback or audio listening was tested. No compilation or isolated result establishes those live paths as accepted.

Manual acceptance: restart Play Mode after import, enter/switch kitchen stations and confirm only the outer blue hotbar surface is gone while item slots, dragging and scrolling work. Open the reported newspaper issues, scroll each column independently, and check long editorial/market content with Large Text. Compare floating numbers with the drawer and task destination through pickup, delivery and table reuse. Use Find on customers moving from spawn, queueing and walking to an assigned table; it must frame their current location, then their table after seating. Repeat affected interactions in all restaurants, tutorial and multiplayer host/guest/reconnect.

## 2026-11-07 — Immediate reward feedback, repeated newspaper opening and cashier purchase capacity

Implemented the approved follow-up through existing owners. DailyRewardSlotUI no longer gates Spin on IsCurrentWallet; wallet preparation starts when the actual reward offer opens. A single accepted click disables duplicate input and starts decorative pending reel updates while the same backend offer resolves. The normal settle animation then uses the returned saved outcome. DailyRewardResolution.HasSelectedOutcome distinguishes selected/pending/confirmed outcomes from unselected offers; only confirmed outcomes invoke Celebrate. Unconfirmed saved outcomes settle without a win celebration and display quiet pending copy. The bounded Continue wait remains, and technical resolve errors are no longer surfaced as retry instructions. No currency grant/retry logic, receipt identity, account check, duplicate-dispatch guard or reward odds were changed. Backend requests still execute at their existing owner; no live reward or currency call was issued during verification.

The newspaper presenter now activates its canvas/overlay before population and forces layout after population. NewspaperLandscapeView keeps a dirty flag through inactive population and enable/disable, and never caches measurements while inactive. The isolated regression now reuses the same presenter and hierarchy across issue replacement while hidden, then reopens and validates generated text/section bounds. Desktop/portrait repeated-open native previews are saved in Documentation/UIConcepts/RewardNewspaperCashierFix and were visually reviewed.

FastFoodProgressionSettings.HasSecondCashier now reflects the second-register purchase without the unrelated Day 15 gate. StaffHiringProgressionSettings.Limit uses that same purchase predicate for Fast Food cashier hiring while preserving existing hires, protected-session handling and other roles/restaurants. EmployeeManager.ActiveSlotLimit and autonomous staff already consume HasSecondCashier, so both hire and active capacity now open after purchase. ManagementComputerHRPanel listens to existing equipment PurchasesChanged and refreshes its current view through the existing deferred refresh owner; reopening marks it for refresh as well. Purchase remains required even after equipment unlock.

Changed: DailyRewardSlotUI.cs, IDailyRewardBackend.cs, DailyNewspaperPresenter.cs, NewspaperLandscapeView.cs, FastFoodProgressionSettings.cs, StaffHiringProgressionSettings.cs, ManagementComputerHRPanel.cs; extended tools/Verification/CustomerDrawer/UnityCheck.cs.txt. Existing unrelated dirty files, saves and the open Unity editor were preserved; no Git history operation occurred.

Verification: runtime and Editor compilation passed with existing unused-field warnings. Isolated production-assembly Unity suite passed 1,696 assertions, including repeated inactive newspaper population/reopening, selected-versus-unselected reward presentation semantics, unpurchased/purchased register capacity before Day 15 and preservation of legacy hires. Backend/Tests/DailyRewardSlot.tests.cjs passed 39 source/asset checks and 1,121 compiled source-linked odds/motion assertions. git diff --check passed. These results do not verify actual asynchronous wallet/backend execution, live Spin input or animation/audio, live equipment purchase/hiring/assignment, network transport, day progression or device behavior.

Manual acceptance: after import restart Play Mode. Press Spin once with the wallet initially preparing: input should acknowledge immediately, remain single-use, settle on the saved result, and celebrate only after confirmed credit. With a real outage Continue remains available and no second grant is sent. Close the first newspaper, advance to the next day and reopen; check normal/Large Text, longer middle-column content and archive navigation. Purchase the second Fast Food register before Day 15, reopen Applicants, verify a 1/2 cashier capacity and enabled Hire, hire/assign the second cashier, reload and verify persistence. Repeat staffing on host/guest without bypassing authority. Live tests were not run automatically.

## 2026-11-07 — Numbered days, contextual Table label, takeout ownership and closed-drawer inspection

Implemented the approved shared-path changes. CasualDiningProgressHUD retains DAY plus actual CurrentDay after the campaign threshold; endless progression itself is unchanged. LobbyHUDRedesign's large contextual booth caption is now Table, while detailed task/customer/bubble identifiers remain unchanged.

PlayerTaskHUD's existing owner applies CompactTaskPanelView.KeepClearOf in LateUpdate after transition updates. It measures final panel transforms and the clipboard plus its visible child graphics in the panel parent's coordinate space, preserving configured separation during slide/scale. It uses a reused graphic list rather than per-frame arrays, falls above on narrow layouts and below if the upper safe-area edge has insufficient room. Existing docking/animation ownership is retained; no second animator or cue was added.

The takeout root cause was TryStartWaiterTask resuming any global HeldBag, including the player's. TakeoutBagInteractable now exposes exact staff ownership from held state, player ownership, global bag identity and the worker's real hold socket. Autonomous delivery only resumes that worker's bag, validates ownership after pickup and before handoff, and refuses player-held or other-worker-held bags. Its abort path cannot clear/destroy a player-held bag or fail that customer's service. Player pickup still only attaches/highlights the bag. A matching locally held non-Fast-Food bag now reuses its existing popup prefab at the customer stand-point with an explicit Give takeout button, original order identifier, movement/arrival validation, cancellation restoration and pending fade. It invokes the existing takeout interactable/network service owner. Fast Food customer self-pickup remains excluded from this bag UI.

CustomerDrawerView's existing world pointer capture is available while the drawer is closed when the gameplay HUD is available. Body presses are consumed before legacy movement/service handlers, release still rejects drags/UI/multitouch, and selecting opens the existing drawer directly to details. Existing tutorial-required world targeting, blocking, scene/recovery cleanup and explicit service UI remain in place. Opening from world selection uses the existing panel slide without an additional panel sound; the release path retains its single selection cue.

Changed: CasualDiningProgressHUD.cs, LobbyHUDRedesign.cs, PlayerTaskHUD.cs, CompactTaskPanelView.cs, CustomerDrawerView.cs, TakeoutBagInteractable.cs, LobbyAutonomousService.cs; extended tools/Verification/CustomerDrawer/UnityCheck.cs.txt. Existing uncommitted work, saves and the open editor were preserved. No commit, push or scene replacement was performed.

Verification: runtime and Editor compilation passed (existing unused-field warnings). The isolated production-assembly suite passed 1,707 assertions. New checks cover exact worker ownership, early rejection of player-held bags, abort preservation, actual worker/other-worker sockets and task separation during simulated slide/scale with oversized clipboard child artwork. Native desktop and portrait task-layout renders were inspected. Evidence: Documentation/UIConcepts/HudTakeoutInspectionFix. git diff --check passed. Tests do not simulate a live takeout handoff, departure, day progression, pointer dispatch, tutorial objective, transport or audio.

Manual acceptance: after import restart Play Mode and confirm Day 31+ displays its number, with Table on contextual hover and numbered task destinations retained. Open/close task popup during new-task button emphasis; repeat narrow viewport/Large Text and check clipboard clearance. Carry takeout while staff are idle or busy: the bag/customer must remain until the explicit matching Give takeout handoff; test cancellation/wrong customer, staff-owned bags and Fast Food self-pickup separately. With the drawer closed and while carrying each item type, click/tap a customer body: only info opens, without movement/service or duplicate audio. Check drags, UI clicks and tutorial-required targets retain their behavior. Repeat applicable cases in Casual/Fast Food/Fine Dining campaign/tutorial and host/guest/reconnect. These live/device/multiplayer/audio checks were NOT RUN.

## 2026-11-07 — Truck badge, accepted-task camera follow and local ground ring

Implemented shared truck locator artwork and camera/identity behavior through existing owners. RestockTruckOffscreenIndicator.prefab now references TruckLocatorButton, preserves aspect at the existing 68-unit allocation and removes the baked exclamation text. Runtime binding adds a real Button/GraphicRaycaster and uses the existing button animation/sound installer as the single click-feedback owner. The marker remains restricted to ready, offscreen deliveries, uses safe-area/marker-size padding and avoids the upper/bottom HUD bands. Clicking frames the truck renderer-bounds center through MainCameraController without a collection or movement request, preserving zoom and existing camera bounds. Station cameras and blocking UI suppress it. Fine Dining and Casual tutorial scene recognition was added to the existing coordinator; tutorial marker visibility is restricted to normal gameplay or the relevant restock/truck lessons. All nine gameplay scene variants have MainCameraController references. No authored camera bounds were disabled or expanded; live ability to frame each truck parking location still needs observation.

PlayerMovement emits AcceptedLocalTask and requests follow only after a successful MoveToInteractable path for a locally controlled non-ground target. The existing camera follows the actual actor at an editable focus offset and retains follow on completion. Intentional pan releases follow and clears temporary drawer camera ownership. Find customer/table and truck framing also release follow, preventing stale drawer-close restoration. An accepted task clears inspection suspension before following; if the gameplay camera is inactive/blocked, it is deferred with the command version and applied only if the same task/player remains current. Scripted framing/manual pan clears pending follow. While following, a change to another local controlled actor rebinds the target. Existing zoom, path, claim, role and multiplayer authority rules remain untouched.

RoleIndicator now owns an optional unlit white annular mesh beneath the locally controlled character, with editable radius (.45), thickness, opacity and ground offset. PlayerMovement.SetPlayerControlled updates it and enables the existing indicator component when required. The mesh is created once per indicator, scales independently of avatar scale, has no spin/pulse/shadows and is hidden for nonlocal/staff control. Mesh/material lifetime is cleaned up by its owner. Existing burger/role selection visuals were preserved rather than globally replaced.

Changed source/assets: PlayerMovement.cs, MainCameraController.cs, CustomerDrawerView.cs, RoleIndicator.cs, RestockTruckOffscreenIndicator.cs and its shared prefab, RestockFlowCoordinator.cs, TutorialSceneRuntimeMarker.cs and the sprite-pack README. Extended tools/Verification/CustomerDrawer/UnityCheck.cs.txt with accepted follow, retained follow on completion, intentional pan, next-task follow, explicit framing, ring uniqueness and nonlocal ring/follow rejection. Existing unrelated changes, saves and the open Unity editor were preserved; no Git history operation occurred.

Verification: runtime and Editor compilation passed with existing unused-field warnings; git diff --check passed. The isolated production-assembly Unity suite passed 1,716 assertions, including the new camera/ring state cases and existing drawer/task/reward/newspaper regressions. Shared sprite GUID/import references and all nine scene camera-controller references were inspected. Final tutorial gating/editable focus-field edits were compiled after that suite. No live truck click, final truck parking/bounds framing, actual accepted/rejected player input, deferred kitchen/restock camera transition, phone swipe/pinch behavior, respawn/role-switch visual playback, host/guest/reconnect, ground-ring floor visibility or audio listening was tested. Test method calls do not establish live input or authority acceptance.

Manual acceptance: restart Play Mode after import. With a delivery ready, pan away on every edge and check the badge remains clear of HUD/safe areas, then click it and confirm the truck is centered without walking/collecting. Check each restaurant's actual parking pose fits its camera bounds. Start a pickup, serving, reception, billing, cleaning or restock task; follow should center once and continue after completion. Swipe/drag to release, then start another task to restore. Check pinch-only zoom and UI clicks do not unlock or fall through. Combine Find customer, drawer close, truck framing, new task, pause and station entry/exit; old camera ownership must not snap back. Verify one white circle under only the local character across role change/respawn/reconnect and no floor flicker. Repeat applicable campaign/tutorial and multiplayer host/guest cases. These live checks remain unverified.

## Multiplayer connection/removal notice — 2026-11-08

- Added Resources/UI/MultiplayerConnectionNotice prefab with editable artwork/font references, copy, size, font sizes and transition timings. The view assembles its UI children from these authored prefab settings.
- Existing MultiplayerSessionUI owns notice refresh/priority. Initial guest synchronization does not report a lost connection. Recovery notices close only after CanAct; Retry uses existing recovery. Host connection loss retains the existing terminal outcome.
- Confirmed host removal sets RemovalPending, stops simulation/voice, clears saved recovery and waits for Return. Return uses the existing leave routine without remembering the removed run. Pause/quit cannot save that pointer again.
- Added eased entrance/exit, status crossfade, Reduced Motion fade, safe area sizing, Large Text support and standard button feedback. Info once per interruption/removal; Success only when restored; no status-refresh sounds.
- Checks: runtime and Editor Roslyn compilation passed; all six sprite references and shell borders checked; Lobby1/2/3 Multiplayer scene session-owner references checked.
- Not run: live Unity render, reconnect/kick with two clients, mobile safe-area/input, audio listening. The main editor and saves were preserved.
- Manual: interrupt guest connection then restore; let recovery fail and Retry; kick guest and confirm notice remains until Return; test mute/Reduced Motion and portrait/Large Text. Confirm ready prompt resumes normally and gameplay remains blocked during recovery.

## Soundtrack and playback balance

- Fuzzball Parade plays in the menus. The three restaurants switch between their assigned preparation and service tracks using the existing shift state, including its multiplayer projection. Hard Boiled and Zazie are imported as unassigned tracks.
- Decoded audio was measured in 50 ms windows. Music uses per-track gain to reach approximately -28 dBFS active RMS before the Music slider. Shared SFX use measured per-cue or per-family gains before the SFX slider, with separate levels for short cues, confirmations, and background sounds. The source recordings were retained.
- Checked the menu and restaurant scene bindings and compiled against local Unity references. Listening, Unity scene playback, and multiplayer audio checks require a live session.

## Fine Dining unread reservation locator

- The shared locator shows the matte bell and booking slip with a live unread count when reception is offscreen. It uses the existing station notification and synchronized booking state, with truck and Serve Food placement priority.
- The supplied bell plays through GameplaySfx on new requests. Initial guest snapshots and recovery are silent.
- Runtime compilation and prefab/scene reference checks passed. In-game positioning, live host/guest behavior, and audio listening remain unverified.

## Fine Dining seating popup and table numbers

- Greeted Fine Dining parties keep the Seat Table presentation while seating is blocked, with a measured explanation under the button. The shared multiplayer presenter also honors this blocked state. The reception indicator uses the same action presenter for refreshes.
- Fine Dining passive table numbers have an editable 64-pixel size at a 1080-pixel viewport and bypass compact action scaling. Matching tray delivery continues using the existing action checks.
- Runtime and Editor source compilation passed, along with diff checks. Play Mode greeting, table release, walk-ins, cancelled selection, receptionist takeover, and multiplayer host/guest remain unverified. Check two-digit badges and zoom in the Game view.

## 2026-11-08 - Partially clipped locator targets
- Replaced any-frustum-intersection suppression in the shared truck, Serve Food, and reservation locator with projected world-bounds coverage of camera pixelRect intersected with Screen.safeArea. Editable prefab defaults: 24 px inset at 1080 viewport height, hide at 75%, reappear below 65%.
- Eight corner projections reject near-plane crossing/behind-camera targets; each destination retains independent hysteresis. Eligibility loss/new destinations reset the initial threshold. Placement conflicts do not change visibility hysteresis. Existing eligibility, arrows, counts, framing, and placement priorities preserved.
- Truck bounds reuse its cached presentation renderers. Shared RestockFlowCoordinator resource binding covers existing applicable restaurant/mode routes without scene changes; reservations remain Fine Dining only.
- Passed runtime and Editor C# compilation with local Unity references; targeted diff whitespace checks; 25 isolated math-model assertions (edges, corners, exact thresholds, hysteresis, resolution scaling, portrait safe viewport), plus shared-call-site/near-plane source checks.
- NOT RUN: Unity Play Mode, rendered projection comparisons, tapping/frame-to-hide checks, live host/guest/reconnect, device panning/zoom, audio listening. Manual check: pan truck/table/reception across edges, verify stable 75/65 transitions, tap to frame, then pan away; repeat portrait/safe areas. No editor control or save changes.

## Kitchen card reference polish - 2026-11-08
- Shared FastFoodCookingView applies existing CustomerDrawerArt frame/header/track and fonts to its explicitly bound station card. Live recipe icon, quantities and existing instructions remain state-driven. Measured text heights move the progress track and ticket region to accommodate wrapping.
- FastFoodCookingTicketView applies the matching ticket frame/header, larger counts, timer in header and footer hint, retaining existing scrolling, progress smoothing and ticket motion. Existing audio ownership retained.
- Runtime and Editor compilation passed; final runtime recompiled after measured-height adjustment. Unity visual matching, portrait/Large Text and campaign/tutorial/multiplayer interaction have NOT been observed. No claim of pixel-exact reference matching until Play Mode review. No new completion cue or check animation added in this pass.

## SFX loudness pass — 2026-11-08
- Normalized 38 supplied/generated SFX assets in place; retained paths, GUIDs and importer metadata. Music untouched. Offline target -23 LUFS, -3 dBTP processing ceiling; sub-400ms cues use -23 dBFS RMS fallback. MP3 encoding can overshoot the processing ceiling: measured exported peaks remain below -1 dBTP.
- Shared foreground gain is now .8 master x .8 cue (.64), replacing inconsistent cue attenuation. Kitchen foreground .512 x 1.25 = .64; reward, reservation bell and truck horn .64. Serialized Fast Food variants and class defaults agree. Background/continuous loops retain softer contextual levels; world distance attenuation and player mixer controls remain intact.
- Tool: Tools/Audio/balance-sfx.cjs. Measurement report: Tools/Audio/sfx-balance-report.json. Original audio backup: Temp/SfxBalanceOriginals (local, not versioned). Do not rerun from processed files after deleting that backup; restore original source audio first.
- Passed: 38 encoded-asset measurements (LUFS within .8 dB; short-cue RMS within .2 dB; true peaks below -1 dBTP), runtime and Editor source compilation. This is not a guarantee against summed-bus clipping under simultaneous effects.
- NOT RUN: Unity listening, live multiplayer/device playback, mute/slider interaction. Manual: compare menu clicks, kitchen placement/ready, serve/payment, reservation bell, horn and reward at the same SFX slider level; then check simultaneous cues and mute. No desktop/editor control performed.


## Restaurant access reference polish - 2026-11-10
- NewGameMenu restaurant access uses the Day Panel V2 frame, ribbon, cash and calendar icons, progress sprites, and action artwork. Fast Food and Fine Dining previews are rendered from the scene's existing FBX models.
- Reuses the existing panel transition and button feedback, with a short ribbon pop that respects reduced motion. Purchase, eligibility, and save flows continue to supply panel state.
- Static art reference and transparency checks passed. Unity compilation and Play Mode layout, safe areas, reduced motion, and purchase interaction are unverified.

## Restock capacity and footer feedback — 2026-11-12

- Existing shelf placements and pending orders count against capacity. Older stock without a shelf placement remains usable and no longer fills shelf slots. The restock window shows the capacity warning in its existing footer.
- Passed Unity C# compilation with project references, static footer binding checks, and an isolated C# fixture for migration, discarding boxes, checkout charges, reservations, and placement.
- The existing Day 30 save, Unity Play Mode, and live multiplayer remain unverified. Check the restock footer and place a one-box order in the Editor before rebuilding Android.

## Customer and task status text fit — 2026-11-12

- Customer table/status badges and compact task destination/table badges now stay on one line, auto-shrinking within a readable lower bound and ellipsizing only if the available width is still insufficient. This prevents wrapped label text from spilling into adjacent status lines.
- Static layout/source checks and Unity runtime C# compilation passed using the generated Unity response file, with output written to a temporary folder. The compile reported existing warnings in unrelated files.
- Live Game view visual confirmation remains unverified. Manually check the Customer Details list with “Awaiting number” and “Walking to table”, and the Current Task card with “At counter” and “Not seated”, at desktop and narrow/mobile UI scales.
