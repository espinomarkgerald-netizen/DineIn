# Casual Dining Level 1 Polish Plan

Status: Implemented and regression-tested  
Scope: Casual Dining restaurant only  
Priority work: #4 Employee Progression, #5 Supplier Economy and Reviews, #6 Restock Persistence and Advanced Storage, #13 UI and Accessibility, plus the Daily Alien Newspaper

Implementation state: **Approved options implemented for Casual Dining Level 1.** The systems use additive managers and schema-3 save migration, while the existing approval, finance, inventory, day flow, management computer, and authored Restock scene remain authoritative.

Validated on 24 August 2026:

- Unity script compilation completed without C# errors.
- Management Computer/restock smoke test passed.
- Full Lobby1 Day 1 → newspaper gate → service → results → Day 2 smoke test passed.
- Bootstrap, Lobby1, and RestockScene remain enabled in build settings.
- User-owned RestockScene, skybox, and Anton font edits were preserved.

## 1. Goal and scope lock

The next development milestone is to make the existing Casual Dining restaurant feel complete, readable, persistent, and worth replaying before expanding the game.

This milestone includes:

- A meaningful employee progression loop.
- Daily supplier price changes, restaurant ratings, and result-based reviews.
- Reliable restock-room persistence and improved storage decisions.
- One consistent UI and accessibility standard across the Level 1 experience.
- A mandatory daily alien newspaper during preparation that explains what happened yesterday and what matters today.

The following remain out of scope until Casual Dining Level 1 passes its polish checklist:

- Tutorials and onboarding campaign work.
- Fast Food, Fine Dining, and other restaurant types.
- Multiplayer.
- Systems that exist only to support those deferred modes.

Existing day flow, approval, finance, employees, inventory, ordering, and management-computer systems will be extended rather than replaced.

## 2. Target daily player experience

Each new restaurant day should follow this sequence:

1. The game enters the Preparation phase.
2. Yesterday's results are converted into a saved daily snapshot.
3. Today's supplier prices and newspaper issue are prepared from that snapshot.
4. The newspaper button appears with an unread indicator and a gentle pulse.
5. The Open Restaurant button is disabled with the message: “Read today's Galactic Gazette first.”
6. The player opens the newspaper. It spins, grows, overshoots slightly, and slaps flat onto the screen.
7. The player reads yesterday's story, approval, rating, incidents, customer comment, market prices, and Alien Boss advice.
8. Once the paper has fully opened, the issue is marked as viewed. The player may then close it and continue preparation.
9. The player reviews stock, deliveries, menu prices, and staff assignments, then opens the restaurant.
10. The completed day records operational results for tomorrow's issue.

Opening the newspaper is required; forcing the player to scroll every line is not. This keeps the daily ritual visible without turning it into a frustrating blocker.

## 3. Daily Alien Newspaper

Working title: **The Galactic Gazette**. The title can be changed later without changing the system.

### 3.1 Newspaper layout

The issue should resemble a clean, cartoonish physical newspaper rather than a normal game menu.

Recommended page order:

1. **Masthead** — newspaper title, issue number, restaurant day, and alien correspondent name.
2. **Lead Story: Earth Restaurant Watch** — an alien-narrated explanation of the current Alien Approval and yesterday's change.
3. **Restaurant Rating** — the current 1–5 star rating, its change, and a short reason.
4. **Yesterday at the Diner** — customers served, customers who left angry, failed orders, payment mistakes, and the most important incident.
5. **Voice from the Queue** — one result-based customer quote.
6. **Market Watch** — raw product price increases, decreases, and important unchanged prices.
7. **Alien Boss Advisory** — specific advice based on the largest operational problem.
8. **Optional small column** — Staff Spotlight, low-stock warning, expiring inventory, or a positive achievement.

The paper uses a responsive `ScrollRect`:

- Desktop: mouse wheel, drag, scrollbar, Escape to close.
- Touch: swipe and a large close button.
- Controller support can be added during the input-standardization pass if controller is part of the Level 1 build target.
- Wide screens may use two columns; narrow screens use one readable column.

### 3.2 Visual direction

- Use Times New Roman for the masthead, headlines, body, bold, and italic variants as requested.
- Create TextMesh Pro font assets and verify the font's distribution rights before packaging the final build.
- Use warm off-white newsprint, near-black ink, dark red or muted blue accents, ruled dividers, halftone details, and simple cartoon star icons.
- Keep distress and paper texture subtle so small text stays readable.
- Use a separate fallback TMP font only for symbols that Times New Roman does not contain.
- Build the layout as an authored prefab with shared style tokens; do not assemble the whole paper through one large runtime script.

### 3.3 Opening animation

The button begins as a small folded-paper icon in the Preparation UI.

On click:

