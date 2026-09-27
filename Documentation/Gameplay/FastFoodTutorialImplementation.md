# Fast Food tutorial — implementation handoff

Implemented in the existing `Assets/_Project/Scenes/RoleBased/Lobby2Tutorial.unity` scene.
The normal Lobby2 scene was not replaced. The tutorial scene remains editable in the Inspector.

## Authoring

- `FastFoodTutorial` root: shared `TutorialSystem`, scene bindings, isolated day/catalog context and `FastFoodTutorialBridge`.
- `TutorialSystem.steps` and `FastFoodTutorialBridge.lessons` contain 95 corresponding entries. Keep their IDs/order aligned.
- `Assets/_Project/Tutorials/FastFoodTutorialPresentation.prefab` contains the copied Casual Dining dialogue, Big Boss portraits, cursor/hand, target indicator and mask presentation, plus training controls.
- Existing station rigs, three prep targets, cooking slots, recipe assets, food visuals and Blue/Gray button sprites are reused.
- `FastFoodTutorialAuthoring.ValidateScene()` checks matching IDs, chapter phases, portraits, serialized references and the enabled build-scene entry.
- The authoring command refuses to overwrite an existing tutorial. Change saved steps/references rather than rerunning the initial authoring routine.
- Stock allowance and dependency timeout allowance are editable on the bridge. Existing station/camera and shared tutorial presentation tuning remain in their existing components.

## Course

| Chapter | Actual player work |
| --- | --- |
| Briefing | Big Boss briefly recaps management, introduces the kitchen and hotbar, then asks the player to enter. No repeated lobby-management course. |
| Burger | Select Grill, drag a raw patty to a cooking slot, wait, collect it, then follow the existing assembly recipe. |
| Fries | Select Fryer, load, wait and collect the ready basket. |
| Serving | Select Assembler, match a burger, fries and drink to the tray, then press Serve. |
| Parallel prep | Cook three patties, collect the full batch, begin all three prep slots and finish each burger independently. |
| Sandwiches | Remain at Grill while staff cook chicken/fish protein, then assemble each sandwich using the real recipe steps. |
| Recovery | Deliberately burn one training patty, discard it, cook another and finish the burger. |
| Restock | Open the kitchen Restock card, enter real storage, store one supplied training box and return. |
| Hygiene | Choose the real Clean Now action and wait for cleaning to finish. Other choices are disabled only during this lesson. |
| Practice | Complete a two-burger/fries/drink order, then a chicken/fish/drink order using the existing kitchen transactions. |
| Complete | Leave training for a fresh normal Day 1, or return to Game Mode when replaying/an existing career is present. |

## Runtime contracts

- This is an adapter to the existing kitchen, not a second cooking simulation. Load, collect, discard, ingredient placement and serving use `FastFoodCookingState`.
- An optional state interaction filter restricts guided actions to the expected portion, ingredient and target. It is unset during normal gameplay.
- The kitchen controller delegates ticking to the bridge only while this scene-local tutorial is active.
- Explanations, pause and skip confirmation stop training kitchen ticking. Order/batch deadlines are disabled. Cooking/overcooking still run during relevant action steps.
- Player station ownership is retained across guided station visits; normal station handoff timing is unchanged outside training.
- Completion checks observe actual state. Completing a step is not a substitute for consuming ingredients or completing a recipe.
- Finished prep uses the existing immediate transfer/reusable-slot behavior. The tutorial does not add another pickup delay.
- Serving guidance resolves the existing hotbar cells. Cooking/collection/assembly targets resolve the existing station slot transforms.
- World guidance uses the active kitchen camera. Presentation stays independent of the station HUD and respects the shared safe-area container.
- The storage coordinator leaves tutorial canvases available while hiding the lobby. The shared mask does not block pause; tutorial canvases/hints hide while pause is open.
- Dependency waits have bounded failure messages. Small Retry Chapter / Skip Training controls remain hidden until failure; intentional burn training does not expose recovery controls.

## September 27 presentation/input correction

- The saved presentation uses 1920x1080, matching Casual Dining's runtime canvas override. Its copied portrait/dialogue dimensions remain unchanged; the previous standalone 800x450 canvas magnified them 2.4x.
- Big Boss opts into the existing portrait pop on each line; Casual Dining defaults are unchanged.
- The real `LobbyHUDRedesign.KitchenButton` opens training. The duplicate ENTER KITCHEN button was removed.
- The kitchen welcome, header, navigation, pause, task, restock, hotbar quantities/READY/empty states and work surfaces are explained before the first patty load. Fryer and tray UI receive introductions too.
- Passive cooking, staff-protein and intentional-burning waits suppress action targets and hand hints. Ready food is protected during guided chapters except intentional burn and free practice.
- Grill collection targets the actual food, not the generic bay anchor.
- Actual input failure: fryer basket trigger padding was authored in local units under scale 300. Those enormous colliders intercepted Grill clicks. Tutorial basket bounds now convert padding to local scale; basket input is enabled only in the active Fryer view. The authoring utility uses the corrected conversion for future authoring.
- `PolishExisting()` updates the existing presentation/lesson list and tutorial collider bounds without rebuilding the kitchen.

