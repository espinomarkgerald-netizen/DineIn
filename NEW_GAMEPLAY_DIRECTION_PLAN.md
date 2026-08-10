# Dine In - New Gameplay Direction Plan

Planning date: 10 August 2026  
Purpose: capture the updated gameplay direction and show how it changes the project architecture.

This document is a design and architecture planning document only. It does not include code changes.

## 1. Core Direction

### Previous Gameplay Model

The older structure was based on separate roles and phase-based scene flow:

- player switches between waiter, cashier, busser, kitchen, and other roles
- gameplay is split across Office, Lobby, and Kitchen scenes
- morning and afternoon shifts exist as separate flow phases
- alien approval is a persistent long-term pressure system
- tutorials teach role-specific workflows

### New Gameplay Model

The new structure is management-focused:

- the player is the management itself
- players do not switch roles
- bots take over restaurant roles by default
- players can help with any task the bots can do
- gameplay should happen in one main restaurant scene
- there is no separate morning and afternoon shift structure
- multiplayer players are all managers/helpers, not locked roles

### Design Summary

```text
Player = Manager
Bots = Staff roles
Scene = One active restaurant scene
Campaign = 30 days
Post-campaign = endless play
Multiplayer = up to 4 unrestricted managers
```

## 2. Mechanics

### Remove Role Switching

Role switching should no longer be part of the main game.

Systems to remove, disable, or heavily simplify:

- role switching UI
- role camera switching
- role locks
- role-based player restrictions
- tutorials that teach role switching
- gameplay logic that checks whether the player is currently a waiter, cashier, busser, or kitchen worker

New rule:

```text
The player can interact with any valid restaurant task if they are close enough and the task is available.
```

### Bot-Driven Restaurant Roles

Bots should handle the normal restaurant workflow:

- greet customers
- seat customers
- take orders
- cook/prep food
- serve food
- clean tables/trays
- cashier/payment work
- restock or support stations if needed

The player acts as management and can manually assist these workflows.

### One-Scene Gameplay

The gameplay should eventually be represented by one active restaurant scene.

This means:

- no separate Office -> Lobby -> Kitchen gameplay loop
- no morning/afternoon scene split
- management, dining floor, and kitchen should be accessible in one scene
- systems should be active together instead of loaded as separate phases

Possible future structure:

```text
MainRestaurantScene
  Management Area
  Dining Area
  Kitchen Area
  Staff/Bot Navigation
  Customer Flow
  Multiplayer Player Spawn Points
```

### Endless Campaign

The campaign is limited to 30 days.

During Days 1-30:

- alien approval matters
- the player must meet approval requirements
- approval can be part of win/loss pressure

After Day 30:

- the player can continue playing the first restaurant
- alien approval no longer matters
- approval UI and approval penalties can be hidden or disabled
- the restaurant becomes endless/sandbox-style

Recommended rule:

```text
if currentDay <= 30:
    approval is active
else:
    approval is inactive
```

## 3. Expansion Plan

Expansion should reuse the same gameplay systems.

Only these should change between restaurant types:

- menu
- theme
- aesthetics

Core mechanics should stay the same.

### Restaurant Types

#### Fast Food

Changes:

- remove receptionist
- use the current casual dining menu
- faster, simpler service theme

Purpose:

- fast-paced restaurant variant
- less formal service flow

#### Casual Dining

Changes:

- replace current menu with a new casual dining menu
- update casual dining aesthetics/theme

Purpose:

- becomes the standard balanced restaurant experience

#### Fine Dining

Changes:

- use a fine dining menu
- use a fine dining visual theme
- more premium aesthetic

Purpose:

- higher-end restaurant variant

### Expansion Architecture Rule

Do not duplicate restaurant systems per expansion.

Use shared systems plus restaurant-specific data:

```text
RestaurantConfig
  restaurantType
  menuItems
  themeSettings
  aestheticPrefabSet
  customerPresentationRules
```

Recommended restaurant data assets:

- `FastFoodRestaurantConfig`
- `CasualDiningRestaurantConfig`
- `FineDiningRestaurantConfig`

## 4. Multiplayer

### Target Rule

Multiplayer supports up to 4 players.

```text
Max players: 4
Role constraints: none
```

### New Multiplayer Identity

All players are managers/helpers.

No player should be locked into:

- waiter
- cashier
- busser
- chef
- receptionist

### Multiplayer Interaction Rule

Any player can help any available task.

Required safeguards:

- prevent two players from completing the same task at the same time
- lock or reserve task ownership while one player/bot is handling it
- release task ownership if the player walks away, disconnects, or cancels
- sync task state across clients

Recommended concept:

```text
RestaurantTask
  taskType
  taskState
  assignedWorker
  assignedPlayer
  isReserved
  completionProgress
```

## 5. Models And Cosmetics

### Character Body Plan

Use two base bodies:

- 1 male body
- 1 female body

Cosmetics should be interchangeable where possible.