1. The background dims.
2. The folded paper spins approximately 1–1.25 turns while scaling from small to full size.
3. It grows slightly past its final size.
4. It slaps flat with a brief squash, shadow impact, paper sound, and optional light camera/UI shake.
5. Input is unlocked when the final open state is reached.

The animation should last about 0.6–0.9 seconds and use unscaled time. This prevents a paused game clock, preparation state, or focus change from permanently freezing the transition. A watchdog must force the final open state if the animation callback is interrupted.

Accessibility requirements:

- A Reduced Motion option replaces the spin and slap with a short fade/scale.
- Clicking again, pressing Escape, or losing window focus cannot leave a full-screen blocker stuck onscreen.
- Closing and reopening the paper shows the same saved issue; it never rerolls.

### 3.4 Data that feeds each issue

At the end of a day, create a `DailyRestaurantSnapshot` containing at least:

- Restaurant day and issue number.
- Alien Approval before, after, and change.
- Restaurant rating before, after, and change.
- Groups arrived and groups seated.
- Customers served, happy, neutral, and angry.
- Groups/customers not accommodated.
- Orders completed and failed.
- Long-wait incidents, wrong orders, and stockout refusals.
- Cash/payment mistakes.
- Revenue, ingredient cost, wages, other costs, and profit.
- Items discarded because they expired.
- Important low-stock or out-of-stock ingredients.
- Staff assigned and their daily performance summaries.
- Today's raw product price changes.

Some current systems already record broad totals such as happy, neutral, angry, completed orders, failed orders, cash errors, and finance results. Add cause-specific incident counters so the newspaper can truthfully say why customers were unhappy. Do not infer “not accommodated” from every angry customer.

Recommended incident categories:

- `Unaccommodated`
- `WaitedTooLong`
- `WrongOrder`
- `OrderFailed`
- `PaymentError`
- `DirtyTableDelay`
- `StockoutRefusal`
- `TakeoutFailure`

### 3.5 Dynamic writing and non-repetition

The newspaper should be generated offline from authored template pools, not from a network AI service. This makes it fast, predictable, save-safe, and available in every build.

Create a `NewspaperTemplateLibrary` ScriptableObject containing tagged entries for:

- Headlines.
- Approval stories: hostile, concerned, neutral, impressed, and delighted.
- Restaurant-rating stories.
- Each incident category and severity.
- Customer quotes for positive, neutral, and negative outcomes.
- Alien Boss advice.
- Market price commentary.
- Staff and stock side stories.

Every template has a stable ID, compatible conditions, tone, minimum/maximum value, and token placeholders such as customer count, ingredient name, percentage, and star rating.

Non-repeat rules:

- Save the selected template IDs with the issue.
- Hard-exclude recently used templates for each section for at least five issues.
- Track full issue combinations across the campaign so the exact same paper is not produced twice.
- When a small pool is exhausted, choose the least recently used compatible entry instead of immediately repeating yesterday.
- Use a stable campaign/day seed so saving and reloading cannot change the issue.
- Apply correct singular/plural grammar and never show a numeric claim that is absent from the snapshot.
- Author enough variations to support at least 30 days without repeating a complete message combination.

The alien narrator should have a recognizable voice: observant, slightly dramatic, amused by humans, and still useful. It may criticize the restaurant, but its advice must always tell the player what can be improved.

Example structure, not final copy:

> Four visitors departed before receiving a table yesterday. Galactic management advises the humans to increase seating turnover and assign a stronger host before tonight's crowd arrives.

### 3.6 Alien Boss advice selection

Advice is chosen from the highest-impact weak metric, with deterministic tie-breaking:

1. Customers not accommodated.
2. Failed or wrong orders.
3. Extreme waiting time.
4. Cash errors.
5. Stockouts or spoiled inventory.
6. Poor staff performance or missing role coverage.
7. Weak profit caused by pricing or cost.

If no metric is poor, the Alien Boss gives positive reinforcement and suggests the next improvement instead of inventing a problem.

### 3.7 Day 1 and endless play

- Day 1 has no previous-day results, so it uses a welcome issue that explains the current restaurant, starting rating, starting approval, and today's market.
- Endless days continue receiving newspapers.
- If Alien Approval is no longer an active win/loss pressure in endless play, the approval column becomes a legacy/status story instead of showing false stakes.

### 3.8 Save data

Add a saved `NewspaperIssueEntry` containing:

- Day and issue ID.
- Deterministic seed.
- Source daily snapshot.
- Chosen template IDs.
- Resolved numeric values and relevant item/employee IDs.
- Viewed state.
- Recently used template history.

An issue must remain identical after closing the game, returning from restock, or reloading the Lobby scene.

## 4. Workstream #4 — Complete employee progression

### Player-facing outcome

Employees become people the player develops, not temporary numbers in the management screen. Good assignments and repeated work improve them, while poor role coverage creates visible consequences.

### Required work

