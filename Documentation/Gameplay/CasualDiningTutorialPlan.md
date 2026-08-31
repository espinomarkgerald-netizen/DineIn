# Casual Dining Tutorial Plan

> Status: planning only. This document describes the replacement tutorial flow; it does not mean the tutorial has already been implemented.

## The rule that everything follows

The player is always the **Manager**.

- There is no role selection and no changing between Host, Waiter, Cashier, or Busser characters.
- The Manager personally learns and performs each station's work.
- The reason is simple: a restaurant manager must understand every station so they can support staff whenever service needs help.
- In the normal game, hired staff handle their assigned work automatically. The Manager can step in when a task needs attention or when the player wants to help.
- Old tutorial language such as “You are the Host now” must be replaced with “Let’s learn the reception station.”

The old role-switch buttons, separate role characters, and “Day 1 Host / Day 2 Waiter / Day 3 Cashier” structure must not be used by the new tutorial.

## Tutorial goal

Teach a first-time mobile player enough to begin Casual Dining Day 1 without guessing:

1. Control the camera and move the Manager.
2. Greet and seat a customer group.
3. Take, confirm, collect, and serve an order.
4. Process a cash payment at the cashier station.
5. Clear and clean the table.
6. Use every management-computer app.
7. Order, receive, and store ingredients.
8. Read the pre-shift checklist and start Day 1.

The tutorial should demonstrate the normal game systems, not a simplified fake version of them.

## Narrative framing

The narrator is a human **Regional Training Supervisor**. The portrait appears beside short dialogue cards. The narrator never calls the Manager the owner.

Suggested opening:

> Welcome, Manager. You have been assigned to this Casual Dining branch. A good manager understands every station so they can support the staff whenever service needs help. Before Day 1, I’ll guide you through the controls and the restaurant’s daily operations.

Use language such as:

- “your assigned branch”
- “the restaurant under your supervision”
- “support your staff”
- “prepare the restaurant for service”

Avoid language such as:

- “you own the restaurant”
- “design your restaurant”
- “build your brand”
- “manage marketing”

This keeps the scope focused on restaurant operations management.

## How every tutorial step should behave

Each step follows the same small pattern:

1. The narrator gives one short instruction.
2. The camera frames the relevant area if it is off-screen.
3. The required target is highlighted.
4. Unrelated input is temporarily blocked when necessary.
5. The player performs one clear action.
6. The real gameplay event confirms completion and immediately advances the tutorial.

### Highlighting UI

For a UI button, card, field, or panel:

- Darken the full screen with a semi-transparent overlay.
- Cut a clear spotlight hole around the required element.
- Add a soft pulse to that element.
- Keep only the highlighted control clickable.
- Place the narrator card where it does not cover the target.
- Use an arrow only when the spotlight alone is not clear enough.

### Highlighting objects in the restaurant

For a customer, booth, counter, computer, truck, shelf, or sink:

- Frame it with the camera.
- Apply the existing outline/highlight treatment.
- Show a ground marker or short path from the Manager.
- Keep the normal interaction bubble visible.
- Do not darken the restaurant so heavily that the player cannot understand the space.

### Help and recovery

- If the player is idle for about 8 seconds, pulse the target again.
- If idle for about 15 seconds, repeat a shorter hint.
- If the target is off-screen, show an edge indicator and allow the task button to refocus the camera.
- A wrong tap should not fail the tutorial. It should be ignored or show a short correction.
- Every step needs a safe retry if movement, navigation, or an animation is interrupted.
- Tutorial progress should save at the start of each major section, not in the middle of a transaction.

## Tutorial setup

- Scene: a controlled copy/configuration of the current Casual Dining lobby.
- Player character: one Manager with all normal station capabilities.
- Staff bots: disabled. No Host, Waiter, Cashier, Busser, Chef, or Barista bot should perform tutorial work.
- Customers: only the scripted customer group required for the lesson.
- Customer spawning, patience pressure, complaints, random events, and shift timer: disabled until instructed.
- Money and stock: fixed tutorial values that cannot damage the campaign save.
- Food, bill, money, and mess objects: created through the real gameplay systems at controlled moments.
- Existing task bubbles and the Player Task HUD remain visible because players will use them in the main game.

## Full tutorial flow

### 1. Camera and movement

