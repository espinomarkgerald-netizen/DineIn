# Lobby2 Fast Food — implementation and verification

## Scope

Single-player Lobby2. Casual Dining keeps its existing service path. Multiplayer scene, protocol, and result recording are unchanged. Existing imported Fast Food artwork and the user's pre-existing asset changes are retained.

## Implemented flow

1. Groups spawn on the outside-left pavement, walk through the two authored left-entrance waypoints, then choose a shortest available queue. There are two cashier counters and two self-service kiosks; every station owns independent queue/phase state.
2. Counter customers use the existing manager order-review controls or the assigned cashier. Kiosk customers spend five gameplay seconds choosing, then use the existing order/stock confirmation operation. Kiosks do not display a waiter/order-taking action bubble.
3. Kiosk customers have an editable 70% chance to pay there automatically. The remaining customers join a shortest cashier queue with the **same confirmed order and order number**. They do not review or reserve ingredients again. Counter customers pay cash/change or card through the existing payment controls/unlock rules. Kiosk automatic payment is independent of the counter card-terminal upgrade.
4. Payment is valid only for that station's arrived front customer. Settlement is reserved before finance callbacks, so repeated confirmation cannot charge again. Cancelled manager payment can be reopened at the matching counter. A single hired cashier alternates available counters; the manager can serve the other counter. This does not duplicate the hired employee.
5. Payment frees the station. Customers move to separate paid-waiting spaces. Dine-in groups choose and reserve a compatible clean table themselves. Full tables keep the paid order waiting.
6. Takeaway cooking begins after payment; dine-in cooking begins after seating. Both use KitchenManager, the existing recipes and stock rules. Seating does not reserve stock again or create duplicate kitchen jobs.
7. Lobby2 defaults to 100% customer tray pickup. One member collects the ready tray and returns via the table approach. The table/order number stays at the table's authored number anchor. Unreachable pickup retains manager delivery; unreachable return fails/refunds an unserved order rather than teleporting a customer through the restaurant.
8. Tray anchors apply the model's horizontal orientation and offset its non-centred pivot on all three table prefabs. Manager delivery and customer pickup use the same anchors. Clicking a table selects its outline and routes available dirty-tray pickup/cleaning; carrying a matching meal still uses the existing delivery interaction.
9. Takeaway groups walk to the pickup point and collect their matching bag. The manager can still deliver a bag if the customer cannot reach pickup. Dine-in groups eat and leave without a second bill/payment. The busser and manager retain dirty-tray disposal and hygiene controls.
10. Unserved prepaid failures receive one refund. Prepared-food waiting has an editable timeout; kitchen cleaning and waiting for a seat do not spend it. Day teardown resets every station, current customer, payment, claim, kitchen output and held item.
11. Lobby2 uses Chef, Barista, Cashier and Busser. Receptionist and waiter objects are disabled in the scene and remain disabled when assignments refresh. Their roles are hidden from Lobby2 HR and opening requirements; the saved employees and other restaurants remain intact.

## Editable hierarchy

Open `Assets/_Project/Scenes/RoleBased/Lobby2.unity` outside Play mode:

- **Fast Food Services / FastFoodRestaurant**: customer mix, self-pickup probability, all four service stations, entrance waypoints/travel timeout, paid-waiting spacing, pickup timeout, ready-food timeout and kitchen/payment references.
- **Fast Food Services / Dining Tables - Editable Service Anchors**: 15 prefab instances: eight four-seat booths, five two-seat long tables and two four-seat round tables (50 seats). The group keeps its original name to preserve the existing tables' hygiene identities. The second round table was visible art without a service proxy and is now registered too. Window bar stools remain decorative.
- **Fast Food Services / Cashier Counter 1 and Cashier Counter 2**: FastFoodServiceStation, the cashier prefab, and their own queue/flow references. Counter 1 references the existing queue/flow objects and anchors under Counter and Waiting Points; counter 2 owns its queue/flow components and anchors. Each cashier prefab has a click collider, outline, service interaction and staff-side Cashier Approach. The station and restaurant links are scene overrides.
- **Fast Food Services / Kiosk 1 and Kiosk 2**: independent queue/flow components, Customer Order Point, QueuePoint_01/02, Overflow Root and a Fast Food Kiosk prefab instance. Edit Kiosk Order Seconds and Kiosk Pay Here Chance on FastFoodServiceStation. Set the chance to 0 or 1 to exercise each payment path externally.
- **Fast Food Services / Counter and Waiting Points**: first counter queue points, customer food-pickup approach and six paid waiting positions. Overflow roots define queue growth direction; kiosk queues face east, counter queues face the counter. Paid waiting is separated from ordering queues.
- **Fast Food Services / Fast Food Sink**: both existing sink meshes, click collider, wash interaction, hygiene marker and Sink Approach, in one prefab.
- **Fast Food Services**: Customer Spawn - Outside Left, Left Entrance - Outside, Left Entrance - Inside, Customer Exit, takeaway output and ground click collider. Move these anchors in Edit mode to tune the route. Its Navigation Surface field explicitly references the existing active surface on Fast Food Revamp; the empty duplicate surface is disabled.
- **GameManager / LobbyAutonomousService**: existing staff service settings, now authored once to prevent duplicate runtime creation.
- **GameManager / RestaurantManager / KitchenManager**: existing recipe cooking/preparation and output slots.
- Existing staff home/work/trolley points have Lobby2 positions. Existing shared restock/hygiene systems are reused, including their normal runtime customer/item/UI instances.
- `Resources/MenuCatalog.asset` explicitly maps the Fast Food menu to Lobby2.