1. Give every employee and applicant a stable ID.
2. Add saved progression fields:
   - Experience points.
   - Experience level or tier.
   - Days employed and days worked.
   - Recent performance score and trend.
   - Role experience for the role they actually performed.
3. Record role-specific daily performance:
   - Host: queue time, seating success, unaccommodated groups.
   - Waiter: response time, correct deliveries, missed orders.
   - Cashier: transaction speed and payment errors.
   - Busser: cleanup time and dirty-table delays.
   - Chef/Barista: production success, speed, and failed products.
4. Award experience only to employees who were assigned and worked that day.
5. Improve speed, accuracy, reliability, or performance multiplier at controlled thresholds.
6. Cap progression so high-level employees help without making the restaurant automatic.
7. Recalculate salary after a promotion and show the player the new wage before the next day opens.
8. Refresh the applicant pool on a weekly schedule instead of every time the screen is opened.
9. Save applicant IDs and expiration/refresh day so reloads cannot reroll candidates.
10. Add Staff Spotlight or promotion news to the newspaper when relevant.

### Balance rules

- Low-level employees may be slower or make occasional mistakes, but they must not break the core loop.
- Attribute effects should use bounded multipliers.
- A promotion must feel noticeable and have a clear cost tradeoff.
- Firing, hiring, promotion, assignment, payroll, and reload must never duplicate or lose an employee.

### Acceptance criteria

- An assigned employee gains the correct role experience after a completed day.
- An unassigned employee gains no work experience.
- A promotion persists through scene changes and a full save/reload.
- Salary changes are visible before the player commits to opening the next day.
- Applicant refresh occurs only on its scheduled day and is deterministic across reloads.
- Newspaper staff stories use the actual promoted or top-performing employee.

## 5. Workstream #5 — Supplier economy, rating, and reviews

### 5.1 Supplier market

Create a `SupplierMarketManager` that owns current raw-product prices and daily market history.

Required behavior:

- Every ingredient retains a base price and a current market multiplier.
- At the start of a day, a small configurable number of ingredients change price.
- Price changes use safe minimum/maximum bounds and avoid extreme random spikes.
- The daily result is deterministic from the campaign/day seed.
- Restock checkout uses the current price.
- An order stores a price snapshot when purchased, so an in-transit order never changes total afterward.
- Current prices and price history persist through save/load.
- The newspaper's Market Watch lists useful changes with arrows, percentage/value change, and a one-line alien comment.
- The management computer clearly distinguishes base price, current price, and changed price.

### 5.2 Restaurant rating

Create a `RestaurantRatingManager` separate from Alien Approval.

- Alien Approval measures the campaign's alien attitude and story pressure.
- Restaurant Rating measures operational quality and is displayed from 1 to 5 stars.

The daily rating calculation should consider:

- Customer satisfaction.
- Service completion.
- Waiting and accommodation failures.
- Order accuracy.
- Payment accuracy.
- Ingredient availability and spoilage.
- Cleanliness/table turnover when those measurements are available.

Use smoothing and a capped daily change so one unusual day cannot jump from one star to five. Save the numeric rating and daily history. Display full, half, and empty stars if half-star precision is used.

### 5.3 Result-based reviews

Create one to three short reviews from actual visits after each completed day.

- Reviews use customer outcomes and incident causes from the daily snapshot.
- A good review mentions a real strength.
- A bad review mentions a real failure.
- Review templates have stable IDs and the same non-repeat history rules as newspaper text.
- Reviews remain in saved history and may be viewed from the management computer.
- The newspaper summarizes the rating and may print one review as “Voice from the Queue.”

### Acceptance criteria

- The restock screen, checkout total, delivery record, and newspaper agree on ingredient prices.
- Reloading does not reroll a market day or review.
- Approval and rating can move differently when their inputs justify it.
- Every printed review is traceable to a real result from the previous day.
- A 30-day simulation never produces an identical complete set of daily reviews.

## 6. Workstream #6 — Restock persistence and advanced storage

### Player-facing outcome

Every physical box remains where the player left it, every batch keeps its real freshness, and storage decisions affect spoilage and tomorrow's newspaper.

### Required persistence model

Give each shelf a stable `ShelfId` and save every placed container with:

- Container ID and inventory batch ID.
- Ingredient/item ID.
- Shelf ID.
- Grid column and row.
- Orientation/rotation.
- Storage type.
- Units remaining.
- Received day and expiration day.
- Whether it is stored in the wrong environment.

Load order:

1. Restore inventory batches.
2. Restore restock-order state.
3. Restore shelf grids.
4. Recreate each physical container at its exact saved cell.
5. Reconcile hotbar/carried containers so one box cannot exist in two places.

### Advanced storage rules