Purpose: make the player comfortable navigating before asking them to manage a station.

| Step | What the player sees | Required action | Completion cue |
| --- | --- | --- | --- |
| Camera pan | A hand animation drags across the restaurant. The camera target area glows. | Drag with one finger on Android. On PC, drag with the right mouse button. | Camera moves far enough in the requested direction. |
| Camera zoom | Two fingers animate inward and outward over the dining area. | Pinch to zoom on Android. On PC, use the mouse wheel. | Player performs both a zoom-in and a zoom-out. |
| Move Manager | A ground marker appears near reception with a short path from the Manager. | Tap/click the marker. | The Manager reaches the target. |
| Interact by moving | The computer or reception object is briefly highlighted as an example. | Tap the highlighted object and watch the Manager walk to its interaction point. | The normal move-to-action callback completes. |

Narrator reminder:

> Tap a station or task and the Manager will move to it. You can still drag the camera while deciding what to handle next.

### 2. Reception station

Purpose: teach the current two-stage reception flow without pretending the Manager became a Host.

1. Spawn one calm customer group at reception.
2. Highlight the customer’s **Greet Customer** bubble.
3. The player taps it; the Manager walks to the group and greets them.
4. The same bubble changes to **Seat Table**.
5. Highlight **Seat Table**, then highlight one valid empty booth.
6. The player selects the booth.
7. The group walks to the selected booth and sits.
8. Show the task HUD updating from greeting to seating, then clear it when seating is complete.

Completion event: the scripted group is greeted, assigned to the chosen booth, and reaches the seated state.

Narrator line:

> This is the reception station. Greet arriving customers, then assign a table that can fit their group.

### 3. Order and table service

Purpose: teach the Manager to assist with the waiter workflow using the existing notepad, task claim, tray, bill, and payment systems.

#### Take the order

1. Allow the seated group to enter **Ready to Order**.
2. Highlight its order bubble and task HUD entry.
3. The Manager walks to the table and opens the notepad.
4. Spotlight the customer request area first; do not allow menu selection yet.
5. Briefly explain that the message, product images, and quantities describe the requested order.
6. Spotlight the correct food card, then its quantity control if needed.
7. Spotlight the correct drink card and quantity.
8. Spotlight **Confirm Order** only after every requested item matches.
9. On confirmation, close the notepad and show the order number that will also appear on the prepared tray.

Completion event: the existing order-confirmed event fires for the tutorial group.

#### Collect and serve the prepared order

1. Advance the normal order state and create the matching prepared tray at the service counter. No kitchen bot is shown.
2. Highlight the tray’s pickup bubble and its order/table number.
3. The Manager picks up the tray.
4. Update the task HUD to **Deliver Order > Table #**.
5. Highlight the correct table while other tables remain unavailable.
6. The Manager delivers the tray to the customer group.

Completion event: the matching tray is delivered to the correct group through the existing delivery interaction.

#### Bill and table payment handoff

1. Move the tutorial group to **Needs Bill** after a short, controlled delay.
2. Guide the Manager to request/collect the printed bill at the cashier station.
3. Highlight the bill’s table number.
4. Guide the Manager back to the same table to deliver it.
5. When cash appears at the table, highlight **Collect Payment**.
6. The Manager picks it up and the task HUD changes to **Take Payment > Cashier**.

Completion event: the Manager is holding payment for the correct tutorial group and has reached the cashier station.

### 4. Cashier station

Purpose: teach the cash register without switching to a Cashier character.

1. Highlight the cashier booth while the Manager is carrying the payment.
2. Tapping the booth opens the existing Cashier UI.
3. Spotlight the order summary and total.
4. Spotlight the **Cash Received** value.
5. Spotlight the expected change.
6. Enable the money buttons and guide the player to enter the exact change.
7. Allow **Undo** to remain available as a safe correction.
8. Enable **Confirm** only when the entered change is correct.
9. Confirm the payment, update finance/revenue through the normal system, and close the register.

Completion event: the existing payment-completed event fires for the tutorial group.

Do not teach card payment here. It is intentionally left as a straightforward variation for the player to discover later.

Narrator line:

> You are still the Manager. When the cashier station needs support, use the register to verify the total and return the correct change.

### 5. Clearing and cleaning

Purpose: teach both used-tray cleanup and table cleaning.

