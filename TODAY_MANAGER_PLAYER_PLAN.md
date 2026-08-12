# Today's Task: Add the Manager Player

Date: 12 August 2026  
Time budget: At least four focused hours  
Scope: Manager player only

## 1. Goal

Add the player-controlled Manager to the current unified restaurant gameplay scene.

The Manager must be able to help with the restaurant work currently supported by the existing game. The player must not switch identities or become an employee role. The player remains the Manager while temporarily performing receptionist, waiter, cashier, busser, or available kitchen interactions.

This task does **not** implement the full daily management loop, employee applicants, weekly reviews, supplier price changes, marketing, restaurant variants, or the final kitchen-bot workflow.

## 2. Current Technical Reality

### Lobby

The Lobby already contains working restaurant processes and autonomous employee behavior. The Manager should be connected to those existing interactions without disrupting the bots.

Target Lobby capabilities:

- Receptionist: assign or seat waiting customer groups.
- Waiter: interact with tables, orders, trays, and deliveries where currently supported.
- Cashier: use the existing cashier interaction and UI.
- Busser: clear, carry, and clean table-related items where currently supported.

### Kitchen

The new unified version does not yet have complete working kitchen employees. Food currently spawns after the Lobby order-processing path instead of being prepared by chef or barista bots.

For today:

- The Manager may access and interact with kitchen systems that already work.
- Existing placeholder food-spawning behavior must remain intact.
- Do not build the final chef or barista bot workflow.
- Do not migrate the complete original Kitchen-scene workflow today.
- Avoid code that would prevent the future Manager from performing chef and barista tasks.

## 3. In Scope

- Add or configure the Manager player prefab in the target unified scene.
- Use one selected movement implementation.
- Use one selected camera implementation.
- Use one selected interaction-targeting implementation.
- Give the Manager permission to perform all currently available restaurant interactions.
- Remove or bypass employee-role restrictions for the Manager through a maintainable capability rule.
- Preserve all existing Lobby bot behavior.
- Protect tested interactions from duplicate player/bot completion.
- Connect required UI and hand/item references.
- Fix integration and compile errors caused by this work.
- Document Unity Inspector assignments that cannot be completed safely through code alone.

## 4. Out of Scope

- Management and Service phase system
- Management computer features
- `Open Restaurant` button
- Inventory redesign
- Booth purchasing and placement
- Weekly applicants and employee replacement
- Employee experience and salary progression
- Supplier price changes
- Menu price editing
- Weekly reviews
- Marketing
- Daily report redesign
- Day 30 campaign logic
- Fast Food restaurant
- Fine Dining restaurant
- Kiosks and pianist
- Final chef and barista bots
- Full kitchen-order preparation migration
- Multiplayer expansion or synchronization redesign
- Large legacy-system refactors unrelated to spawning and enabling the Manager

## 5. Manager Rules

### Identity

- The player is always identified as `Manager`.
- Helping with a task does not change the player's identity.
- Existing role-selection UI must not be required for Manager interactions.
- Code must not repeatedly change the Manager among employee-role enum values.

### Capabilities

The Manager has access to every supported employee capability.

Conceptually:

```text
Manager
  can perform Receptionist interactions
  can perform Waiter interactions
  can perform Cashier interactions
  can perform Busser interactions
  can perform Chef interactions when implemented
  can perform Barista interactions when implemented
```

The preferred implementation is an explicit Manager override or capability check. Do not duplicate every employee script onto the player if the same result can be achieved through the shared interaction path.

### Interaction ownership

For every interaction adapted today:

- Check that the target is still valid before starting.
- Prevent a bot and the Manager from finalizing the same action twice.
- Release temporary ownership when the Manager cancels or the target becomes invalid.
- Preserve the existing bot's ability to use the interaction afterward.
- Apply rewards, state changes, inventory changes, and order progression exactly once.

## 6. Implementation Order

### Step 1 — Identify the target scene and player path

Confirm:

- Which scene is the current unified restaurant scene.
- Which player prefab should become the Manager.
- Which movement script is active on that prefab.
- Which camera script follows it.
- Which interaction component selects restaurant objects.
- Whether the player is local-only or already network-spawned in this scene.

Do not add a second movement, camera, input, or interaction controller if a valid one is already active.

### Step 2 — Establish Manager identity

Add the smallest maintainable representation of Manager identity needed by current interactions.

Requirements:

- Existing employee roles remain valid for bots.
- Manager identity does not break serialized role values.
- Manager capability checks are centralized where practical.
- Future chef and barista permissions can be added without redesigning the identity system.