- Moving a box never resets its freshness.
- Correct storage preserves the configured shelf life.
- Wrong storage is allowed only after a clear confirmation and applies a configurable accelerated spoilage rate.
- Wrong-storage boxes show a visible warning, not color alone.
- Expired stock is excluded from recipes.
- Discarding expired stock removes the exact batch, removes the exact physical box, and frees the shelf cells.
- Partial boxes preserve remaining units when moved, saved, delivered, or discarded.
- Leaving the restock scene and returning to Lobby must complete or safely cancel its iris transition and cannot leave an input-blocking overlay.

### Forecast and recommendations

Add a simple preparation forecast using expected customers and recipe requirements:

- Estimated demand per raw product.
- Quantity in correct storage.
- Quantity in wrong storage.
- Quantity expiring soon.
- Incoming delivery quantity.
- Status: Low, Enough, Overstocked, or Spoilage Risk.

The recommendation is advisory. It should not automatically buy products.

### Acceptance criteria

- Boxes return to the same shelf cells after Lobby → Restock → Lobby → Restock.
- A partial batch retains units and expiration date after save/reload.
- A box can never appear both on a shelf and in the hotbar.
- Wrong storage changes spoilage predictably and is clearly communicated.
- Expired stock cannot satisfy a recipe.
- Discarding a batch updates inventory, shelf occupancy, daily waste totals, finance where applicable, and tomorrow's newspaper.
- Restock close, freezer, shelf, and navigation buttons work in a Windows build, not only in the editor.

## 7. Workstream #13 — UI and accessibility standardization

### Shared Level 1 UI system

Define one style guide and reusable components for:

- Colors and contrast.
- Times New Roman newspaper typography and the normal game UI typeface.
- Heading/body/caption sizes.
- Spacing and panel margins.
- Primary, secondary, warning, destructive, disabled, focused, and pressed button states.
- Cards, tooltips, unread badges, modal dimmers, scrollbars, and transitions.
- Safe areas and responsive breakpoints.
- Minimum mouse/touch target sizes.

Apply it to:

- Preparation HUD.
- Gameplay HUD and patience bars.
- Results screen.
- Pause and settings UI.
- Management computer.
- Employee screens.
- Restock/delivery/storage screens.
- Newspaper.

### Required behavior

- Support the target desktop resolutions and windowed/fullscreen modes.
- Support safe areas and the existing mobile layout where mobile remains a build target.
- Never rely only on red/green color to communicate status.
- Provide readable text scale options.
- Provide Reduced Motion.
- Keep important buttons above screen edges and system safe areas.
- Standardize hover, focus, click, disabled, and selected states.
- Prevent legacy or inactive prefab UI from becoming visible for a frame during scene load.
- Initialize patience bars while hidden, then reveal them only after their final size/value is assigned.
- Use unscaled time for full-screen UI transitions that must work while gameplay time is paused.
- Restore UI after window minimize/focus return; a minimized window not rendering is normal, but returning to it with missing or blocked UI is a bug.

### Acceptance criteria

- No UI appears for a single frame in the wrong position or at the wrong scale.
- Patience bars do not expand to a full/incorrect width during initialization or reuse.
- Every Level 1 button has visible hover/focus/pressed/disabled feedback.
- All restock and newspaper buttons work with mouse in a Windows build.
- Text remains readable at supported aspect ratios without clipping.
- Losing and restoring focus cannot leave an iris, dimmer, or modal blocker active.
- Reduced Motion is honored by the newspaper and scene transitions.

## 8. Technical ownership and integration

Extend these current owners instead of adding duplicate managers:

| Concern | Existing owner/integration point | Planned addition |
|---|---|---|
| Preparation and day state | `GameFlowManager` | Newspaper gate and daily setup hook |
| Daily customer results | `GameDayManager` | Cause-specific incident counters and snapshot export |
| Approval | `AlienApprovalManager` | Approval values supplied to snapshot; no replacement |
| Orders and revenue | `DailyRevenueTracker` and finance bridge | Snapshot totals and profit story inputs |
| Employees | `EmployeeManager` and `EmployeeData` | Stable IDs, progression, weekly applicants, daily performance |
| Saving | `GameSaveData` | Newspaper, rating, market, review, progression, and shelf entries |
| Inventory batches | `InventoryManager` | Exact batch reconciliation and waste reporting |
| Deliveries | `RestockOrderManager` | Purchase price snapshot and restored state |
| Physical storage | `ShelfGrid` and `RestockStorageContainer` | Stable shelf/cell persistence and wrong-storage state |
| Menu/restock UI | Management computer | Market prices, rating/review history, forecast |
| Responsive UI | Existing responsive/accessibility helpers | Shared Level 1 style and behavior pass |

New focused services may be added where there is no current owner:

- `DailyRestaurantSnapshotBuilder`
- `DailyNewspaperManager`
- `NewspaperTemplateLibrary`
- `RestaurantRatingManager`
- `RestaurantReviewManager`
- `SupplierMarketManager`