1. Let the tutorial group leave and keep its used tray at the booth.
2. Highlight the tray pickup interaction.
3. The Manager carries the dirty tray to the sink.
4. Highlight the sink and complete the existing tray-clean action.
5. If a table mess is part of the tutorial setup, reveal its **Clean** bubble after the tray is removed.
6. Demonstrate the hold gesture visually.
7. The player holds **Clean** until the radial meter finishes.
8. Restore the booth to its clean/available state.

Completion events: dirty tray cleaned at the sink; optional booth mess cleaned through the existing hold-to-clean controller.

Narrator line:

> Clear used trays first. If the table is still dirty, hold Clean until the station is ready for the next group.

### 6. Management computer

Purpose: teach pre-shift decisions after the player understands what those decisions affect during service.

The Manager walks to the computer and opens it. The desktop fills the available mobile computer UI. Teach one app at a time with spotlight masks. Other app icons stay visible but locked until their turn.

#### Dashboard

- Point out the day, cash, approval, and time status area.
- Open **Dashboard**.
- Highlight restaurant status, today’s menu count, scheduled staff, inventory, restaurant rating, and latest review.
- Demonstrate that dashboard shortcuts open the related app.
- No data must be changed in this step.

#### Staff Scheduler

- Open **Staff** and explain Lobby and Kitchen tabs.
- Open one role section.
- Highlight employed staff, applicants, salary information, and the role capacity.
- Hire one predetermined tutorial applicant.
- Set one hired employee as the active employee for that role.
- Explain that up to three employees can be kept per role, but only one is scheduled/active for the shift.
- Mention that the Manager can still help every station even when employees are assigned.
- Avoid forcing the player to fill every role in the tutorial; provide ready tutorial staff for the remaining roles.

#### Menu Editor

- Open **Menu**.
- Explain that only available and unlocked products appear in customer orders.
- Spotlight one product’s availability control and let the player enable it.
- Spotlight its price field/control and let the player set a safe prompted price.
- Show a short preview of how price affects revenue and customer acceptance without teaching advanced optimization.
- Leave the tutorial menu in a valid state for Day 1.

#### Ingredient Restock

- Open **Restock**.
- Highlight current stock, projected need, storage type, crate quantity, unit cost, and cart total.
- Add the prompted dry ingredient and frozen ingredient.
- Show insufficient-money or capacity warnings only as visual examples; do not make the player intentionally fail.
- Confirm the order and show that it is now in delivery, not instantly placed on the shelves.

#### Equipment Store

- Open **Equipment**.
- Explain the two current sections: **Booths & Seating** and **Restaurant Upgrades**.
- Highlight lock status, description, price, and purchased state.
- Let the player inspect one item. Only require a purchase if the tutorial wallet and campaign setup are designed to keep it afterward.
- Explain that equipment is purchased before service and saves automatically.

#### Finances

- Open **Finances**.
- Highlight cash balance, revenue today, ingredient purchases, scheduled payroll, and recent transactions.
- Explain that payroll is settled at the end of the day.
- This is a reading step; no input beyond opening and scrolling is required.

#### Objectives / Alien Demands

- Open **Objectives**.
- Highlight mandatory, secondary, and bonus objectives one at a time.
- Explain that these are evaluated automatically at the end of the shift.
- Mention the separate Alien Approval and restaurant rating indicators without turning this into a scoring lecture.

After all seven apps have been visited, spotlight the computer exit button and return control to the restaurant.

### 7. Delivery and restock room

Purpose: connect the computer order to the physical stock workflow.

1. Show the delivery notification and truck edge indicator.
2. Highlight the delivery truck.
3. The Manager walks to the truck and collects the delivered crates.
4. Explain that collected crates appear in the restock hotbar.
5. Highlight the dry-room entrance first.
6. Enter through the existing iris transition and wait until input is safely restored.
7. Highlight the correct dry crate in the hotbar.
8. Drag or select it, then place it in a valid shelf grid slot.
9. Highlight the crate label and its live remaining item count (`x20`, `x19`, and so on).
10. Exit the dry room using its normal exit button.
11. Highlight the freezer entrance.
12. Enter the freezer and place the frozen crate on a valid freezer shelf.
13. Demonstrate a wrong-storage warning without committing the invalid placement.
14. Exit back to the lobby.
15. Confirm the hotbar is empty and the stored quantities match the inventory ledger.

