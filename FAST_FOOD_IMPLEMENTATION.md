# Lobby2 Fast Food — implementation and verification

Updated: 20 September 2026. This is the current implementation reference for the approved single-player master plan. It supersedes the older Fast Food notes in this file.

## Status

The code, scene references and prefab authoring described below are implemented. Runtime and Editor assemblies compile offline against Unity 6000.0.40f1 with **zero compiler errors**. The serialized asset checks and nine isolated persistence assertions pass.

**Playable end-to-end is not yet verified.** Unity was not launched on this machine. Existing navigation data was preserved, not rebaked. Actual scene import, movement clearance, tray/hand alignment, animations, cloud operations and a complete human-controlled day need the external Unity verification below. Static checks do not prove those visual or runtime outcomes.

## Final gameplay rules

- Lobby2 only, single player. Two independent cashier stations and two kiosks.
- Cashier, chef, barista and busser are the required roles. One assigned cashier serves both counters in rotation. Receptionist and waiter are not used.
- Counter work defaults to 2 seconds ordering plus 2 seconds payment, excluding walking; existing staff speed modifiers apply. Kiosk ordering defaults to 2 seconds with automatic payment. No kiosk-to-counter payment transfer.
- A stable representative orders and collects. Companions have distinct authored waiting positions. Groups reserve a station before approaching it.
- Each station has one front position and two queue positions. Two outdoor pockets absorb overflow; arrivals pause at capacity. Six paid waiting positions prevent checkout from creating an unbounded crowd. Three exclusive customer pickup positions prevent collectors sharing one destination.
- Food preparation begins at payment, while dine-in customers find their own suitable table. Prepared food waits for seating before appearing for pickup. Customers collect every dine-in meal themselves.
- Existing table numbering stays at the table while the representative collects. No manager food-delivery workaround is required.
- Customers carry trays using a customer carry frame and tray-owned left/right grips. Hand pose blends in/out over 0.15 seconds; tabletop scale is restored after carrying.
- Takeaway remains enabled at the existing 30% default. Only its representative approaches the ready bag. The rest of the group waits, then leaves together.
- Existing stockout policy remains: some customers agree to wait; others leave unsatisfied. Existing stockout dialogue/mood/HUD reporting and slower arrivals are reused.
- At closing, arrivals stop immediately. Admitted customers have up to 120 gameplay seconds to finish, followed by 20 seconds to exit. Unserved paid orders follow the existing one-time refund path.
- Preparation changes save normally. Starting service creates a rollback checkpoint. Leaving during service restores preparation; active customers/tasks are not resumed. A completed report is saved before advancing the day.

## Ownership and reuse

| Area | Implementation owner / scope |
|---|---|
| Day, clock, spawning, result screen | Existing `GameDayManager` and `GameFlowManager`; Fast Food opening guards, closing grace and saved report added |
| Arrival and station selection | `FastFoodRestaurant`, `GroupSpawner`, existing queue managers; bounded reservations include en-route groups |
| Queue/payment | Four authored `FastFoodServiceStation` instances, each with its own `TakeoutQueueManager` and `TakeoutFlowManager`; existing register/card UI and settlement guard |
| Stock and food | Existing `LobbyStockBridge`, `KitchenManager`, recipe catalogue, food/tray prefabs; stock consumption stays in the existing confirmation path |
| Group behaviour | `CustomerGroup.FastFood.cs` plus narrow Fast Food branches in `CustomerGroup`; one representative, self-seating, pickup, remakes and closing |
| Carry pose | `CustomerAgent`, `AlienProceduralAnimation`, pure authoring component `FoodTrayCarryPose`; no separate tray inventory |
| Tables and outlines | Existing `Booth` contracts and editable `FastFoodTable` prefabs; keep complete-mesh outline authoring |
| Staff | Existing `LobbyAutonomousService`, `AutonomousStaffBot`, `KitchenWorkerBot`, hands, sink and trolley systems |
| Restock | Existing `RestockFlowCoordinator`, `RestockOrderManager`, `InventoryManager`, truck/hotbar and shared `RestockScene` |
| Shelf presentation | `FastFoodShelfPreview` reads stored-container/batch data; `StoredBoxPreview` is art/UI only, without colliders, draggable stock or inventory identity |
| Management/HUD | Existing management canvas and full LobbyHUD; room computer bindings, idle Task panel station summary, existing kitchen/order indicators |
| Persistence | Existing `GameSaveManager`, `CampaignSaveStore`, `CampaignCloudSync`, objectives, finance and roster systems; restaurant-specific files, not a replacement save service |