These services should communicate through saved data and events, not find one another repeatedly by scene name.

## 9. Save migration and safety

This milestone changes saved data, so migration is part of the feature rather than a final cleanup task.

- Increase the save schema version.
- Give old saves safe defaults for rating, employee progression, market prices, review history, newspaper state, and shelf placement.
- If an old save has inventory but no physical placement, place valid boxes into the first compatible free cells and report any overflow to a recovery list rather than deleting it.
- Generate the current day's paper once after migration and save it immediately.
- Save the end-of-day snapshot before the scene can move to the next day.
- Treat inventory, hotbar, and shelf placement as one transaction so a failure cannot duplicate stock.
- Keep a recoverable previous-day checkpoint for validation during development.

## 10. Delivery order

### Phase 0 — Scope and data contracts

- Lock the Level 1 metrics and definitions.
- Add schema/version plan and stable IDs.
- Decide rating weights, price bounds, employee caps, and storage spoilage multiplier in configurable assets.
- Prepare the shared UI tokens.

Exit: the daily snapshot and save structures are agreed and compile with safe defaults.

### Phase 1 — Daily snapshot and newspaper shell

- Capture the previous day's results.
- Add Preparation newspaper button, unread state, open/close UI, scroll view, and animation watchdog.
- Add the mandatory view gate.
- Add Day 1 issue and deterministic issue saving.

Exit: every day starts with a readable saved paper, even before all optional columns are connected.

### Phase 2 — Supplier market, rating, and reviews

- Implement daily market prices and price-at-purchase snapshots.
- Implement separate 1–5 star restaurant rating.
- Implement result-based review generation and history.
- Connect all three to the newspaper and management computer.

Exit: the paper truthfully explains rating and prices; reload produces the same issue.

### Phase 3 — Employee progression

- Add role performance capture, experience, promotions, wage changes, and weekly applicants.
- Connect employee outcomes to gameplay and Staff Spotlight.

Exit: an employee can progress across several days and remain correct after reload.

### Phase 4 — Restock persistence and advanced storage

- Save shelf positions and reconcile physical boxes with inventory.
- Add wrong-storage spoilage, expiration visibility, exact-batch discard, and forecast.
- Harden Restock ↔ Lobby transitions and build-only input behavior.

Exit: repeated scene changes and save/reload never lose or duplicate a box.

### Phase 5 — UI and accessibility polish

- Apply shared visual states and responsive rules across every Level 1 screen.
- Remove legacy UI flashes and patience-bar initialization flashes.
- Add Reduced Motion, focus recovery, readable scaling, and safe-area checks.
- Finalize the cartoon newspaper art and Times New Roman TMP assets.

Exit: Level 1 has one consistent, build-safe visual language.

### Phase 6 — Integrated balance and build validation

- Run multi-day automated simulations.
- Play a minimum three-day manual session in a Windows build.
- Test a save/reload and Restock round trip on every day boundary.
- Run a 30-day content repetition and save-integrity test.
- Validate supported resolutions, minimized/restored window behavior, and mobile layout if mobile remains in this release.

Exit: all Level 1 definition-of-done items below pass.

## 11. Test matrix

| Test | Expected result |
|---|---|
| Start Day 1 | Welcome paper appears; Open Restaurant remains locked until it opens |
| Complete a perfect day | Positive, truthful story and advice; no invented failure |
| Four customers are not accommodated | Exact count and cause can appear in story/advice |
| Reload during preparation | Same market, paper, reviews, and unread/viewed state |
| Minimize during newspaper animation | On restore, paper is safely open or closed; never frozen midway |
| Exit restock during iris transition | Lobby becomes usable and overlay/input lock clears |
| Price changes after ordering | Existing order keeps purchased total; new orders use new price |
| Promote an employee | Stats and wage persist; paper may report the promotion |
| Move a partial box | Units and expiry persist; no duplicate shelf/hotbar copy |
| Store item incorrectly | Warning appears and accelerated spoilage persists |
| Discard expired box | Exact batch and shelf occupancy are removed; waste is reported |
| Run 30 generated days | No identical complete newspaper; recent section copy is not repeated |
| Change resolution/window mode | UI remains readable, clickable, and within safe bounds |

## 12. Casual Dining Level 1 definition of done

Casual Dining Level 1 is ready for tutorial and restaurant-variant work only when:

- The full preparation → newspaper → service → results → next-day loop is reliable in a Windows build.
- The newspaper is mandatory, readable, deterministic, and based on real previous-day data.
- A 30-day run does not repeat an identical issue and avoids recent section repetition.
- Approval and restaurant rating are separate, saved, and understandable.
- Supplier prices affect real purchases and are reported consistently.
- Employees gain meaningful role progression and persist correctly.
- Physical restock storage survives scene changes and save/reload without loss or duplication.
- UI is consistent and accessible across Lobby, management, results, and restock scenes.
- No legacy UI flash, patience-bar size flash, stuck iris, or focus-return blocker remains.
- All critical mouse buttons and F10/debug behavior required for the PC build pass a standalone-build smoke test.
- There are no known progression-blocking or save-corrupting bugs.

