# Dine In — Active Bug Backlog

**Last updated:** 20 August 2026  
**Status:** Planning only. These items are not authorization to implement unrelated changes.

## P0 — Gameplay and save integrity

### 1. Valid interaction clicks can be ignored

**Observed:** Clicking or tapping an allowed interactable does not always make the player move toward it or begin the interaction. Small and overlapping world-space controls make this worse on mobile.

**Expected:** A click on the highest-priority valid interactable must always be detected, assign a valid destination, and make the player approach it. Invalid UI or overlapping world objects must not consume the input.

**Acceptance criteria:**

- [ ] Pickup items, customer bubbles, booths, bills, payments, trays, equipment, and role actions respond consistently.
- [ ] One physical tap produces one action only.
- [ ] The nearest/highest-priority valid target wins when targets overlap.
- [ ] Movement failure restores the interaction instead of silently discarding it.
- [ ] Works with mouse, Android touch, and world-space UI.

### 2. Abandoned days are not rolled back correctly

**Reproduction:** Start Day 2, buy stock or change money, leave before serving the final customer, pause, return to the main menu, and load the game again.

**Expected:** The unfinished day restarts from its day-start checkpoint. Stock purchases, inventory consumption, earnings, spending, customer progress, and other changes from the abandoned attempt are rolled back. Only a completed day commits permanent day progress.

**Acceptance criteria:**

- [ ] Loading restarts the same unfinished day from the beginning.
- [ ] Money equals the amount saved at the start of that day.
- [ ] Inventory and purchased stock equal the day-start snapshot.
- [ ] Approval, objectives, customers, orders, and temporary tasks reset cleanly.
- [ ] Unlocks and restaurant progression from previously completed days remain intact.
- [ ] Completing the day creates the next valid checkpoint.

## P1 — Mobile usability

### 3. All mobile UI is too small

**Observed:** The UI is acceptable on PC but undersized in the Android build. Text, buttons, cards, HUD controls, menus, and interaction prompts are difficult to read or tap.

**Expected:** Mobile uses responsive sizing and touch targets without making the PC interface oversized.

**Acceptance criteria:**

- [ ] Interactive controls have at least approximately 48dp touch areas.
- [ ] Visual size and invisible hit area are both increased where needed.
- [ ] HUD, pause menu, results, notepad, computer, and modal windows respect the safe area.
- [ ] Text remains readable without overlap, clipping, or excessive shrinking.
- [ ] PC retains an appropriately compact layout.

### 4. World-space UI is too small and easy to mis-tap

**Affected UI:** Customer bubbles, pickup buttons, bills, money, trays, role prompts, interaction popups, and other world-following actions.

**Acceptance criteria:**

- [ ] Prompts remain readable at the normal gameplay camera zoom.
- [ ] Touch areas are larger than the visible artwork where necessary.
- [ ] Prompts do not overlap or block a higher-priority interaction.
- [ ] Off-screen and edge prompts remain inside the mobile safe area.
- [ ] Multiple nearby customers do not cause random target selection.

## P1 — Customer animation

### 5. Eating particles are not visible

**Observed:** Eating animations play, but the intended bite/crumb particles are still not visibly appearing.

**Acceptance criteria:**

- [ ] Particles appear consistently in Editor, Windows, and Android builds.
- [ ] Particles are visible at the normal camera distance without becoming distracting.
- [ ] The effect uses a build-included material and does not require manual scene setup.
- [ ] The old eating bubble remains disabled while the particle presentation is active.

### 6. Aliens do not clearly take food from the tray or plate

**Observed:** Procedural eating motion exists, but the hand does not convincingly grab a visible bite from the delivered food.

**Acceptance criteria:**

- [ ] The active hand reaches the actual food/plate position.
- [ ] A visible bite or food piece travels with the hand to the mouth.
- [ ] The hand returns naturally after the bite.
- [ ] Group members use slightly different speeds, phases, and food targets.
- [ ] The animation does not stretch, twist, or detach humanoid limbs.

## P2 — Results presentation

### 7. Day Results presentation needs polish

**Observed:** The panel can take too long to appear, popup motion is unclear, the star animation may not play, and authored star sizes may be overridden at runtime.

**Acceptance criteria:**

- [ ] Results appear promptly after the day ends.
- [ ] The panel has a clear, short opening transition.
- [ ] Stars animate in sequence and respect prefab-authored sizes.
- [ ] Runtime layout does not overwrite intentional Inspector edits.
- [ ] Text dynamically fits without overlapping buttons or statistics.

## P2 — Rendering direction

### 8. Cozy toon rendering does not match the intended art direction

**Observed:** The current experimental toon system can look excessively blue/tinted and resembles a full-screen filter rather than authored cartoon materials.

**Expected:** A cozy indie/cartoon/anime appearance driven by materials, lighting ramps, shadows, highlights, and optional outlines—not a global blue/yellow tint.

**Acceptance criteria:**

- [ ] Original material colors remain recognizable.
- [ ] No unwanted global blue or yellow cast.
- [ ] The effect is visible in Edit Mode and Play Mode.
- [ ] One shared settings inspector can tune the global look.
- [ ] UI is excluded from model shading.
- [ ] Windows and Android produce a consistent result.

## Planned UI redesign — not a bug fix by itself

### 9. Notepad vertical cards

- [ ] Reorganize each product into a vertical card without enlarging the entire window.
- [ ] Show image, name, price, availability/stock, and large `− / quantity / +` controls.
- [ ] Show bundle contents clearly.
- [ ] Use categories and scrolling that remain comfortable on mobile.
- [ ] Preserve the Dine In blue, playful aesthetic.

### 10. Management-computer cards and recipe clarity

- [ ] Use vertical cards for Menu and Restock entries.
- [ ] Clearly show which ingredients produce each menu item, including owned/required quantities.
- [ ] Keep Equipment and information-light entries as taller horizontal rows.
- [ ] Add clear categories, search/filter controls, and consistent icon placement.
- [ ] Make the content feel larger through organization rather than increasing the whole panel.

### 11. UI transitions and feedback

- [ ] Add short window pop-in and close transitions.
- [ ] Add restrained card stagger/fade animations.
- [ ] Animate category expand/collapse and detail-page changes.
- [ ] Add obvious button press, selected, disabled, and purchase feedback.
- [ ] Keep animations responsive and avoid delaying gameplay actions.

## Fixed items to regression-test

These were previously addressed but must be checked in future builds:

- [ ] Build characters remain on the floor and never treat counters or walls as NavMesh.
- [ ] Staff and player models remain upright in Windows and Android builds.
- [ ] Next Day advances correctly after the results screen.
- [ ] Bill, money, and customer task claims recover after cancelled or failed movement.
- [ ] Angry customers leave through the exit instead of instantly disappearing.
- [ ] Queue positions compact immediately after a customer group leaves.
- [ ] Management-computer clicks do not hire or purchase twice.

## Bug report template

```text
Date:
Build target: Editor / Windows / Android
Scene:
Day:
Role / player count:
Steps to reproduce:
Expected result:
Actual result:
First Console error or warning:
Screenshot or video:
Status: Open / Investigating / Fixed / Retest
```
