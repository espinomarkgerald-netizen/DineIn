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
- **Fast Food Services / Dining Tables - Editable Service Anchors**: 14 table proxies linked to the imported furniture. Each has a Booth, approach, seats, tray location, table-number anchor, delivery interaction, collider, and an instance of the reusable cleaning UI prefab.
- **Fast Food Services / Counter and Waiting Points**: cashier, pickup and paid waiting anchors.
- **Fast Food Services**: entrance, exit, customer order point, sink interaction/approach, takeaway output, ground click collider, and the dedicated navigation surface.
- **GameManager / LobbyAutonomousService**: existing staff service settings, now authored once to prevent duplicate runtime creation.
- **GameManager / RestaurantManager / KitchenManager**: existing recipe cooking/preparation and output slots.
- Existing staff home/work/trolley points have Lobby2 positions. Existing shared restock/hygiene systems are reused, including their normal runtime customer/item/UI instances.
- `Resources/MenuCatalog.asset` explicitly maps the Fast Food menu to Lobby2.

## Required Unity authoring/verification step

**No Unity launch, compilation, build, navigation bake, or gameplay test was performed by this implementation session.** This follows the user's restriction. C# syntax parsing and serialized-reference checks are not proof of playability.

The previous scene navigation belongs to the old layout. The new dedicated surface intentionally has no generated data checked in. Start Day refuses to open without that data, rather than spawning customers into an invalid navigation layout.

On the external Unity **6000.0.40f1** verification machine:

1. Open Lobby2. Resolve any Unity compiler/import errors first.
2. Run **Dine In > Fast Food > Validate Lobby2 Structure**.
3. Run **Dine In > Fast Food > Bake Lobby2 Navigation**. This creates `Lobby2Navigation.asset`; it does not overwrite Lobby1 navigation assets. Save Lobby2 and include the generated asset and `.meta` in the eventual commit.
4. Run **Validate Lobby2 Navigation**. Inspect anchor alignment against the imported furniture, including both booth aisles and the kitchen. Adjust scene anchors in the Inspector if a route or visual alignment fails, rebake and save.
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
| Casual Dining regression | Lobby1 human seating, ticket/cooking, bill, cash/card, restock and hygiene remain unchanged. |

During each scenario, **Dine In > Fast Food > Check Live Service Invariants** checks duplicate output, payment-before-seating, unique table ownership and counter release. This diagnostic does not simulate button presses or replace the checklist.

## Remaining verification limits

Navigation is not yet baked; runtime behavior, imported-model alignment, animation appearance, device performance and Unity compilation remain unverified. Do not label this version verified playable until the checks above pass. Multiplayer Fast Food is intentionally deferred.