Only after this sign-off should production move to tutorials, additional restaurant types, or multiplayer.

## 13. First implementation tickets

1. Define `DailyRestaurantSnapshot` and cause-specific incident counters.
2. Add save schema entries and migration defaults.
3. Build the authored newspaper prefab and shared visual tokens.
4. Add the Preparation newspaper button, unread badge, view gate, and animation watchdog.
5. Build the deterministic template library and 30-day repetition test.
6. Implement `SupplierMarketManager` and purchase price snapshots.
7. Implement `RestaurantRatingManager` and result-based reviews.
8. Add stable employee IDs, daily performance records, and progression save fields.
9. Add stable shelf IDs and physical container save/reconciliation.
10. Complete the Level 1 responsive/accessibility pass and standalone-build regression suite.

## 14. Suggestions and player-choice sheet

This section contains optional design directions. Each choice has a short ID so selections can be approved without rewriting the plan. The recommended choices aim for a polished Level 1 without adding systems that belong to later restaurant types or multiplayer.

No item in this section is approved for implementation until the user selects it and gives the start signal.

### Newspaper suggestions

#### N1 — Newspaper name

- **N1-A — The Galactic Gazette (Recommended):** Clear, memorable, and broad enough to cover the whole campaign.
- **N1-B — The Daily Saucer:** More comedic and strongly alien-themed.
- **N1-C — The Cosmic Critic:** Focuses more heavily on restaurant ratings and reviews.
- **N1-D — Custom name:** Use a final title supplied later.

#### N2 — Daily viewing requirement

- **N2-A — Open once to unlock the day (Recommended):** The restaurant unlocks when the opening animation finishes; the player is encouraged, but not forced, to read every line.
- **N2-B — Scroll near the end:** Unlock only after the player has opened the paper and scrolled most of the page.
- **N2-C — Read-or-skip confirmation:** Let the player skip after a confirmation, useful for repeat/endless play.

#### N3 — Alien narrator personality

- **N3-A — Dramatic but helpful reporter (Recommended):** Playful criticism, clear facts, and useful advice.
- **N3-B — Harsh alien inspector:** Stronger insults and pressure when the restaurant performs badly.
- **N3-C — Friendly alien fan:** Warmer, funnier, and more encouraging even after a weak day.
- **N3-D — Rotating reporters:** Several alien writers with distinct voices; more variety but substantially more writing work.

#### N4 — Newspaper depth

- **N4-A — One main page plus scrollable columns (Recommended):** Approval, stars, incident, customer quote, prices, and advice in one focused issue.
- **N4-B — Two-page newspaper:** Adds staff, finances, stock forecast, and a second page; richer but slower to read daily.
- **N4-C — Short front page:** Shows only the lead story, rating, prices, and advice; faster but less useful.

#### N5 — Story variation

- **N5-A — Authored templates with 30-day non-repeat testing (Recommended):** Reliable offline writing with deterministic saves.
- **N5-B — Authored templates plus rare special issues:** Adds milestone, promotion, shortage, Day 10/20/30, and perfect-day editions.
- **N5-C — Procedural sentence mixing:** Produces more combinations but needs more grammar testing and can sound less natural.

N5-B can be combined with N5-A and is the suggested first expansion after the base newspaper is stable.

#### N6 — Opening animation style

- **N6-A — Spin, grow, and slap (Recommended):** Matches the requested physical cartoon effect, with a Reduced Motion alternative.
- **N6-B — Newspaper flies in from offscreen:** Cleaner and less energetic.
- **N6-C — Alien beam delivery:** The paper materializes in a beam before opening; charming but needs more visual effects.

#### N7 — Optional newspaper extras

These may be selected independently:

- **N7-A — Staff Spotlight (Recommended):** Reports promotions or the best worker from yesterday.
- **N7-B — Tomorrow's weather/crowd flavor:** Cosmetic forecast that may hint at expected traffic.
- **N7-C — Daily cartoon panel:** A small reusable comic image; high art cost, so defer until the text system is stable.
- **N7-D — Collectible archive:** Lets players reread all past issues from the management computer.
- **N7-E — Headline visible on the folded icon:** Gives a preview before opening.

Suggested newspaper package: **N1-A, N2-A, N3-A, N4-A, N5-A + N5-B, N6-A, N7-A, and N7-D.**

### Plan #4 suggestions — Employee progression

#### E1 — Progression model

- **E1-A — Role-based experience (Recommended):** Staff improve mainly in the role they perform, making assignments meaningful.
- **E1-B — General employee level:** Every completed shift improves all relevant abilities; simpler but less strategic.
- **E1-C — Skill-tree perks:** Players choose perks at level-up; deeper but too large for the first Level 1 polish pass.