### Reusable prefabs and editing

`Assets/_Project/Restaurant/Prefabs/FastFood/` contains **Fast Food Booth**, **Fast Food Long Table**, **Fast Food Round Table**, **Fast Food Cashier** and **Fast Food Sink**. Open them in Prefab Mode outside Play mode. Meshes and materials reference the original model assets. Scene instances retain their original mesh/material references and world transforms.

Each table owns `Furniture`, `ApproachPoint`, `FacingPoint`, `SeatPoint_01...`, `TableFoodSpawn`, `TableNumberAnchor` and `Table Cleaning`. Edit these transforms directly. `Booth.seats` sets capacity; keep it matched to usable visible seats. FacingPoint controls customer facing independently of food placement. The right-hand booth row overrides ApproachPoint toward its outer aisle. Long-table points follow their offset tabletop and integrated stools. One `BoothDeliverInteractable` handles delivery, and one `HygieneSurface` tracks dining dirt. There are no table-side bill/money or legacy puddle spawners.

The FastFoodTable Inspector now includes anchor-selection shortcuts and authoring diagnostics. Right-click its component header and choose **Validate Table Authoring** for a direct check. Disabled, white, width-4 QuickOutline components are authored on all three furniture prefabs, both cashiers, both kiosks, sink and paper bag. Edit these components in Prefab Mode. A separate unused menu-book anchor or competing tray registry is not required: Fast Food orders happen at the counter, and the existing FoodTray/Booth lifecycle remains the tray source of truth.

## Queue, input and day-boundary repair

- Queue membership is FIFO; duplicate enqueue/removal/departure is harmless. A destroyed, inactive or departing front no longer blocks promotion. New arrivals do not restart movement for unchanged slots.
- Only the current front can reserve payment. Human order confirmation hands the claim to its payment continuation after stock submission succeeds. Review and open payment panels suspend the Fast Food phase timeout; cancellation restores normal timeout and allows retry.
- Successful payment releases the counter once. Duplicate kitchen-finished callbacks cannot start multiple customer collectors. Customer-returned trays preserve their world scale.
- Lobby2 outline selection is driven by the target accepted by PlayerMovement or the existing hygiene selection path. It does not run an independent pointer raycast. Unavailable/destroyed targets, movement cancellation and blocking panels clear selection. Paper-bag world clicks use the normal mouse/touch and UI filters; pickup buttons retain their existing approach sequence.
- GameDayManager still owns the clock/spawn/results loop. Start validates service references before opening. At closing, unserved customers finish their existing outcome/refund path before finance results. Closing and subsequent starts clear scene-local queue/flow/kitchen state, staff jobs, held items, table reservations and pending payment UI. A service generation invalidates delayed payment continuations.
- The delivery-truck edge indicator now supports Lobby2. Lobby1 selection remains on its existing path; multiplayer Fast Food is not enabled.

The existing **Computer Final** scene prefab is active, connected to ManagementComputerCanvas, and its stand point is at floor height. The complete HUD remains editable at `Assets/_Project/Resources/UI/LobbyHUD.prefab`, also available through **Tools > Dine In > UI > Open Complete Lobby HUD Prefab**. Camera/computer buttons retain their original assets and listeners. Zero scales on the controls, progress and pause canvases were reset to one; rebuilding the combined HUD now also normalizes its canvas branches.

