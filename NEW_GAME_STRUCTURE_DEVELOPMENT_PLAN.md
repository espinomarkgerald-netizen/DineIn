# Dine In - New Game Structure Development Plan

Planning date: 11 August 2026

## Purpose

This plan turns the new Dine In direction into an executable development roadmap. It covers the move from the current role-based multi-scene project to a management-focused restaurant game with bots, a 30-day campaign, endless play, unrestricted four-player multiplayer, restaurant variants, standardized UI/tutorials, and local settings.

This is a plan only. It does not authorize deleting legacy systems until their replacements pass scene and play-mode validation.

## 1. Current Starting Point

Completed groundwork:

- First-party scripts and assets are organized under `Assets/_Project`.
- The old `Assets/Assets/MAINGAME` tree is removed.
- Existing Kitchen, Lobby, Office, Tutorial, and Multiplayer scenes are grouped under `Assets/_Project/Scenes/RoleBased`.
- The redesigned menu is imported as `NewMainMenu` and `NewGameMenu` without replacing the existing menu.
- Menu settings now use local storage only.
- Low, Mid, and High renderer profiles exist and are wired to the new menu.

Important legacy load still present:

- `Lobby`: 18 scripts, including role assignment and role switching.
- `Kitchen`: 32 scripts, including kitchen-role workflow and kitchen tutorial logic.
- `Office`: 50 scripts, including finance, HR, inventory, recipes, and older movement variants.
- `Tutorials`: 37 scripts, many tied to role-specific teaching.
- `Restaurant`: 54 scripts for booths, orders, cleaning, payments, takeout, and interactables.
- `Customers`: 12 scripts for customer groups, seating, orders, and bubbles.
- `Networking`: 14 scripts using Photon and PlayFab bindings.

The project should treat the RoleBased scenes as a stable reference implementation, not as the target architecture.

## 2. Product Decisions To Lock First

The team should treat these as the current design baseline. Changing them later changes the timeline.

```text
Player identity: Management helper
Staff identity: Bots own normal roles and tasks
Player permissions: Can assist every available task
Gameplay scene: One main restaurant scene
Campaign: Days 1-30 with alien approval
Post-Day-30: Endless play in Casual Dining; approval no longer matters
Multiplayer: Up to 4 unrestricted managers
Restaurant variants: Fast Food, Casual Dining, Fine Dining
Variant differences: Menu, theme, aesthetics only
Settings: Local device persistence only
```

## 3. Target Project Structure

The present folders are a clean transition state. The target structure below should be reached gradually as features are replaced.

```text
Assets/_Project
  Core/                 Application bootstrap, game flow, loading, global settings
  Data/                 ScriptableObject configs, save schemas, balance data
  Gameplay/             Day loop, campaign, approval, objectives, game over
  Restaurant/           Shared restaurant task and station systems
    Customers/          Customer state, queue, seating, orders, payment
    Tasks/              Interactables, task state, reservation, rewards
    Stations/           Cooking, serving, cleaning, cashier, restock stations
  Bots/                 Bot brain, task selection, navigation, role profiles
  Player/               Manager movement, interaction, camera, cosmetics, animation
  Networking/           Room flow, player spawn, replicated task state, ownership
  UI/                   Shared screens, HUD, menus, feedback, accessibility
  Tutorials/            Data-driven tutorial steps and contextual hints
  Save/                 One save owner and migration helpers
  RestaurantVariants/   Fast Food, Casual Dining, Fine Dining configs and themes
  MainMenu/             Main menu UI, account UI, local settings, menu scenes
  Scenes/
    Bootstrap/
    MainMenu/
    Restaurant/
    LegacyRoleBased/    Temporary reference only; remove after replacement validation
  Debug/                Development-only tools and diagnostics
```

Rules:

- New gameplay code must not be added to `Lobby`, `Kitchen`, or `Office` unless it is a deliberate legacy bug fix.
- New code must depend on task/state interfaces, not current role names.
- Restaurant variants must use data/configuration, not copied scenes or duplicated gameplay scripts.
- A scene must never require a hard-coded role to let a player interact with a task.

## 4. Architecture Before Features

### 4.1 Interaction and Task Spine

Refactor the existing interaction path around `IInteractable`, `PlayerMovement`, and restaurant interactables before building bots.

Target responsibilities:

- `TaskDefinition`: task type, priority, reward, required station/state.
- `TaskInstance`: live task state, reservation owner, completion state, timeout.
- `TaskReservation`: prevents a bot and a player from completing the same task at once.
- `TaskExecutor`: shared completion path used by players and bots.
- `PlayerInteractionController`: manager input and proximity selection only.

This is the highest-value refactor because bots and multiplayer both need a single source of truth for task ownership.

### 4.2 Bot Staffing Spine

Bots should not be role-switched player clones. Each bot should have a role profile and select tasks from the shared task queue.

Target pieces:

- `BotStaffController`: bot lifecycle, state transitions, navigation bridge.
- `BotRoleProfile`: allowed task types and priorities per staff type.
- `BotTaskPlanner`: selects and reserves the next valid task.
- `BotTaskExecutor`: performs the shared task completion path.
- `RestaurantTaskBoard`: creates, tracks, prioritizes, and exposes task instances.

Initial bot roles for the MVP:

- Host/customer seating
- Waiter/order and delivery support
- Kitchen cook/prep support
- Busser/cleaning support
- Cashier/payment support

The player can reserve and complete any exposed task. A player never needs to become a different role.

### 4.3 One-Scene Restaurant Spine

Create `MainRestaurant.unity` as a new scene. Do not merge the current Office, Lobby, and Kitchen scenes directly. Build the new scene around a playable vertical slice, then migrate validated systems one at a time.

Required first slice:

- Entry/management area
- Dining area with customer queue and tables
- Kitchen station set for one menu
- Bot navigation and spawn points
- Manager player spawn and camera
- Shared task board
- Local save/load test path

### 4.4 Campaign and Save Spine

Keep `GameSaveManager` as the intended primary save owner until testing proves otherwise. Treat `LocalGameSaveManager` as migration-only and audit `LocalSaveManager` before removal.

Save data must cover:

- Current campaign day
- Approval state and campaign completion
- Endless-mode unlock state
- Restaurant variant unlocks
- Currency, cosmetics, power-ups, revive state
- Player local settings
- Tutorial completion

Campaign rule:

```text
Days 1-30: Approval affects progression and failure rules.
Day 31 onward: Endless Casual Dining mode; approval UI and penalties are inactive.
```

## 5. Refactoring Order

Do not refactor by folder name alone. Each refactor must preserve a working play path and receive a scene/prefab reference check.

### P0 - Required Before New Core Gameplay

1. Define one save owner.
   - Audit `GameSaveManager`, `LocalGameSaveManager`, and `LocalSaveManager`.
   - Preserve backwards-compatible saves during migration.

2. Normalize manager/player movement ownership.
   - Keep one manager movement and interaction path for new gameplay.
   - Do not carry `KitchenPlayerMovement`, `SimplePlayerMovement`, `PlayerMove`, and legacy `Movement` into the new scene.

3. Replace role checks with task capability checks.
   - Audit `RoleManager`, `StaffRole`, `RoleBasedAssignController`, and direct role lookups.
   - Disable role restrictions only after the shared task system works.

4. Split global scene/day control.
   - Clarify ownership among `GameFlowManager`, `GameManager`, `CoreManagersBridge`, and scene loaders.
   - One service owns day transitions; UI does not own campaign state.

5. Freeze legacy scenes.
   - Fix blocking bugs only.
   - Do not add new gameplay content to `Scenes/RoleBased`.

### P1 - Required For Thesis MVP

1. Create `MainRestaurant.unity` vertical slice.
2. Implement shared task board and player interaction controller.
3. Implement basic bots for host, waiter, kitchen, busser, and cashier tasks.
4. Migrate one customer-to-payment loop into the new scene.
5. Implement Day 1 to Day 30 campaign rules and post-Day-30 endless mode.
6. Convert tutorial flow to data-driven contextual prompts for manager gameplay.
7. Adapt existing Photon flow to four unrestricted manager players.
8. Finish one polished Casual Dining variant.

### P2 - Full Planned Vision

1. Fast Food and Fine Dining data/theme variants.
2. Cosmetics, power-ups, revives, currency conversion, and bundles.
3. Expanded bot failure/recovery behavior.
4. Full tutorial polish and accessibility pass.
5. Multiplayer reconnect, late join, and host migration hardening.

### P3 - Decommissioning

Only after the new restaurant scene passes a complete launch-to-endless-play test:

- Archive or remove legacy role UI and role-switch code.
- Archive unused Office/Lobby/Kitchen scene-specific controllers.
- Remove unused tutorial role gates.
- Rename known file/class mismatches after scene references are validated.

## 6. Delivery Phases And Estimates

Estimates include implementation, scene wiring, debugging, and focused testing. They assume a three-person student team with 15 productive hours per person per week, or 45 team hours per week. They do not assume full-time development.

| Phase | Deliverable | Person-hours | Calendar at 45 h/week |
| --- | --- | ---: | ---: |
| 0 | Scope lock, current-build smoke test, save ownership audit | 50-75 | 1-2 weeks |
| 1 | P0 refactor spine: flow, save, movement, task contracts | 140-210 | 3-5 weeks |
| 2 | MainRestaurant vertical slice and migrated customer loop | 130-190 | 3-4 weeks |
| 3 | Bot staffing MVP and task reservation | 200-300 | 5-7 weeks |
| 4 | 30-day campaign, approval cut-off, endless mode, local saves | 90-140 | 2-3 weeks |
| 5 | Four-player manager multiplayer integration | 130-220 | 3-5 weeks |
| 6 | Standard UI, contextual tutorial, Casual Dining polish | 120-190 | 3-4 weeks |
| 7 | QA, balancing, device testing, build stabilization | 120-180 | 3-4 weeks |
| 8 | Fast Food, Fine Dining, cosmetics, power-ups, economy | 220-360 | 5-8 weeks |