#### E2 — Employee strengths

- **E2-A — One strength and one weakness (Recommended):** Examples include Fast Learner, Careful, Slow Starter, or Easily Distracted. Easy to understand and useful for assignment decisions.
- **E2-B — Numeric stats only:** Cleaner implementation, but employees feel less memorable.
- **E2-C — Multiple traits and personalities:** Richer simulation with a much larger balance and writing burden.

#### E3 — Morale and fatigue

- **E3-A — Performance trend only (Recommended for Level 1):** Show recent form without adding a system that requires rest schedules and recovery items.
- **E3-B — Simple morale:** Good days and fair pay raise morale; repeated failures lower it.
- **E3-C — Full morale and fatigue:** Adds rest days, burnout, and recovery; defer unless employee management should become a major game pillar now.

#### E4 — Promotions and wages

- **E4-A — Automatic promotion at thresholds with advance wage notice (Recommended):** Clear and low-friction.
- **E4-B — Player approves promotions:** More control, but declining a deserved promotion needs consequences and more UI.
- **E4-C — Training purchase:** Spend money to accelerate growth; useful later as a management upgrade.

#### E5 — Applicant refresh

- **E5-A — Weekly refresh with a fixed pool (Recommended):** Predictable and prevents rerolling by reopening the screen.
- **E5-B — Replace one applicant daily:** More variety but makes hiring feel less deliberate.
- **E5-C — Pay to advertise for new applicants:** Good future money sink after the base hiring loop works.

#### E6 — Failure severity

- **E6-A — Bounded mistakes (Recommended):** Weak staff are slower and slightly less accurate but never make the day impossible.
- **E6-B — High-impact mistakes:** Staff can severely damage service; creates drama but may feel unfair.

Suggested employee package: **E1-A, E2-A, E3-A, E4-A, E5-A, and E6-A.** Add E3-B and E5-C only after the base balance is proven.

### Plan #5 suggestions — Supplier economy, ratings, and reviews

#### S1 — Price-change frequency

- **S1-A — Change 1–3 ingredients per day (Recommended):** Noticeable without forcing the player to relearn the whole catalog daily.
- **S1-B — Change every ingredient slightly:** A lively market, but visually noisy and harder to plan around.
- **S1-C — Change prices every few days:** Easier planning but gives the daily Market Watch less value.

#### S2 — Price volatility

- **S2-A — Mostly ±5–15%, rare ±20% events (Recommended):** Meaningful but controllable.
- **S2-B — Very stable ±3–8%:** More casual and predictable.
- **S2-C — Volatile ±10–35%:** Stronger purchasing strategy but risks punishing unlucky days.

#### S3 — Market event stories

- **S3-A — Tagged flavor events (Recommended):** Price changes receive alien reasons such as moon harvests or cargo delays, but the numbers remain bounded.
- **S3-B — Numbers only:** Faster to build but makes the newspaper less alive.
- **S3-C — Events affect supply quantity too:** Deeper economy; defer until pricing alone is balanced.

#### S4 — Restaurant star precision

- **S4-A — Half-star display with an internal 0–100 score (Recommended):** Easy to read while preserving smooth progress.
- **S4-B — Whole stars only:** Very simple but progress may feel slow or jumpy.
- **S4-C — Decimal rating such as 4.3:** Precise but less cartoon-like.

#### S5 — Rating relationship to Alien Approval

- **S5-A — Separate values with a small relationship (Recommended):** Good service improves rating directly and may slightly help approval, while story events can still change approval independently.
- **S5-B — Completely independent:** Very clear technically, but players may find conflicting results confusing.
- **S5-C — Same value:** Simpler, but loses the distinction between restaurant quality and alien campaign sentiment.

#### S6 — Review volume and access

- **S6-A — 1–3 reviews per day plus a saved archive (Recommended):** Enough evidence for the rating without flooding the player.
- **S6-B — One featured review only:** Quick to consume but may overrepresent one customer.
- **S6-C — Review feed for every group:** Rich data but excessive writing and UI noise.

#### S7 — Purchasing tools

These may be selected independently:

- **S7-A — Price-change arrows and yesterday comparison (Recommended).**
- **S7-B — Mark favorite ingredients for quick tracking.**
- **S7-C — Buy-limit warnings when an order is likely to overstock.**
- **S7-D — Future contracts/locked prices:** Defer until the normal market is proven.

Suggested economy package: **S1-A, S2-A, S3-A, S4-A, S5-A, S6-A, S7-A, and S7-C.**

### Plan #6 suggestions — Restock persistence and advanced storage

#### R1 — Wrong-storage behavior