### Verification for this correction

- Unity compilation and saved-scene validation passed (95 matched lessons).
- Play Mode timer wait had no active focus mask.
- EventSystem raycast reproduced the blocker: Lift Basket 4 was the first hit ahead of the ready patty. Disabling inactive basket targets made the patty first; dispatching the actual pointer-click handler collected it (`proteinReady=true`, `Preparing`).
- Final runtime check confirmed inactive basket targets disabled, normal Kitchen button visible/interactable and its real handler advanced to the welcome lesson.
- Recovery buttons were hidden normally and appeared for an injected failure.
- Preferences and shared skybox rotation were restored after checks.
- Full course, mouse/touch dragging, visual aspect-ratio review and platform builds were NOT rerun in this correction. Earlier smoke results below are historical, not proof of those interactions.

## Isolation, entry and progress

- Fast Food campaign entry routes to `Lobby2Tutorial` unless its own completed/skipped flag is set. Casual Dining retains its own flag.
- The existing tutorial entry button uses the selected restaurant for replay.
- Chapter entry is the checkpoint, not an arbitrary mid-drag state. Resuming reconstructs the chapter's training prerequisites.
- Keys: `DineIn_FastFoodTutorial_Completed`, `DineIn_FastFoodTutorial_Skipped`, `DineIn_FastFoodTutorial_Chapter_v1`.
- Replay does not overwrite career progress or mark an unfinished tutorial completed.
- `TutorialDayContext` clones Fast Food recipes/items and isolates stock, equipment and runtime day state. Assembly steps and the first cooking ingredient are remapped to cloned items as well as ordinary recipe ingredients.
- `CampaignSaveStore.ProtectedSession` recognizes the tutorial. Career writes are suspended during training; original runtime state is restored when leaving.
- A fresh normal Fast Food career is initialized by the existing normal-scene save/load flow. Training stock, orders, temporary restock boxes and practice earnings are not promoted into it.
- `FastFoodScene.Contains` narrowly includes Lobby2Tutorial in existing Fast Food scene guards. Multiplayer guards remain intact.

## Verification performed (Unity 6000.0.40f1)

- Unity recompilation passed with no compilation errors.
- Saved-scene validation passed: 83 unique matched steps/lessons, correct chapter phases, portraits, serialized references and build entry.
- Play Mode API smoke traversal passed guided chapters through storage and hygiene, then both practice orders and completion.
- The smoke traversal invokes real kitchen actions; cooking/cleaning clocks were advanced for the check. It is not a mouse/touch interaction test.
- The actual storage scene was opened and closed. The storage transaction was verified through its existing backend, not a simulated pointer drag onto a shelf cell.
- Existing `FastFoodBatchRegression.Run()` passed: six-burger batches, simultaneous grill/fryer bays, both sandwich handoffs, exact consumption and staff continuation.
- Pause check passed: time scale zero, tutorial mask does not intercept input, tutorial canvases hidden. Resume/retry were exercised.
- A fresh Play Mode launch resumed at the Sandwiches chapter boundary. Runtime recipes/items were non-persistent clones and every assembly ingredient resolved to the cloned catalog.
- Persistence checks reported protected tutorial session, no runtime campaign and active save-application isolation.
- Big Boss opening and completion game renders were inspected using Unity's render capture (no desktop automation).
- Casual Dining save, Fast Food save and Fast Food day-start checkpoint hashes matched their pre-test values.
- Test completion was not persisted; test-created chapter progress was restored afterward.
- The existing skybox rotation script changes its shared material during Play Mode. Its test-induced rotation was restored to the pre-test value; no skybox change is part of this feature.
- `git diff --check` passed. Temporary render captures were removed after inspection.

## Manual acceptance still required

1. Enter from Game Mode on an unfinished Fast Food tutorial; check actual Next, mouse/touch drag/drop, hand cues and masks.
2. Play the complete course without API-driven input, especially basket collection, all three prep targets and physical shelf placement.
3. Pause during explanations, cooking, a drag and storage; resume without unintended clicks or loss of progress.
4. Retry while a patty is burnt and while inside storage; leave/relaunch at a chapter boundary.
5. Cancel Skip, then test confirmed Skip. Verify incomplete/skipped/completed states stay distinct.
6. Test replay without changing an existing career; test fresh-career completion to normal Fast Food Day 1.
7. Check 16:9, 16:10, wide Android landscape and tablet landscape, including non-default UI scale and safe-area insets.
8. Check Windows/Android builds and a real touch device. No platform builds, device tests, full pointer-driven course or multiplayer session were run in this pass.

Historical Editor console errors from the prior upgrade smoke test and occasional Pipeline command timeouts were present. No tutorial gameplay exception was observed during the successful course traversal.