`BoothMessCleanUI.cs` was renamed to match its existing public class while retaining its original `.meta` GUID and code. Existing prefab script references remain intact.

## Required Unity authoring/verification step

**No Unity launch, compilation, build, navigation bake, or gameplay test was performed by this implementation session.** This follows the user's restriction. C# syntax parsing and serialized-reference checks are not proof of playability.

The existing user-authored bake at `Assets/_Project/Scenes/RoleBased/Lobby2/NavMesh-Fast Food Revamp.asset` is preserved on its original transform. Previously Start Day checked a different, empty surface; it now checks the assigned active surface. A saved bake does not establish that every corrected service point is reachable. Include this asset and its `.meta` in the commit along with the scene.

On the external Unity **6000.0.40f1** verification machine:

1. Open Lobby2. Resolve any Unity compiler/import errors first.
2. Run **Dine In > Fast Food > Validate Lobby2 Structure**.
3. Run **Validate Lobby2 Navigation**, including both booth aisles, cashier, computer, sink, kitchen and pickup points.
4. If routes fail or geometry has changed, run **Dine In > Fast Food > Bake Lobby2 Navigation** on the external machine. It updates only a Lobby2 navigation asset. Save the scene and asset, then validate again. Visually inspect seating and tray alignment for all three table types.
5. Verify manager movement, camera framing, stock-room access and truck collection before starting service. These require runtime/visual validation after the layout change.
6. Separately, enter Play Mode in an empty scene on the external machine, then run **Dine In > Fast Food > Run Service Regressions (Empty Play Mode Scene)**. It creates temporary fixtures and covers FIFO, unchanged slots, destroyed-front recovery, repeated removal/departure, authored overflow, independent station ownership, kiosk-to-counter transfer without another order review, rejection before counter arrival, duplicate/stale settlement reservations, repeated reset, and presentation without movement commands. It refuses to run over a live restaurant and removes its fixtures afterward. This runner has been prepared but has not been executed here.

## External regression checklist

Use a disposable Fast Food save. Do not treat a staff-completed shift as proof that human controls work.

| Scenario | Required result |
|---|---|
| Human dine-in order | Review correct quantities; stock deducted once; cash/change completes once; counter advances; customers choose seats; one tray appears. |
| Card/cancel/retry | Unlock card equipment; cancel and reopen payment; confirm once; no stale claim, duplicate earnings or stuck hands. |
| Customer pickup | Set self-pickup probability to 1; one member collects; the rest stay seated; tray lands on the matching table; eating begins. |
| Manager delivery | Set self-pickup probability to 0; manager completes pickup/delivery at all three table types. Wrong table cannot consume the tray. Restore 1 for normal customer self-pickup; Fast Food has no waiter delivery job. |
| Takeaway | Set takeaway probability to 1; pay; next customer can order; collect matching bag; deliver to numbered waiting group; group exits once. |
| Mixed/full tables | Restore probabilities; fill every compatible table; paid groups wait without stealing occupied/dirty seats or consuming stock again. Clean/free a table; oldest compatible group seats. |
| Kitchen/output pressure | Fill output slots; kitchen queues or safely fails/refunds; never stacks two outputs in one slot. Pause kitchen for cleaning while orders are queued. |
| Recovery | Interrupt customer pickup route, cancel review/payment, let ready food time out, close the day with waiting customers. No permanent reservation, orphaned held item or double refund. |
| Full loop | Manager disposes dirty trays at the sink, cleans tables, restocks dry/freezer items, reads newspaper and starts two consecutive days. Reopen the save and verify restaurant isolation. |
| Table prefabs | Each of the 15 tables seats its full capacity without overlapping customers; facing, tray placement and number position match the model. Leaving vacates all seats; occupied, dirty or tray-blocked tables cannot be reserved. |
| Table cleaning | Clean each furniture type through both manager hold-to-clean and staff requests. Dirt/shine follows the same table; no duplicate prompt or missing-script error. |
| HUD/stations | Camera focuses the manager; computer opens through its button and world click. Cashier reopens payment; sink disposes the tray. All controls return after management, pause and restock panels close. |
| Casual Dining regression | Lobby1 human seating, ticket/cooking, bill, cash/card, restock and hygiene remain unchanged. |
| Queue recovery | Queue at least four groups; add another while the others stand still; delete the front in a disposable run. Remaining groups advance once, in order. Cancel order/payment and retry without skipping the front. |
| Selection parity | Mouse and touch select the same service object that highlights. Dragging the camera or tapping UI cannot pick up a bag. Cancel movement, open a modal, let staff claim the target, and destroy a selected item; no stale outline or new command is generated. |
| Day boundaries | End with a held bag/tray, active payment, queued customers and paid customers waiting for tables. Refund each unserved paid order once; no retained hands, jobs, seats or UI after results/start. |