## Authoring outside Play Mode

Open `Assets/_Project/Scenes/RoleBased/Lobby2.unity`.

### Fast Food Services

The `FastFoodRestaurant` component owns editable service station, table, entrance, paid waiting, outdoor waiting, pickup, exit, kitchen and navigation references. Timings and takeaway chance are Inspector fields. A missing required service reference blocks opening with feedback.

- **Outdoor Waiting - Editable Group Pockets:** two outside positions.
- **Counter and Waiting Points:** existing paid waiting and service points, plus three customer pickup positions.
- Each counter/kiosk has its own order point, two queue points, staff approach and three **Companion** children beneath its order point. Move the point and companion hierarchy together.
- Entrance route starts outside on the left and passes through the left entrance. Customer spawning stays out on the map.
- `FastFoodLobbyAuthoring` explicitly references the office computer, two shelf entrances, sink, cashier home and busser home.

Do not delete a queue/flow or duplicate its reference into another station. Run structural and navigation validation after moving points. The existing NavMesh asset remaining assigned does not establish that a moved point is reachable.

### Tables and furniture

Open prefabs in `Assets/_Project/Restaurant/Prefabs/FastFood/` in Prefab Mode: **Fast Food Booth**, **Fast Food Long Table**, **Fast Food Round Table**, cashier, kiosk and sink assets.

Table hierarchy owns furniture, approach point, facing point, seat points, `TableFoodSpawn`, `TableNumberAnchor` and cleaning UI. Scene registration contains eight booths, five long tables and two round tables. Use the `FastFoodTable` Inspector shortcuts and validation to edit anchors. Preserve original imported meshes/materials and the full-mesh outline repair. The model needs its current Read/Write import setting for QuickOutline's combined submesh handling.

`TableFoodSpawn` is oriented for the tray model's local Z to face upward. Do not reset it to an identity quaternion merely because the tabletop is horizontal.

### Kitchen output and customer tray

The four dine-in output points remain assigned on `KitchenManager`. Their orientations now match the tray's authored axis, and their horizontal spacing is greater than the tray width. Takeaway output is separate. Spawn-slot timeout is 60 seconds in Lobby2.

Open `Restaurant/Assets/Level1/GameObjects/RestaurantObjects/Customers/Food Tray.prefab`:

- `FoodTrayCarryPose` references **Customer Carry Origin**, **Customer Left Grip**, **Customer Right Grip**.
- `Carry World Scale` defaults to 160 while carried. Counter/table scale is restored on release.
- Customer prefabs have an editable `trayCarryAnchor` and pose blend duration. The previous customer-local grip references remain serialized but hidden for compatibility; active hand targets come from the tray itself.
- Green, blue, pink and generic customer prefabs all have carry frames. Verify each body/hand configuration visually; source inspection cannot establish clipping quality.

### Storage and management

The dry/freezer shelf-bank objects own clickable `RestockStockRoomEntrance` components connected to the shared RestockScene. Each bank owns twelve Inspector-editable preview slots under **Stored Stock Preview - Read Only**. Empty slots start inactive. Move slots in the hierarchy to fit a changed shelf model. Their nested art comes from `RestockRoom/Prefabs/StoredBoxPreview.prefab`.

Physical containers remain exclusively owned by RestockScene and the existing ledger. Preview visibility uses actual remaining batch quantities and actual stored room. Cached Restock roots are refreshed when closing the stock room so newly restored boxes are hidden too; this addresses the boxes appearing over dining tables. Returning to the restaurant restores the existing input/camera/time state. Failed loading/activation returns to the restaurant with feedback.

