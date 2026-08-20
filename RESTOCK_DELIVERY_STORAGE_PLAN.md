# Dine In — Restock Delivery, Hotbar, and Storage Plan

## Purpose

Update the existing restock feature without replacing the work already completed in `RestockScene`.

Target gameplay loop:

> Computer cart → checkout → delivery delay → arrival warning → hold at truck → temporary restock hotbar → enter stock room → drag boxes onto shelves → stock becomes usable.

This document is a plan only. It does not authorize implementation by itself.

---

## 1. Current Project Status

### Implemented foundation

- `RestockScene` exists and already contains a Dry Storage Room and Walk-In Freezer.
- Cardboard-box and crate prefabs exist.
- The box/crate prefabs can be reused as visual templates whose displayed name and image change per ingredient.
- Dry-room and freezer shelf prefabs exist.
- Boxes can be dragged with a mouse or touchscreen.
- A tap is distinguished from a drag.
- Shelf cells snap boxes into place.
- Occupied cells are tracked while the scene is running.
- A placement ghost shows valid and invalid positions.
- Invalid drops return the box to its previous position.
- An existing placed box can be moved to another free cell.
- Tapping a box can open its interaction UI.

### Partially implemented

- A scene object named `Hopbar` already exists, but it is only a visual/container foundation. It does not yet hold, stack, display, or place delivered items.
- The management computer already has a Restock app, but purchases are immediate and add units directly to `InventoryManager`.
- The project already has a circular hold interaction used for cleaning.
- The project already has an iris transition component and scene-loading systems.
- The game already has notification/warning UI that can be reused for delivery arrival.

### Not implemented yet

- Shopping cart and checkout review.
- Pending restock orders and delivery states.
- Delivery delay and truck arrival.
- Hold-to-collect interaction on the truck.
- Functional restock hotbar and item stacking.
- A data link between hotbar items, physical boxes, shelf cells, and inventory.
- Lobby1 stock-room entrance interaction and arrival point.
- Restock scene session context and return-to-Lobby1 flow.
- Pausing only the restaurant clock while the player is in `RestockScene`.
- Saved shelf occupancy and box positions.
- Dry/frozen capacity validation.
- Wrong-storage warnings and spoilage.
- Customer-demand recommendations.

---

## 2. Locked Player Experience

### A. Order from the management computer

1. The player interacts with the computer.
2. The player opens the Restock app.
3. The player chooses box quantities using a shopping cart.
4. The player presses **Checkout**.
5. A review panel shows the items, box count, total cost, and storage-capacity impact.
6. Money is spent only after **Order Now** is confirmed.
7. Checkout creates a delivery order. It must not immediately add usable recipe stock.

### B. Wait for and collect the delivery

1. The order moves through a short configurable delivery delay.
2. When it arrives, show:

   > **Your order has arrived. Go to the truck and get your order.**

3. The player clicks/taps the truck, and the player walks to its interaction point.
4. At the truck, show **Hold to Get Orders** with the same circular progress behavior used by Busser cleaning.
5. Releasing early cancels and resets the circle without collecting anything.
6. Completing the hold transfers the delivered stacks into the restock hotbar exactly once.
7. The truck cannot be collected repeatedly to duplicate stock.

### C. Enter the stock room

1. The restock hotbar becomes visible after a delivery has been collected.
2. The player clicks/taps the stock-room shelf, door, or entrance interactable in Lobby1.
3. The player always walks to a dedicated arrival/stand point.
4. After arrival, the iris closes and `RestockScene` loads.
5. The iris opens in the stock room.
6. The session remembers the originating restaurant, return location, delivery, hotbar contents, and saved shelf layout.

### D. Store the boxes

1. Identical delivered items occupy one hotbar slot and show a quantity such as `Chicken Box ×5`.
2. The player drags an item from the hotbar toward a shelf.
3. Dragging creates or activates a physical box using the existing `DraggableStorageBox` behavior.
4. A valid shelf drop snaps the box into a free grid cell.
5. Only after a valid placement does the hotbar stack decrease.
6. The placed container becomes stored inventory and its units become usable by recipes.
7. An invalid or cancelled drop returns the item to the hotbar and does not change inventory.
8. Existing shelf boxes remain movable with the current world drag-and-drop controls.

### E. Return to the restaurant