### Cosmetic System Direction

Cosmetics should attach to shared body slots.

Possible slots:

- head
- hair
- hat
- face accessory
- upper body
- lower body
- shoes
- backpack/accessory

Architecture goal:

```text
BaseBody + CosmeticSlots = Character Appearance
```

This allows fewer base models while still supporting visual variety.

## 6. Currency Uses

Currency should support progression, monetization-style rewards, and convenience mechanics.

Planned uses:

- cosmetics
- power ups
- revive
- conversion of gold to normal currency
- bundles

### Currency Types

Recommended distinction:

```text
Normal Currency
  earned through gameplay
  used for regular upgrades, restocks, basic items

Gold / Premium Currency
  rarer currency
  used for cosmetics, bundles, revive, premium convenience
  can convert into normal currency
```

### Currency Architecture Notes

Currency should not be hardcoded into UI scripts.

Recommended services:

- `CurrencyWallet`
- `CurrencyTransactionService`
- `ShopCatalog`
- `BundleCatalog`
- `CosmeticInventory`

## 7. UI Direction

### Standardize UI

The UI should use one consistent visual system across:

- main menu
- management UI
- task UI
- bot/staff UI
- day results
- currency/shop UI
- multiplayer UI
- tutorial UI

### Recommended UI Categories

```text
Global UI
  loading
  pause/settings
  notifications
  currency display

Gameplay UI
  task prompts
  bot status
  customer state
  day timer/progress
  approval during Days 1-30

Management UI
  staff/bot overview
  restaurant status
  menu management
  upgrades

Shop UI
  cosmetics
  power ups
  bundles
  currency conversion

Tutorial UI
  standardized dialogue
  highlight arrows
  objective checklist
```

### UI Rule

Avoid one-off UI systems per role.

Because role switching is removed, UI should be task-based and management-based instead.

## 8. Tutorial Direction

### Standardize Tutorial

The tutorial should teach the new management model.

Remove or rewrite tutorials that teach:

- role switching
- separate role control
- morning/afternoon shift flow
- scene-specific role phases

### New Tutorial Topics

Recommended tutorial flow:

1. Basic movement and camera.
2. Understanding bots/staff.
3. Helping with any task.
4. Customer flow.
5. Kitchen/food flow.
6. Payment and cleaning flow.
7. Management overview.
8. Approval system during campaign days.
9. Multiplayer cooperation basics.

### Tutorial Rule

Tutorial logic should be based on task completion, not role identity.

Example:

```text
Old: switch to cashier role and open register
New: interact with the register task and complete payment
```

## 9. Architecture Impact

### Systems To Remove Or Downgrade

- `RoleManager` role-switch behavior
- role-specific camera switching
- role switching warning UI
- role-based assignment restrictions
- tutorial role switching steps
- morning/afternoon split in `GameFlowManager`
- scene flow that depends on separate Office, Lobby, and Kitchen gameplay phases
- alien approval behavior after Day 30

### Systems To Keep But Rework

- customer flow
- order flow
- kitchen flow
- payment flow
- cleaning flow
- save/load
- day progression
- multiplayer spawning/sync
- player customization
- UI helpers
- tutorial framework

### New Systems Needed

- `RestaurantTask` system
- task reservation/ownership system
- bot worker system
- manager/player assist system
- restaurant config system
- expansion theme/menu config
- post-Day-30 endless mode rule
- standardized UI style guide
- standardized tutorial objective system

## 10. Updated Refactor Priorities

### P0 - Foundation

- Remove role switching as a core gameplay dependency.
- Replace role-based interaction checks with task-based interaction checks.
- Redesign `GameFlowManager` around one scene, 30 campaign days, and endless post-campaign play.
- Create a task ownership model that works for both bots and players.
- Decide which scene becomes the one main restaurant scene.

### P1 - Gameplay Systems

- Rework bots to own restaurant roles automatically.
- Let players assist any task without role restrictions.
- Rework customer, order, kitchen, payment, and cleaning systems around shared tasks.
- Rework multiplayer so all players are unrestricted managers/helpers.
- Prevent duplicate task completion between bots and players.

### P2 - Progression And Expansion

- Add restaurant config data for Fast Food, Casual Dining, and Fine Dining.
- Move menu and theme data out of hardcoded scripts.
- Define 30-day campaign rewards/loss conditions.
- Disable or hide alien approval after Day 30.
- Build currency use cases for cosmetics, power ups, revive, conversion, and bundles.

### P3 - Presentation

- Standardize UI.
- Standardize tutorial.
- Standardize cosmetic slots across male/female body models.
- Clean up old role-specific UI and tutorial content.

## 11. Key Design Rule

The new game should be task-based, not role-based.

```text
Bots perform roles.
Players manage and assist tasks.
Multiplayer players are unrestricted helpers.
Restaurant expansions swap data and theme, not core mechanics.
```

This rule should guide future refactors.