### Computed Completion Windows

Thesis-ready MVP, Phases 0-7:

```text
850-1,305 person-hours
850-1,305 / 45 team-hours per week = 19-29 weeks
Add 15% integration buffer = 22-34 calendar weeks
```

Full vision, Phases 0-8:

```text
1,070-1,665 person-hours
1,070-1,665 / 45 team-hours per week = 24-37 weeks
Add 15% integration buffer = 28-43 calendar weeks
```

Practical answer:

- Thesis-ready management-game MVP: about 5-8 months part-time.
- Full planned vision: about 7-10 months part-time.
- A four-person team completing 20 focused hours each per week (80 team hours/week) can reduce the MVP window to about 3-5 months.

The largest uncertainty is bot behavior plus multiplayer synchronization. If multiplayer is deferred until the restaurant loop is stable, the MVP is much more likely to finish on schedule.

## 7. MVP Scope Guard

To protect the thesis timeline, the MVP should contain only:

- One polished Casual Dining restaurant scene
- Manager player with unrestricted task help
- Basic staff bots for the five key workflows
- One customer loop: arrive, seat, order, cook, serve, pay, clean
- 30 campaign days and endless post-campaign mode
- Local save/load
- Four-player multiplayer only after single-player loop is stable
- Standardized minimal UI and tutorial

Defer from MVP unless the earlier phases finish ahead of schedule:

- Fast Food and Fine Dining production assets
- Cosmetic store/bundles
- Currency conversion economy
- Complex revive/power-up systems
- Advanced bot personalities and rare events
- Full multiplayer reconnect/host-migration support

## 8. Milestone Acceptance Tests

Every milestone needs a visible pass condition before the next one begins.

| Milestone | Pass condition |
| --- | --- |
| P0 complete | Clean launch has no blocking Console errors; save owner is documented. |
| Interaction spine | Player can complete a shared task without role switching. |
| Bot slice | One bot can reserve, execute, and release a task without conflicting with player input. |
| One scene | Customer loop works in `MainRestaurant.unity` without loading Office/Lobby/Kitchen. |
| Campaign | Day 30 approval rule works; Day 31 disables approval consequences. |
| Multiplayer | Four players can join, see each other, and cannot double-complete a task. |
| Tutorial/UI | Fresh player can complete first loop without role-specific instructions. |
| MVP release | New build passes a clean-launch, 30-day, endless-mode, and multiplayer smoke test. |

## 9. Suggested Team Ownership

For a three-person team:

| Owner | Primary responsibility | Secondary responsibility |
| --- | --- | --- |
| Developer A | Core flow, save, campaign, task contracts | Build and release checks |
| Developer B | Restaurant scene, bots, customer flow, interactions | Performance and scene wiring |
| Developer C | UI, tutorials, menus, multiplayer UI, variants | Test cases and documentation |

Multiplayer logic should have one designated owner, even if all teammates test it.

## 10. AI Credit-Aware Workflow

Codex cannot read the account's remaining credit balance from the Unity workspace. The team should record the available balance manually before each sprint.

Suggested usage budget:

| Credit allocation | Share | Best use |
| --- | ---: | --- |
| Architecture and code audits | 15% | Dependency mapping, refactor plans, risk reviews |
| Targeted implementation | 45% | One bounded feature or bug at a time |
| Debugging and verification | 25% | Console errors, profiler evidence, scene reference validation |
| Reserve | 15% | Late integration blockers and build failures |

Use credits efficiently:

- Give Codex one bounded objective at a time, with the target scene/script named.
- Request a workspace scan before large moves or deletions.
- Use AI for source analysis, code changes, test planning, serialized-reference risk checks, and difficult bugs.
- Use Unity Inspector manually for repeated drag-and-drop assignments, simple layout edits, and visual tuning.
- Do not spend credits on broad rewrites before the P0 architecture decisions are locked.
- After every phase, record: completed scope, Console state, build result, actual hours, remaining credit, and next risk.

Credit checkpoints:

```text
More than 50% remaining: Continue MVP implementation normally.
30-50% remaining: Freeze P2 features and finish the playable MVP only.
Less than 30% remaining: Use credits only for blockers, verification, and release-critical bugs.
Less than 15% remaining: Avoid refactors; stabilize, test, document, and build.
```

## 11. Immediate Next Sprint

Recommended next two-week sprint:

1. Freeze the current RoleBased scenes except for blocking bug fixes.
2. Audit and select the single save owner.
3. Write the manager interaction/task contract before moving gameplay code.
4. Create an empty `MainRestaurant.unity` with player spawn, dining area, kitchen area, and bot navigation zones.
5. Migrate one interaction from the legacy scenes into the shared task system.
6. Add one bot that can claim the same task the player can claim.
7. Run a clean-launch and save/load test at the end of the sprint.

The first bot-and-task vertical slice is the decision point. If it is stable, the one-scene management design is technically viable. If it is not, reduce the first milestone to fewer bot roles before expanding content.