During each scenario, **Dine In > Fast Food > Check Live Service Invariants** checks duplicate output, payment-before-seating, unique table ownership and counter release. This diagnostic does not simulate button presses or replace the checklist.

## Remaining verification limits

`python tools/validate_lobby2_authoring.py` performs dependency-free, read-only checks of prefab ownership, unique seats, service/cleaning references, all 15 registrations, station instances, authored outlines and queue anchors, navigation binding, and HUD canvas scales/buttons. These serialized checks and C# syntax parsing pass. They do not replace Unity compilation or gameplay verification.

Runtime behavior, navigation reachability, visual seating/animation appearance, device performance and Unity compilation remain unverified. Do not label this version verified playable until the checks above pass. Multiplayer Fast Food is intentionally deferred.

## Four-station and visual acceptance cases (external Unity 6000.0.40f1)

These require external execution. Static source and YAML checks cannot establish navigation or visual playability.

1. Open Lobby2 without entering Play mode. Run **Dine In > Fast Food > Validate Lobby2 Structure** and **Validate Lobby2 Navigation**. The latter now checks both cashier approaches, all station queues/overflow points and the outside-left entrance route. Re-bake Lobby2 navigation with the existing menu command if any route is missing; do not replace another restaurant's navigation asset.
2. Start a stocked day. Follow green, pink and blue groups from the outside-left spawn through the entrance. Verify door clearance, formation spacing and FIFO at all four stations. No group should appear directly inside the dining room.
3. Force each kiosk's Pay Here Chance to 1. Confirm one stock reservation, one payment, one freed queue slot and one kitchen result. Repeat with chance 0: follow the same number into either counter; cancel/reopen cash and card payment; verify no second order review, stock reservation or charge.
4. Serve the second counter manually while the cashier serves the first, then reverse. Competing clicks cannot steal an owned customer. Fire/unassign the cashier and verify manager service still works at both counters; reassign without enabling a waiter/receptionist.
5. On booth, long table and round table, observe one-, two- and four-person groups. Check food rests flat and centred, clear of diners. Watch a member leave to collect: the number remains over its table. Repeat manager delivery with self-pickup chance temporarily set to 0. Verify tray pickup controls disappear during eating.
6. Click clean, occupied and dirty tables on mouse and touch. Verify accepted outlines, existing manager cleaning options, tray pickup and automatic sink disposal. Neither a table selection nor kiosk information tap should create an unrelated service claim.
7. Exercise takeaway automatic collection and manager delivery, blocked pickup/return routes, stockout, payment cancellation and duplicate clicks. Confirm failed unserved paid orders refund once and output/claims disappear.
8. Complete two consecutive days and a fresh run. Verify every queue starts empty, assigned staff remain correct, no stale register/card panel or permanently held item remains, and restaurant openings use only the four Fast Food roles.
9. Regression-test Lobby1 manually: waiter/receptionist staffing, order review, tray placement, bill/cash/card, restock and cleaning retain their prior controls. Multiplayer gameplay has not been extended to Lobby2.

**Defaults/settings:** kiosk order 5 seconds; kiosk immediate payment 70%; counter order/payment deadlines 60/45 seconds; outside entrance travel deadline 60 seconds per segment. These are serialized Inspector fields. Existing kitchen, restock, cash-change and counter card timings remain the shared implementations.

### Checks performed for the four-station update

- Read-only Roslyn semantic analysis: 545 runtime sources and 55 editor sources, zero errors using the existing Unity reference assemblies. No assemblies were emitted and Unity was not launched.
- `tools/validate_lobby2_authoring.py`: passed scene/prefab references, reciprocal hierarchy links, unique station queues/flows, entrance route, horizontal table tray mounts and existing HUD structure.
- `git diff --check`: passed.
- External navigation, visual alignment, Unity import/compilation and gameplay regressions remain unexecuted. This is an implementation handoff, not a verified-playable result.