### Step 3 — Spawn and control the Manager

Ensure that:

- Exactly one local Manager spawns.
- The Manager starts at the intended spawn point.
- Input controls only the local Manager.
- The camera follows the correct object.
- Movement and animation remain functional.
- Required hand, carry, raycast, outline, and UI references are assigned.

### Step 4 — Enable Lobby interactions incrementally

Adapt and verify one capability at a time in this order:

1. Receptionist
2. Busser
3. Waiter
4. Cashier

The order may change if the scene shows that another interaction has fewer dependencies, but only one path should be changed and tested at a time.

For each capability:

1. Find the exact role gate.
2. Allow Manager access without removing the restriction for incompatible bots.
3. Confirm required held items and UI references.
4. Test the normal success path.
5. Test cancellation or invalid target behavior.
6. Test conflict with the existing bot.

### Step 5 — Preserve the current kitchen placeholder

Verify that Manager changes do not stop the current order-to-food-spawn path.

If there is a safe existing kitchen interaction for the player, expose it through Manager capability rules. Do not invent the final preparation system today.

### Step 6 — Stabilize

- Resolve compile errors.
- Check for new missing-reference errors.
- Check for duplicate player objects or cameras.
- Check for duplicate order, payment, and cleaning completion.
- Record Inspector assignments and unresolved scene-only work.

## 7. Four-Hour Work Schedule

### Hour 1 — Audit

- Locate the current unified scene and Manager/player prefab.
- Trace the active movement, camera, and interaction path.
- Find all Lobby role checks relevant to the player.
- Identify the minimum affected files.
- Prepare the first coding-AI prompt using the real class names and APIs.

Exit result: no code generation begins until the active player and interaction path is known.

### Hour 2 — Manager foundation

- Integrate Manager identity/capability behavior.
- Add or configure the player spawn.
- Verify movement and camera.
- Enable the first simple Lobby capability.
- Compile and correct integration errors.

Exit result: controllable Manager plus one working restaurant interaction.

### Hour 3 — Expand current capabilities

- Add the next one or two Lobby capabilities.
- Reuse existing employee interaction logic.
- Add minimum conflict protection where required.
- Verify bots still perform their normal tasks.

Exit result: Manager assists with multiple existing Lobby tasks without role switching.

### Hour 4 — Complete and verify

- Attempt the remaining current Lobby capability if earlier paths are stable.
- Verify the placeholder kitchen/order flow still works.
- Fix errors and missing assignments.
- Produce a play-test checklist and next-session backlog.

Exit result: the broadest stable Manager capability set achievable without beginning the future kitchen-bot system.

## 8. Minimum Acceptance Criteria

The task is successful today if all minimum items pass:

- [ ] One Manager player exists in the target unified scene.
- [ ] Manager movement works.
- [ ] The camera follows the Manager correctly.
- [ ] Manager interaction targeting works.
- [ ] The Manager completes at least one existing Lobby employee interaction.
- [ ] The interaction does not require role switching.
- [ ] Existing Lobby bots continue operating.
- [ ] The tested action cannot be completed twice by the Manager and a bot.
- [ ] Current placeholder food spawning still works.
- [ ] No new blocking compile or Console errors remain.

## 9. Target Acceptance Criteria

If integration proceeds smoothly, aim for:

- [ ] Manager can seat customers as Receptionist.
- [ ] Manager can perform the current Waiter workflow.
- [ ] Manager can use the Cashier workflow and UI.
- [ ] Manager can perform the current Busser workflow.
- [ ] Manager can access currently valid kitchen interactions.
- [ ] All capabilities work without changing Manager identity.

Kitchen food preparation by employee bots is explicitly not required for today's target.

## 10. Stop Conditions

Stop expanding capabilities and stabilize the working subset if:

- The active scene or player prefab cannot be identified confidently.
- Adding Manager identity would corrupt existing serialized role values.
- A Lobby interaction requires a large unrelated rewrite.
- Player/bot double completion cannot be prevented locally.
- Compile errors spread into unrelated legacy systems.
- Scene wiring cannot be verified without Unity Editor input.

When a stop condition occurs, preserve the working path, document the exact blocker, and make that blocker the next bounded task.

## 11. Deliverables

By the end of today's session:

1. Integrated Manager player code and prefab/scene configuration where possible.
2. A list of modified files.
3. A Unity Inspector wiring checklist.
4. Compile and play-test results.
5. Known limitations, especially around the temporary kitchen workflow.
6. A focused next prompt for unfinished Manager capabilities or the future kitchen-task migration.
