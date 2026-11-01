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