1. The player can see **Orders Remaining** at all times.
2. The player may return to Lobby1 to switch between dry and frozen storage areas.
3. Hotbar contents persist across room changes and scene transitions.
4. The iris closes, Lobby1 loads, and the player returns to the saved entrance point.
5. The restaurant clock resumes exactly once after Lobby1 is ready.
6. When every delivered box has been stored, the hotbar hides and the order becomes complete.

---

## 3. Restocking Sub-State and Timer Rules

Restocking is a temporary gameplay sub-state, not a second inventory mode.

- The hotbar is visible only while collected delivery items remain.
- The system may be entered during preparation or during an active restaurant day.
- While `RestockScene` is open, the Lobby1 day clock, customer spawning, patience, cooking, and other restaurant simulation must not advance.
- RestockScene controls and animations must continue to work.
- Do not implement this with only `Time.timeScale = 0`, because that can also freeze dragging, transitions, and UI behavior.
- Add an explicit restaurant-simulation pause reason, such as `RestockScene`, and restore the prior phase when returning.
- Scene entry/exit must be guarded so repeated clicks cannot load twice or resume the clock twice.

If the player tries to start the day while collected boxes remain, show a confirmation or block start until the delivery is stored. The safest first version is to require the hotbar to be empty before **Start Shift**.

---

## 4. Inventory Source of Truth

Use one stock ledger, but track where each quantity currently exists.

```text
Ordered / In Transit
    Reserved capacity, not usable by recipes

Arrived at Truck
    Reserved capacity, not usable by recipes

Collected in Restock Hotbar
    Reserved capacity, not usable by recipes

Placed on Shelf
    Stored and usable by recipes

Spoiled / Discarded
    Not usable
```

The computer, truck, hotbar, RestockScene, and kitchen must not each maintain unrelated stock counts.

Important change to the current behavior:

- Checkout must not call `InventoryManager.AddStock` immediately.
- Truck collection must not call `InventoryManager.AddStock` immediately.
- A successful shelf placement is the point at which the container's units become usable stock.
- Moving an already stored box must not add its units a second time.
- Throwing away or spoiling a stored box removes its remaining usable units.

---

## 5. Required Data

Extend existing item data instead of hard-coding food names.

```text
Stock Container Definition
- Item ID
- Display name
- Icon
- World box prefab
- Container label style
- Units per box
- Box cost
- Required storage type: Dry or Frozen
- Shelf life in game days
- Wrong-storage spoilage multiplier
- Grid footprint (1x1 for the first version)
```

```text
Stored Container Instance
- Unique container ID
- Item ID
- Remaining units
- Freshness
- Restaurant ID
- Room ID
- Shelf ID
- Grid coordinates
```

```text
Restock Order
- Unique order ID
- Ordered stacks
- Total cost
- Created time
- Arrival time
- Current state
```

Use stable IDs rather than scene object names for saved data.

### Reusable container presentation

Do not create a separate prefab for every ingredient unless its physical container truly needs a different model.

- Keep the existing cardboard-box and crate prefabs as reusable bases.
- Add editable label references to the prefab: ingredient name text and ingredient image.
- When a container is created, bind those fields from its `Stock Container Definition`.
- The same item icon is used consistently in the computer card, checkout, notification details, restock hotbar, world container label, and stored-item panel.
- The prefab must remain editable in the Unity Inspector; runtime code only supplies the item-specific content.
- If an item has no icon, show a deliberate generic ingredient placeholder instead of an empty or broken image.
- Preserve image aspect ratio so uploaded art is never stretched.
- Use a readable fallback name and automatically fit long ingredient names within the label area.

Example:

```text
CardboardBox prefab + Buns item data
    Label text: BUNS
    Label image: buns icon

Crate prefab + Tomato item data
    Label text: TOMATO
    Label image: tomato icon
```

### Initial content scope

- The first implemented catalog is **Casual Dining** only.
- Uploaded Casual Dining menu and ingredient icons should be reused whenever they represent the same ingredient.
- One ingredient definition should reference one shared icon; recipes and containers should not contain duplicate copies of that art.
- Menu-dish icons and raw-ingredient icons remain distinct data fields, even when sourced from the same uploaded icon sheet or folder.
- Fast Food and Fine Dining assets may be imported and organized, but their inventory catalogs are deferred until the Casual Dining flow is complete and validated.

---

## 6. Order State Machine

```text
Draft Cart
    ↓ checkout confirmation
Ordered
    ↓ delivery timer
In Transit
    ↓ arrival
Arrived at Truck
    ↓ completed hold interaction
Collected
    ↓ one or more valid shelf placements
Partially Stored
    ↓ all stacks placed
Stored / Complete
```

