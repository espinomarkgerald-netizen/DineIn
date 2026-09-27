# Fast Food tutorial target-resolution audit

Source audit: 2026-09-27. Covers the current 97 authored lessons and their saved bridge/step entries. This is not a runtime walkthrough or a guarantee against softlocks.

## Defects repaired

- `ff_hud_header`: the real header exists on both device layouts. `FastFoodCookingView.Refresh` hides it without player work. `SetupChapter(Burger)` creates the exercise before `ff_Burger_station`; the presentation-readiness exception in `TickKitchen` used to tick staff production during that station-selection lesson. Staff could start the burger before `State.Enter(Grill)`, which correctly refuses to steal an already-started staff operation. The tutorial now freezes ledger advancement for Open/Station actions even during target preparation. Camera/UI updates are not frozen. The header itself is not forced on or replaced.
- `ff_Fries_wait_0`: food anchors are children of real basket equipment under Restaurant, not the Fry Station rig. The adapter now resolves the portion from the same current `cookingVisuals` list used by collection. This also avoids selecting old transient handles awaiting destruction in other cooking/collect/discard lessons.
- `ff_fryer_ui`, `ff_Fries_collect_0`: the current station's authored basket reference supplies its real collider bounds, rather than relying on whichever child mesh a generic lookup returns first.
- `ff_discard`: the drop destination is `discardBox`, a RectTransform. The saved explanation now uses the existing `FF.Discard` UI key; its world-target entry is retained as the drag destination. The mask no longer attempts to project this UI element through a 3D camera. The actual burnt food remains the drag source.
- `ff_fries_done`: the normal rack camera glance expires after collection. The tutorial adapter renews that existing glance only during the rack explanation, using the existing `prepLookSeconds`. No camera anchors, equipment, food, or normal-game timing settings change; renewal stops on lesson exit/failure.
- Hotbar targets now respect active Mask/RectMask2D clipping in readiness checks. The existing auto-scroller prepares the real cell. Disabled/zero-stock cells remain valid explanatory subjects.
- `ff_stock_store`, `ff_stock_exit`: readiness also waits for the native storage transition. Storage sources resolve before visibility validation so an initializing cell is not discarded before layout preparation. Cached storage targets clear at lesson entry/retry.

## Complete sequence coverage

Patterns below cover every lesson in the named chapter, including all individual ingredient/slot actions. The IDs, recipes, portion indices, slot indices, ingredient indices, targets, messages and objectives are checked against the saved scene by the existing authoring snapshot comparison, not just by count.