- **R1-A — Allow after confirmation with faster spoilage (Recommended):** Preserves player freedom and creates a meaningful consequence.
- **R1-B — Block placement completely:** Easier to understand but removes the storage decision.
- **R1-C — Allow without confirmation:** Fast, but players may accidentally ruin stock without understanding why.

#### R2 — Wrong-storage spoilage rate

- **R2-A — Approximately 3× faster, configurable (Recommended):** Serious but gives time to correct a mistake.
- **R2-B — Approximately 2× faster:** Gentler casual mode.
- **R2-C — Approximately 4× faster:** Strong punishment suitable only after clear warnings are tested.

#### R3 — Shelf organization tools

These may be selected independently:

- **R3-A — Ingredient labels and expiry badges (Recommended).**
- **R3-B — Sort/highlight by expiry date (Recommended).**
- **R3-C — One-click auto-place:** Convenient but reduces the point of the physical restock room.
- **R3-D — Custom shelf labels:** Helpful once the ingredient catalog becomes larger.

#### R4 — Stock forecast depth

- **R4-A — Four simple states: Low, Enough, Overstocked, Spoilage Risk (Recommended):** Immediately readable.
- **R4-B — Exact demand calculation with projected units:** More useful for advanced players; can appear in a tooltip under R4-A.
- **R4-C — Automatic recommended order cart:** Powerful but should be deferred until forecasts are trusted.

R4-A and R4-B may be combined: show the simple status first and exact values on hover/click.

#### R5 — Expired stock handling

- **R5-A — Manual discard with strong warning and newspaper waste report (Recommended):** Keeps the consequence visible.
- **R5-B — Automatic overnight disposal:** Less busywork but hides the cause of inventory loss.
- **R5-C — Sell/dispose service for a fee:** Potential later upgrade, not needed for Level 1 polish.

#### R6 — Recovery from old saves or invalid placement

- **R6-A — Recovery holding area (Recommended):** Any box that cannot return to its cell is placed in a safe overflow zone and reported to the player.
- **R6-B — First available compatible shelf:** More automatic but may rearrange the player's room unexpectedly.
- **R6-C — Convert misplaced boxes back to abstract inventory:** Safe for data but breaks the physical-storage promise.

Suggested restock package: **R1-A, R2-A, R3-A, R3-B, R4-A + R4-B, R5-A, and R6-A.**

### Plan #13 suggestions — UI and accessibility

#### U1 — Visual consistency approach

- **U1-A — Shared style tokens and reusable prefabs (Recommended):** One controlled source for buttons, panels, type, spacing, and states.
- **U1-B — Polish each screen independently:** Faster for one screen but likely to create new inconsistencies.

#### U2 — Text size support

- **U2-A — Normal and Large modes (Recommended):** Practical scope with strong readability benefit.
- **U2-B — Continuous text-size slider:** Flexible but much harder to validate on every layout.
- **U2-C — Fixed text only:** Lowest effort, but not an accessibility polish pass.

#### U3 — Motion settings

- **U3-A — Normal and Reduced Motion (Recommended):** Newspaper and iris transitions use short fades in reduced mode.
- **U3-B — Separate animation intensity slider:** More control but requires more testing and tuning.

#### U4 — Color accessibility

- **U4-A — Icons/text plus color and a high-contrast option (Recommended):** No status relies on color alone.
- **U4-B — Several color-blind presets:** Valuable, but should follow a complete palette audit.

#### U5 — Input scope for this milestone

- **U5-A — Mouse/keyboard and touch first (Recommended):** Matches the current PC and mobile concerns.
- **U5-B — Mouse/keyboard, touch, and controller:** Choose only if controller is a Level 1 launch requirement; it expands focus/navigation testing across every menu.

#### U6 — Newspaper typography scope

- **U6-A — Times New Roman only inside the newspaper (Recommended):** Preserves the existing game identity while making the paper authentic.
- **U6-B — Times New Roman across every management UI:** Creates a stronger theme but may reduce clarity and require a larger redesign.

#### U7 — UI sound and feedback

These may be selected independently:

- **U7-A — Paper rustle/slap and page movement sounds (Recommended).**
- **U7-B — Consistent hover/click/disabled UI sounds (Recommended).**
- **U7-C — Optional light haptics on supported mobile devices.**

Suggested UI package: **U1-A, U2-A, U3-A, U4-A, U5-A, U6-A, U7-A, and U7-B.**

## 15. Approval format

Selections can be supplied in a short list. Example:

```text
Newspaper: N1-A, N2-A, N3-C, N4-A, N5-A+B, N6-A, N7-A+D
Employees: recommended package, plus E3-B
Economy: recommended package
Restock: R1-A, R2-B, R3-A+B, R4-A+B, R5-A, R6-A
UI: recommended package, but U5-B
```

Choices may be changed while the project remains in planning. Implementation begins only after a separate explicit signal such as: **“Approved—start Phase 0.”**