Every transition must be idempotent. Repeating an input, reloading a scene, or restoring a save must not duplicate money, boxes, hotbar stacks, or inventory units.

---

## 7. Shopping Cart and Checkout

The Restock computer app should show vertical, mobile-friendly item cards.

Each card should include:

- Item icon and name.
- Units per box.
- Current usable stock.
- Pending/delivered stock.
- Recommended amount.
- Box price.
- Minus button, requested quantity, and plus button.

For the first pass, populate these cards only from the Casual Dining ingredient catalog.

Checkout review should show:

- Every ordered item and quantity.
- Total boxes.
- Total cost.
- Dry capacity after delivery.
- Frozen capacity after delivery.
- **Back** and **Order Now** buttons.

The cart may change freely. Only **Order Now** spends restaurant money and creates the order.

---

## 8. Physical Capacity

Existing `ShelfGrid` dimensions define the physical slot count. Capacity is calculated separately for dry and frozen storage.

```text
Available Capacity =
Physical Shelf Cells
- Stored Containers
- Ordered/In-Transit Containers
- Arrived Containers
- Collected Hotbar Containers
```

Rules:

- Pending containers reserve space as soon as checkout succeeds.
- The computer must explain the exact remaining capacity.
- Do not silently reduce the cart quantity.
- Moving an existing box does not consume additional capacity.
- Discarding a box frees one cell.
- The first version keeps every container at `1x1`.

---

## 9. Hotbar Behavior

- Use the existing `Hopbar` scene object as the editable visual foundation, but name the feature **Restock Hotbar** in code and player-facing text.
- Same item IDs stack in one slot.
- Quantity is the number of physical containers, not ingredient portions.
- Large touch targets are required for Android.
- Hotbar slots support pointer/touch drag into the 3D scene.
- A slot remains visible until its quantity reaches zero.
- The hotbar persists between Lobby1 and RestockScene.
- It is not used for food trays, bills, money, tools, or ordinary player tasks.
- It is hidden when no collected delivery containers remain.

Placement transaction:

```text
Begin drag
    Do not decrement the stack

Valid shelf drop
    Create/bind stored container
    Mark shelf cell occupied
    Add usable units
    Decrement hotbar stack
    Save transaction

Invalid/cancelled drop
    Destroy/cancel preview
    Keep the complete hotbar stack
```

---

## 10. Lobby1 Interactions

### Truck

- Use a simple cube as the delivery truck placeholder for the first implementation.
- Give the placeholder a clear temporary material/color, collider, interaction target, player stand point, and optional arrival marker.
- Keep truck gameplay logic in reusable components rather than embedding it in the cube, so the cube can later be replaced by a finished truck prefab without rewriting delivery behavior.
- Click/tap target must be comfortably sized on mobile.
- The player routes to a truck stand point before the hold UI activates.
- Prompt is available only for an arrived, uncollected order.
- Circular hold progress reuses the Busser clean interaction style.
- The hold should use the gameplay interaction routing so a valid click always creates the movement task.

### Stock-room entrance

- Use one clear Lobby1 interactable for the stock room.
- Clicking/tapping routes the player to a stand point.
- Revalidate the interaction after arrival.
- Start the iris transition only after successful arrival.
- Prevent duplicate scene-load requests.

---

## Placeholder Asset Policy

Placeholder visuals are acceptable for the MVP. Gameplay correctness, interaction reliability, and data flow come before final art.

- The truck may be a cube.
- New entrance markers, stand points, delivery markers, hotbar slots, notification icons, box labels, warning panels, and other unfinished objects may use simple placeholder prefabs.
- Reuse existing project prefabs and UI assets when they already suit the purpose.
- Clearly name placeholders with a `Placeholder` suffix so they are easy to find and replace.
- Keep placeholder dimensions close to the expected final asset, especially colliders, navigation clearance, world-space UI position, and mobile interaction areas.
- Do not make core logic depend on a placeholder object's mesh, material, hierarchy name, or exact dimensions.
- Put behavior on reusable scripts/components and supply visual prefabs through editable Inspector references.
- Missing final art must never prevent testing the complete computer → delivery → truck → hotbar → shelf flow.
- Replacing a placeholder later must not change order counts, saved IDs, interaction ownership, or inventory behavior.

---

## 11. Reusable RestockScene

Use one reusable scene for all restaurants and both storage types.

The session context supplies:

```text
- Origin scene
- Origin return point
- Restaurant ID
- Active room: Dry or Frozen
- Restaurant storage configuration
- Stored container records
- Collected hotbar stacks
- Active order IDs
- Whether preparation or service was paused
```

The existing Dry Storage and Walk-In Freezer areas can remain in the same scene. Activate the appropriate room/camera/spawn point based on session context, or allow switching between the two rooms inside the scene if that produces a smoother experience.

---

## 12. Save and Rollback Rules

Save enough state to restore safely:

- Restock order state and arrival time.
- Whether the truck order was collected.
- Hotbar stacks.
- Stored container IDs, remaining units, freshness, shelf IDs, and grid cells.
- The committed usable inventory total.

Transactions must be atomic: a box cannot exist in both the hotbar and a shelf after a crash or scene reload.

Match the game's day-checkpoint rule:

- Finishing a day commits that day's money, purchases, deliveries, and shelf changes.
- Leaving an unfinished day for the game menu restores the day-start checkpoint.
- Money spent, boxes ordered, boxes collected, and shelf changes made during the abandoned day are rolled back together.

---

## 13. Wrong Storage and Spoilage — Later Phase

These systems come after the complete delivery loop is stable.

- Items define `RequiredStorageType` as data.
- Shelves define `StorageType` as data.
- Wrong storage is allowed but shows a clear warning.
- Correct storage uses the normal spoilage rate.
- Wrong storage uses a configurable multiplier.
- Moving a container never resets freshness.
- Spoiled stock cannot be consumed by recipes.
- Throwing away a container frees its shelf cell and removes its remaining units.

Suggested first balance target:

```text
Correct storage shelf life: about 7 game days
Wrong-storage multiplier: about 4x
```

Both values remain editable.

---

## 14. Forecasting and Advice — Later Phase

The computer may recommend stock using the existing day/customer scaling:

```text
Expected customers
× average ingredient usage per customer
= recommended stock
```

Advice should inform, not force:

- Low stock.
- Enough stock.
- Overstock/spoilage risk.

This phase should not block the MVP delivery and storage loop.

---

## Restock UI and Count Reliability Guardrails

The restock implementation must treat UI values as views of authoritative data, never as separate counters. Avoid fixes that only change displayed text without correcting the underlying order, hotbar, shelf, capacity, money, or inventory state.

### Values that must remain synchronized

- Cart quantity per ingredient.
- Total boxes in the cart.
- Checkout line quantities and total price.
- Available money before and after checkout.
- Stored usable units per ingredient.
- Pending ordered containers.
- Containers that have arrived at the truck.
- Containers collected into the hotbar.
- Quantity shown on every hotbar stack.
- Containers placed on shelves.
- Orders remaining overall.
- Orders placeable in the current storage room.
- Dry and frozen used/available capacity.
- Delivery/order status and notification badge.

### Required update behavior

- Refresh affected UI immediately after every successful transaction.
- Rebuild or rebind UI after scene changes, save loading, day rollback, and returning from `RestockScene`.
- Subscribe and unsubscribe cleanly from data-change events so reopening a panel does not create duplicate callbacks.
- Never rely on an old captured row, index, or quantity after the UI list has refreshed.
- Disable or guard buttons while checkout, truck collection, placement, scene loading, or saving is being committed.
- Repeated taps/clicks in one frame must not create duplicate orders, spend money twice, collect twice, or place two boxes.
- UI animations must not delay, block, or repeat the underlying transaction.
- A failed transaction must restore both the UI and data to their previous consistent state.
- Empty, zero, unavailable, loading, full-capacity, insufficient-money, and missing-icon states need intentional visuals and messages.
- Text must auto-fit without overlapping, and mobile buttons must retain large hit areas at supported aspect ratios.

### Count invariants

For each order:

```text
Ordered Containers =
In Transit
+ At Truck
+ In Restock Hotbar
+ Stored From This Order
+ Explicitly Cancelled/Discarded
```

For each storage type:

```text
Physical Capacity =
Free Cells
+ Stored Containers
+ Reserved Pending Containers
```

For each hotbar item:

```text
Displayed Stack Count = Authoritative Collected-Unstored Container Count
```

These equations should be asserted during development. Counts must never become negative, exceed capacity, or differ between the computer, truck, hotbar, shelf, and saved state.

### UI regression checks

