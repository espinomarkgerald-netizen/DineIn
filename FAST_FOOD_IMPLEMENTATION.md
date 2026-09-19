# Lobby2 Fast Food — implementation and verification

## Scope

Single-player Lobby2. Casual Dining keeps its existing service path. Multiplayer scene, protocol, and result recording are unchanged. Existing imported Fast Food artwork and the user's pre-existing asset changes are retained.

## Implemented flow

1. Customers enter the counter queue. `FastFoodRestaurant` selects dine-in or takeaway using its Inspector probability.
2. The manager uses the existing order review UI, recipes, quantities and stock transaction. Assigned staff can take the order instead.
3. Counter cash/change or the existing unlocked card terminal completes payment once. A cancelled payment can be reopened by selecting the cashier. Staff can complete an unclaimed counter payment.
4. Payment frees the counter immediately. Customers move to authored waiting positions, with separate overflow spacing.
5. Dine-in groups reserve their own compatible, clean, vacant table. The manager does not seat them. Full tables keep the paid order waiting.
6. Cooking begins after payment through `KitchenManager`. Preparation can finish while the group waits for seating; the output is held until its table is ready. No second stock withdrawal or kitchen submission is made on seating.
7. The configurable self-pickup choice sends one customer to collect the tray and return. Otherwise the existing manager/staff tray-delivery controls apply. An unreachable customer pickup falls back to staff delivery.
8. Dine-in customers eat, react, and leave through existing satisfaction/cleanup rules. They do not request a second bill or pay twice. Takeaway customers receive their matching numbered bag while waiting away from the counter.
9. Unserved paid orders that fail receive a refund once. Prepared-food delivery has an editable timeout; seat waiting and kitchen cleaning do not spend that timeout. Departure removes abandoned output. Staff and manager retain the existing dirty-tray disposal and hygiene mechanics.

## Editable hierarchy

Open `Assets/_Project/Scenes/RoleBased/Lobby2.unity` outside Play mode:

- **Fast Food Services / FastFoodRestaurant**: customer mix, self-pickup probability, waiting spacing, pickup timeout, ready-food timeout, queue/kitchen/payment references.
- **Fast Food Services / Dining Tables - Editable Service Anchors**: 15 prefab instances: eight four-seat booths, five two-seat long tables and two four-seat round tables (50 seats). The group keeps its original name to preserve the existing tables' hygiene identities. The second round table was visible art without a service proxy and is now registered too. Window bar stools remain decorative.
- **Fast Food Services / Counter and Waiting Points**: customer pickup and paid waiting anchors.
- **Fast Food Services / Fast Food Cashier**: counter art, click collider, payment interaction, hygiene marker and Cashier Approach, in one prefab. Its restaurant reference is a scene override.
- **Fast Food Services / Fast Food Sink**: both existing sink meshes, click collider, wash interaction, hygiene marker and Sink Approach, in one prefab.
- **Fast Food Services**: entrance, exit, customer order point, takeaway output and ground click collider. Its Navigation Surface field explicitly references the existing active surface on Fast Food Revamp; the empty duplicate surface is disabled.
- **GameManager / LobbyAutonomousService**: existing staff service settings, now authored once to prevent duplicate runtime creation.
- **GameManager / RestaurantManager / KitchenManager**: existing recipe cooking/preparation and output slots.
- Existing staff home/work/trolley points have Lobby2 positions. Existing shared restock/hygiene systems are reused, including their normal runtime customer/item/UI instances.
- `Resources/MenuCatalog.asset` explicitly maps the Fast Food menu to Lobby2.

### Reusable prefabs and editing

`Assets/_Project/Restaurant/Prefabs/FastFood/` contains **Fast Food Booth**, **Fast Food Long Table**, **Fast Food Round Table**, **Fast Food Cashier** and **Fast Food Sink**. Open them in Prefab Mode outside Play mode. Meshes and materials reference the original model assets. Scene instances retain their original mesh/material references and world transforms.

Each table owns `Furniture`, `ApproachPoint`, `FacingPoint`, `SeatPoint_01...`, `TableFoodSpawn`, `TableNumberAnchor` and `Table Cleaning`. Edit these transforms directly. `Booth.seats` sets capacity; keep it matched to usable visible seats. FacingPoint controls customer facing independently of food placement. The right-hand booth row overrides ApproachPoint toward its outer aisle. Long-table points follow their offset tabletop and integrated stools. One `BoothDeliverInteractable` handles delivery, and one `HygieneSurface` tracks dining dirt. There are no table-side bill/money or legacy puddle spawners.

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

## External regression checklist

Use a disposable Fast Food save. Do not treat a staff-completed shift as proof that human controls work.

| Scenario | Required result |
|---|---|
| Human dine-in order | Review correct quantities; stock deducted once; cash/change completes once; counter advances; customers choose seats; one tray appears. |
| Card/cancel/retry | Unlock card equipment; cancel and reopen payment; confirm once; no stale claim, duplicate earnings or stuck hands. |
| Customer pickup | Set self-pickup probability to 1; one member collects; the rest stay seated; tray lands on the matching table; eating begins. |
| Human/staff delivery | Set self-pickup probability to 0; manager completes pickup/delivery; repeat with assigned staff. Wrong table cannot consume the tray. |
| Takeaway | Set takeaway probability to 1; pay; next customer can order; collect matching bag; deliver to numbered waiting group; group exits once. |
| Mixed/full tables | Restore probabilities; fill every compatible table; paid groups wait without stealing occupied/dirty seats or consuming stock again. Clean/free a table; oldest compatible group seats. |
| Kitchen/output pressure | Fill output slots; kitchen queues or safely fails/refunds; never stacks two outputs in one slot. Pause kitchen for cleaning while orders are queued. |
| Recovery | Interrupt customer pickup route, cancel review/payment, let ready food time out, close the day with waiting customers. No permanent reservation, orphaned held item or double refund. |
| Full loop | Manager disposes dirty trays at the sink, cleans tables, restocks dry/freezer items, reads newspaper and starts two consecutive days. Reopen the save and verify restaurant isolation. |
| Table prefabs | Each of the 15 tables seats its full capacity without overlapping customers; facing, tray placement and number position match the model. Leaving vacates all seats; occupied, dirty or tray-blocked tables cannot be reserved. |
| Table cleaning | Clean each furniture type through both manager hold-to-clean and staff requests. Dirt/shine follows the same table; no duplicate prompt or missing-script error. |
| HUD/stations | Camera focuses the manager; computer opens through its button and world click. Cashier reopens payment; sink disposes the tray. All controls return after management, pause and restock panels close. |
| Casual Dining regression | Lobby1 human seating, ticket/cooking, bill, cash/card, restock and hygiene remain unchanged. |

During each scenario, **Dine In > Fast Food > Check Live Service Invariants** checks duplicate output, payment-before-seating, unique table ownership and counter release. This diagnostic does not simulate button presses or replace the checklist.

## Remaining verification limits

`python tools/validate_lobby2_authoring.py` performs dependency-free, read-only checks of prefab ownership, unique seats, service/cleaning references, all 15 registrations, station instances, navigation binding, and HUD canvas scales/buttons. These serialized checks and C# syntax parsing pass. They do not replace Unity compilation or gameplay verification.

Runtime behavior, navigation reachability, visual seating/animation appearance, device performance and Unity compilation remain unverified. Do not label this version verified playable until the checks above pass. Multiplayer Fast Food is intentionally deferred.