**Room Management Computer** is the active physical computer. **Legacy Lobby Computer (Unused)** stays inactive. The full HUD remains editable at `Resources/UI/LobbyHUD.prefab`; camera, computer, newspaper and task buttons remain present.

## Preparation and opening

1. Load the selected restaurant profile before staff evaluate assignments.
2. Use the office computer to select menu/prices, assign the four required roles, inspect equipment, order stock and review objectives.
3. Receive deliveries using the existing truck/hotbar flow; enter dry/freezer storage, place boxes and return.
4. Opening validates service references, the assigned navigation data, required staff and successful save loading. Existing readiness/stock warnings remain in use.
5. Save the service-start checkpoint before enabling Fast Food spawning. A failed checkpoint write leaves the restaurant closed and reports the failure.

## Recovery and exactly-once boundaries

- Station reservations count incoming groups, preventing travel-time overbooking. Outdoor FIFO customers retain priority over new arrivals.
- Missing/disabled stations cause a bounded customer failure; destroyed queue members cannot permanently block the front.
- Payment requires the correct station front, arrival at its order point, confirmed order, payment phase and available paid waiting space. Reservation happens before finance callbacks to prevent duplicate payment.
- Cooking submission reuses KitchenManager acceptance checks. Remakes receive a new order identity without a second payment or a waiter ticket.
- Seat wait is bounded (120 seconds). Pickup travel and ready-food waits are bounded. Kitchen cleaning pauses the relevant wait.
- Pickup positions and tray ownership release on success, cancellation, failure or destroyed customers. Failed carries restore the original output pose/queue when the order is still valid.
- Dine-in delivery buttons are hidden/disabled for manager pickup in Fast Food; dirty-tray cleanup remains available.
- Table/tray cleaning reuses task claims. Fast Food automated table cleaning also has a deadline and releases work state afterward.
- Day reset stops service coroutines, staff jobs and held-item activity, dismisses stale payment/review UI and clears queues, customers, table reservations and kitchen objects.

## Save/load details

Files live under Unity's existing `Application.persistentDataPath`:

| Profile | Main save | Service-start checkpoint | PlayFab file |
|---|---|---|---|
| Casual Dining | `dinein_save.json` (existing configurable name) | `dinein_save_day_start.json` | `CampaignSave_v1.json` |
| Fast Food | `dinein_fastfood_save.json` | `dinein_fastfood_save_day_start.json` | `FastFoodCampaignSave_v1.json` |

The selected profile persists through additive RestockScene/menu transitions. Profile changes reload stock, equipment, money, staff, menu and progression; ingredient unlocks are rebuilt from saved recipes. Tutorial graduation always writes the Casual Dining profile. Tutorial/multiplayer persistence boundaries remain enforced.

Schema 3 remains in place, with optional Fast Food objective, finance and report fields. Fast Food report reload restores completed-day status without charging payroll or awarding objectives again. Beginning the next day saves its preparation state before reloading Lobby2.

Main-save/checkpoint pair changes use the existing recoverable journal. Rollback retires its checkpoint after restoration so new preparation changes remain durable. Final report commit retires the checkpoint with the final save. An invalid journal or unreadable save is preserved and blocks service rather than silently overwriting progress.

Cloud operations capture the restaurant/file they started with; stale callbacks cannot install progress into a different profile. Confirmed currency exchange credits also retain the originating profile and receipt deduplication. Cloud/account behavior still needs live integration testing.

## Validation performed here

- Offline `Assembly-CSharp` and `Assembly-CSharp-Editor` compile using installed Unity 6000.0.40f1 references: zero errors. Existing warnings remain.
- `python tools/validate_lobby2_authoring.py`: passes scene/prefab file IDs, references, reciprocal hierarchy links/cycles, SceneRoots ordering, table registrations, separate station ownership, queue/companion/pickup/outside positions, tray axis/spacing, art-only previews, four customer carry rigs and HUD bindings.
- `python tools/verify_fastfood_persistence.py --unity "D:/Unity/Unity/Hub/Editor/6000.0.40f1/Editor"`: nine assertions pass against the real `CampaignSaveStore` implementation with isolated temporary files. Covers profile separation, checkpoint recovery, late credits, duplicate credits, invalid-journal preservation, balance switching and tutorial guards. JSON/scene/account adapters are used; this is not a Unity lifecycle or PlayFab integration test.
- Editor service regression runner updated for counter arrival, bounded waiting capacity, closing admissions, unique pickup slots and payment reservation. It compiles but was **not executed** here.
- No local Unity launch, scene import test, NavMesh bake or human playthrough was performed.