- Open and close the Restock app repeatedly; quantities and callbacks remain correct.
- Add, subtract, clear, and re-add cart items; totals update exactly.
- Attempt checkout with insufficient money or capacity; no money/order state changes.
- Double-click/tap **Order Now**; exactly one order is created and charged.
- Let an order arrive while the computer is closed; the warning and badge update when appropriate.
- Double-complete or rapidly tap the truck interaction; collection occurs exactly once.
- Partially store a stack; hotbar, remaining order, capacity, and usable stock all update by exactly one container.
- Cancel or make an invalid placement; no count changes.
- Move an existing box; no inventory or capacity count changes.
- Change between Dry Storage, Walk-In Freezer, and Lobby1; all values remain identical.
- Save/load during Ordered, Arrived, Collected, and Partially Stored states; values restore exactly.
- Abandon an unfinished day; money, orders, hotbar, shelves, and inventory all return to the same day-start checkpoint.
- Test desktop and representative Android resolutions for clipped text, overlapping panels, missed taps, and stale values.

---

## 15. Intentional Implementation Order

### Phase 0 — Preserve and harden existing RestockScene work

- Keep current mouse/touch box dragging.
- Keep shelf snapping, occupancy, placement ghost, and invalid-drop rollback.
- Give every shelf a stable ID and Dry/Frozen type.
- Give every box a stable item/container binding.
- Add editable name-text and image references to the reusable box/crate prefabs.
- Bind Casual Dining ingredient names and icons to spawned containers.
- Add save/load for grid occupancy and box placement.

### Phase 1 — Functional restock hotbar

- Convert the existing `Hopbar` visual into a data-driven hotbar.
- Stack identical containers.
- Support mobile-sized slots.
- Bridge hotbar drag to `DraggableStorageBox`.
- Make valid placement a single atomic transaction.

### Phase 2 — Cart, checkout, and reserved capacity

- Replace instant Restock purchases with cart quantities.
- Add checkout review.
- Validate dry/frozen capacity including pending containers.
- Spend money and create an order only on confirmation.

### Phase 3 — Delivery and truck collection

- Add configurable delivery delay.
- Show arrival warning and optional Restock badge.
- Add truck availability state using a cube placeholder initially.
- Reuse circular hold interaction.
- Transfer order to the hotbar exactly once.

### Phase 4 — Lobby1 ↔ RestockScene flow

- Add stock-room entrance interactable and stand point.
- Add session context.
- Use iris close/load/open transition.
- Pause restaurant simulation without freezing RestockScene controls.
- Return to the correct Lobby1 location and resume once.

### Phase 5 — Completion, saves, and recovery

- Show orders remaining.
- Hide the hotbar when empty.
- Save order/hotbar/shelf transactions.
- Apply unfinished-day rollback consistently.
- Test reloads at every order state.

### Phase 6 — Storage mistakes and spoilage

- Wrong-storage warnings.
- Freshness persistence.
- Spoilage multipliers.
- Spoiled-item and throw-away flow.

### Phase 7 — Forecast and polish

- Expected visitors.
- Recommended inventory.
- Low-stock and overstock advice.
- Mobile UI polish, sound, animation, and tutorial prompts.

---

## 16. MVP Acceptance Checklist

- Computer purchases no longer instantly increase usable inventory.
- Checkout spends money exactly once and creates one order.
- Delivery arrives after the configured delay.
- The warning tells the player to collect from the truck.
- Truck collection requires a completed hold and cannot duplicate the order.
- The placeholder cube truck can later be swapped for final art without changing delivery logic or saved state.
- Collected identical items stack in the restock hotbar.
- Hotbar appears only when collected containers remain.
- Lobby1 stock-room interaction routes the player before loading.
- Iris transition plays in both directions.
- Restaurant simulation is paused in RestockScene and resumes correctly.
- Mouse and Android touch can drag hotbar boxes to shelves.
- Invalid drops do not consume a box.
- Valid drops occupy one shelf cell and add units exactly once.
- Existing boxes remain movable without duplicating stock.
- Every Casual Dining container shows the correct ingredient name and icon on the reusable box/crate prefab.
- The same Casual Dining icon is reused consistently across the computer, hotbar, box label, and item details.
- Shelf layout, hotbar, and order state survive a normal save/load.
- Abandoning an unfinished day rolls the entire day back consistently.
- Starting the restaurant after restocking returns to normal gameplay.

---

## Core Architectural Rule

> A stock container changes location and state as it travels from order to shelf. The existing inventory system remains authoritative, but recipe-usable quantity is granted only after a delivered container is successfully stored on a physical shelf.

This keeps the feature reusable without duplicating inventory across the computer, truck, hotbar, RestockScene, and kitchen.