| Chapter / count | Lessons and actual targets | Prerequisite and lifetime review |
|---|---|---|
| Briefing / 6 | `ff_welcome`, `ff_lobby_recap`; `ff_hud_pause`, `ff_hud_task`, `ff_enter`, `ff_kitchen_welcome` | Untargeted narration needs no geometry. Pause uses `LobbyHUDRoot.PauseMenuView.PauseButton`; Task uses the live controls' `SafeArea/TaskButton`; Kitchen uses `KitchenButton`. Selection is requested only after real kitchen entry. |
| Burger / 16 | `ff_Burger_station`, `ff_hud_header`, `ff_hotbar`, `ff_hotbar_counts`, `ff_hotbar_empty`, `ff_grill_area`; `ff_Burger_load_0`, `ff_Burger_wait_0`, `ff_Burger_collect_0`; `ff_prep_area`, `ff_ready_ingredient`; `ff_Burger_02_0_ingredient_0..3`; `ff_burger_done` | Exercise remains unstarted until player station entry. Header/hotbar are real active view references; raw and READY cells are distinguished by ingredient and raw flag. Actual slot 0 and current food are used. Prep camera/PrepReady gates precede ingredient actions. Final layer waits for real completion and pickup. |
| Parallel / 24 | `ff_parallel_intro`; `ff_Parallel_load_0..2`, `ff_Parallel_wait_0..2`, `ff_Parallel_collect_0..2`; `ff_three_slots`, `ff_parallel_bun_0..2`; `ff_Parallel_02_{0..2}_ingredient_{1..3}`; `ff_parallel_done` | New work is provisioned at the already-owned Grill. Cooking positions 0/1/2 and prep positions 0/1/2 exist in the saved rig. Food binding is per portion. Ready protection remains enabled; no action can satisfy another slot's ingredient gate. |
| Sandwiches / 11 | For each `ff-chicken-sandwich` / `ff-fish-fillet-sandwich`: `ff_protein_{recipe}`, `ff_sandwich_ready_{recipe}`, `ff_Sandwiches_{recipe}_0_ingredient_0..2`; `ff_sandwich_done` | Staff protein waits have no required hidden action target. Real staff production runs at Fryer while Grill remains owned. READY explanations follow protein availability; assembly uses chicken slot 0 and fish slot 1. |
| Burn / 11 | `ff_Burn_load_0`, `ff_Burn_wait_0`, `ff_discard`, `ff_reload`, `ff_retry_wait`, `ff_retry_collect`, `ff_recovery_prep`, `ff_Burn_02_0_ingredient_0..3` | Intentional burn is the only guided unprotected overcook wait. Discard UI exists only with a real burnt player portion. Replacement readiness/collection/prep remain real state gates; replacement protection is preserved. |
| Fries / 8 | `ff_hud_navigation`, `ff_Fries_station`, `ff_fryer_ui`, `ff_fryer_raw`, `ff_Fries_load_0`, `ff_Fries_wait_0`, `ff_Fries_collect_0`, `ff_fries_done` | Arrow is chosen from the real enum station ring. Fries work is created after Fry ownership. Basket comes from the active rig's array (equipment lives outside the rig). Current food comes from the live collection list. Rack explanation retains the existing collection view while reading. |
| Serving / 9 | `ff_Serving_station`, `ff_ticket`, `ff_tray_ui`, `ff_assembler_hotbar`, `ff_place_02`, `ff_place_03`, `ff_place_04`, `ff_serve`, `ff_served` | Normal progress preserves cooked prerequisites; checkpoint reconstruction restores only prerequisites. Ticket is matched to `State.PlayerTicket`, not the first card. Item resolves to the current ticket's unplaced portion. Tray footprint uses real `AssemblyContact`/`AssemblySlot`; refreshed handles follow rebuilds. Serve is requested only after all three placements and completes only from submission. |
| Practice / 5 | `ff_practice_intro`, `ff_practice_navigation`, `ff_practice_exit`, `ff_practice`, `ff_practice_done` | Existing Stations/Exit controls are visible even when disabled during their explanations. Practice then restores navigation. Nonblocking navigation hints resolve from current mode/selection/restaurant. Progress counts newly issued work; tickets advance by real completion/submission, with no station-cycling gate. |
| Hygiene / 2 | `ff_hygiene_intro`, `ff_hygiene_clean` | Native tutorial cleaning decision opens before focus resolution. `TutorialCleanControl` resolves the visible native button; unscaled readiness accommodates its decision pause. Once cleaning starts the hidden button is no longer required by guidance: passive wait suppresses it until real cleaning completion. |
| Restock / 4 | `ff_stock_intro`, `ff_stock_open`, `ff_stock_store`, `ff_stock_exit` | Training supply is configured before notice display. Actual notice and Go to Restock button are used. Native transition settles before storage guidance. Supplied item is matched in the persistent RestockFlowHUD, not assumed to belong to RestockScene. Shelf is compatible/free/visible; exit comes from the active RestockScene. |
| Complete / 1 | `ff_complete` | Untargeted completion uses existing completion controls/routes. No station reopening or future geometry dependency. |

## Retry, platform, and presentation review

- Chapter numeric identities/order are unchanged: 0,1,4,5,6,2,3,9,8,7,10.
- Existing chapter reconstruction is retained: Burger starts on station selection; Fries reconstructs at Grill for the arrow lesson; Serving starts at Fry with prerequisite ready food; other cooking chapters reconstruct at Grill; Practice/Hygiene/Restock reconstruct at Assembler. No automatic station change was added to normal progression.
- `StartAtStep`/`ClearGuidance` cancel framing, handoff and layout preparation, increment the presentation revision and clear UI/world targets. Bridge lesson entry resets failure timers and current-source caches; storage caches now reset too. Failure reports remain lesson-guarded and one-time, with player-work/expected-stage context added.
- Both layouts use the same authored cooking Canvas/rigs and runtime HUD owners. Target resolution uses active objects, current canvases/cameras and the safe area, never Windows screen coordinates or inactive platform duplicates. Existing mouse/touch terminology and gestures are unchanged.
- Explanation, dismissal-release consumption, action handoff, animated spotlight and drag-safe mask behavior remain in the existing presentation system. Passive waits retain objective-only action guidance. No timeout increase, dummy controls, skipped lesson or fake completion was added.

## Verification and remaining uncertainty

- Lightweight runtime C# compilation: passed.
- Lightweight editor-authoring C# compilation against updated runtime references: passed.
- Full saved authoring/bridge/step snapshot comparison: passed, 97 lessons; the scene diff is only `ff_discard`'s UI key.
- Play Mode, automated gameplay, device walkthroughs and builds: NOT RUN, per request.
- Manual confirmation is still needed for actual runtime camera/animation geometry, Windows/touch input and recovery. After scripts reload, Retry Chapter reconstructs a burger already stolen by staff in the previously failed run; the patch does not falsify that existing food's state.