## Required external Unity acceptance pass

Use Unity **6000.0.40f1**, with a disposable copy of the saves and existing player backups preserved.

1. Open Lobby2 without crashes/import errors. Run **Dine In > Fast Food > Validate Lobby2 Structure**.
2. Run **Validate Lobby2 Navigation**, including outside pockets, companion points, pickup points, table aisles, office, shelves, sink and staff homes. If required, use **Bake Lobby2 Navigation**, save and validate again on the external machine.
3. In an empty Play Mode scene, run **Run Service Regressions (Empty Play Mode Scene)**. The command creates and removes its own temporary Lobby2 fixture; never run inside the restaurant.
4. In Lobby2, test all fifteen tables and each station: complete furniture outline, accepted interaction, no movement from presentation-only selection, correct clean/self-clean/staff-clean prompts.
5. Restock from both shelves: truck collection, hotbar, place/move/discard boxes, dry/freezer selection and repeated return. Verify stock counts and expiry labels in the real stock room. Only bank previews may remain visible in Lobby2; nothing floats over tables.
6. Open service with each missing role in turn, then assign all four. Confirm only cashier/chef/barista/busser operate, with one cashier alternating the two counters. Confirm manager assistance still uses normal order/payment/cleanup interactions.
7. Admit mixed group sizes and all alien variants. Fill every station and outdoor pocket. Confirm distinct destinations, left-door entrance, FIFO promotion, no spawn/despawn flicker at capacity and clear kiosk progress.
8. Exercise cash, card, kiosk payment, review cancellation and repeated interaction. Confirm one charge, one stock deduction and one kitchen submission per accepted order.
9. Observe cooking while a paid dine-in group waits for a table. Fill paid waiting positions; payment must pause instead of placing extra groups on the same spot.
10. Collect simultaneous trays and takeaway bags. Confirm separate pickup spaces, one representative moving, table number staying at its table, level waist-height trays and both hands meeting the grips. Inspect at all three table types and for green/blue/pink/generic customers.
11. Check eating, wrong/burnt meal complaint and remake, one-time refund for an unserved departure, satisfaction bars and departure paths. No second charge or waiter ticket for a prepaid remake.
12. Check busser pickup, surface cleaning, sink/trolley return, lobby cleaning and task release. Remove/reassign a staff role during a job and verify no locked table or permanent staff task.
13. Test unavailable station, blocked path, no stock, no free seat, occupied output slots, destroyed customer/tray, paused kitchen and failed stock-room load. All must recover within configured timeouts and restore input.
14. Reach closing with unpaid, cooking, collecting, eating and takeaway groups. Confirm admissions stop; service/exit grace runs; remaining objects/claims clear; result totals/refunds/payroll/objectives settle once.
15. Reload during preparation, quit mid-service, then change preparation and reload again. Preparation must persist after rollback. Reload a completed report twice: balances/objectives/payroll must remain unchanged. Advance/reload the next day and test endless/recovery transitions.
16. Switch Lobby1 ↔ Lobby2 and enter/leave RestockScene. Confirm independent money, stock, staff and progression. Regression-test the shared Restock hide/show, Casual Dining unlock restoration, tutorial graduation and cloud/currency profile switch callbacks. Multiplayer rules are unchanged, but shared persistence/Restock boundaries still warrant a smoke check.

**Release gate:** all external cases pass with no missing-reference errors, overlapping/trapped customers, floating/sideways trays, leaked boxes, stuck claims or save regression. Until then, describe this as implemented and statically checked, not fully playtested.

## Scope boundaries

Do not rebuild inventory, restaurant management screens, finance, customer mood/leave bubbles, hygiene shaders, full seating outline logic or multiplayer Fast Food. Existing project art/import changes unrelated to this implementation are preserved. No commit or push was performed by this task.