The restock lesson must use the real delivery order, hotbar, shelf grid, storage validation, live crate quantity, and safe scene-transition systems.

### 8. Readiness and Day 1 handoff

1. Return to the management computer.
2. Spotlight **Start Shift**.
3. Open the current readiness checklist.
4. Review each row: news, menu, staff, equipment, and restock/storage readiness.
5. If a tutorial action was missed, its row should open the correct app or point to the correct world target.
6. When there are no blockers, enable the final **Start Shift** action.
7. Show a short completion message from the Training Supervisor.
8. Save tutorial completion, clear all tutorial-only state, and start Campaign Day 1 through the normal game flow.

Suggested completion line:

> Your branch is prepared. You now know how to support every station and make the decisions required before service. When you’re ready, begin Day 1.

## What the base tutorial intentionally leaves for discovery

These systems should not receive a forced step in the first Casual Dining tutorial:

- Customers asking specifically for the Manager and complaint-response choices.
- Card payments.
- Takeout service if it is not unlocked on Day 1.
- Trolley upgrades and autonomous trolley behavior.
- Advanced pricing strategy.
- Rare customer types, random events, and late-game equipment.
- Optimizing approval, rating, payroll, and profit across multiple days.

The game may show contextual hints the first time these systems appear during the campaign.

## Training Mode and future restaurant tutorials

- The first new Casual Dining campaign routes the player into this tutorial before Day 1.
- After completion, **Training Mode** appears in the gameplay-selection screen.
- Training Mode lets the player replay a restaurant tutorial without changing campaign money, stock, day, staff, objectives, or unlock progress.
- Each restaurant can have its own training card.
- Casual Dining is the base operations tutorial.
- Fast Food later adds its own unlocked module focused on the Manager learning hands-on cooking and its different service flow.
- Fine Dining later adds only its new restaurant-specific mechanic instead of repeating the entire base tutorial.

## Implementation order

### Phase 1 — Replace the old tutorial structure

- Add a new station-based tutorial controller/state list.
- Use the permanent `ManagerPlayer` as the only controllable character.
- Disable old role-switch tutorial UI and role-day gates in the new tutorial runtime.
- Keep old scripts untouched until the replacement flow is validated, then remove only genuinely obsolete tutorial dependencies.

### Phase 2 — Build reusable guidance tools

- Full-screen dimmer with a configurable spotlight cutout for UI.
- Input blocker that allows only the required highlighted target.
- World-object focus using outline, camera framing, path/marker, and optional arrow.
- Narrator card with editable portrait, speaker name, dialogue text, and placement.
- Idle reminder, refocus, safe retry, and checkpoint support.
- All important references remain serialized and editable in the Unity Inspector.

### Phase 3 — Connect the station lessons

- Controls and movement.
- Reception.
- Order and table service.
- Cashier.
- Clearing and cleaning.
- Use the existing task-claim and task-HUD events as completion signals wherever possible.

### Phase 4 — Connect management and restocking

- All seven computer apps.
- Start-shift readiness checklist.
- Restock order, delivery truck, hotbar, dry room, freezer, shelves, and safe exit transition.
- Avoid hard-coded UI positions; spotlight bounds should follow the actual target `RectTransform` on PC and Android.

### Phase 5 — Add entry, replay, and save rules

- First-campaign tutorial gate.
- Training Mode selection card.
- Resume from section checkpoints.
- Replay in isolated tutorial state.
- Clean handoff to Campaign Day 1.

### Phase 6 — Test before calling it complete

- Android test on the Realme 8 5G resolution and safe area.
- Additional wide, tall, and tablet Android simulator profiles.
- PC mouse and keyboard test.
- Verify every target remains readable and comfortably tappable.
- Verify the dark overlay never blocks the highlighted control.
- Verify no old role-switch button or role name appears.
- Verify only the Manager is controllable.
- Verify exiting the restock room cannot leave a black screen or locked input.
- Verify replaying Training Mode does not alter the campaign save.
- Verify Day 1 starts only after tutorial completion and readiness validation.

## Definition of done

The Casual Dining tutorial is ready when a first-time mobile player can complete the entire flow without verbal help, can explain why the Manager learns every station, and reaches a safe, playable Day 1 with valid staff, menu, stock, and UI state.
