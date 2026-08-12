# Dine In: Manager Gameplay and Unified Restaurant Loop

Planning date: 12 August 2026  
Status: Product direction and implementation roadmap

## 1. Game Vision

`Dine In` is a restaurant-management game in which the player acts as the restaurant manager while remaining able to help with every operational task.

The manager is not locked to a single employee role. During restaurant service, the player can perform the same work as any staff bot, including:

- Reception and customer seating
- Taking table orders
- Cashier and payment processing
- Clearing and cleaning tables
- Cooking and food preparation
- Preparing drinks as a barista
- Restocking and other restaurant tasks introduced later

Management and restaurant service take place in one unified restaurant scene. The old Office, Lobby, and Kitchen scenes remain useful as reference implementations, but the target game does not transition between separate role-based scenes during a normal day.

## 2. Core Design Principles

The new implementation should follow these rules:

1. The player is always the manager and never needs to switch roles.
2. Player and bot actions use the same underlying task and interaction rules.
3. Management and service occur in the same restaurant scene.
4. Restaurant phases control which systems and interactions are active.
5. Restaurant types share the same core architecture and differ through data, layouts, menus, staffing rules, and unique upgrades.
6. Legacy scenes and systems must not be deleted until their replacement paths are playable and verified.
7. Single-player restaurant gameplay must be stable before multiplayer is expanded.

## 3. Daily Game Loop

Each in-game day has three major phases:

```text
Management Phase
      |
      v
Restaurant Service Phase
      |
      v
End-of-Day Results
      |
      v
Next Day -> Management Phase
```

One authoritative game-flow system should own the current phase and day number. UI panels may request a transition, but they must not independently change campaign state.

Suggested phase states:

```csharp
Management
Opening
Service
Closing
DailyReport
```

### 3.1 Management Phase

The player begins each day inside the closed restaurant. Customers and normal service activity are inactive. The manager can walk around the restaurant and use the management computer.

The management computer is the central interface for the following systems.

#### Inventory and stock

- Review current ingredient and product quantities.
- Order additional stock before opening.
- Continue ordering stock during service when necessary.
- Prevent unavailable menu items from being served when their required stock is depleted.
- Restore an unavailable item after its required stock has been delivered.

#### Restaurant upgrades

- Buy and place additional booths or tables within allowed upgrade locations.
- Unlock restaurant-specific equipment.
- Preserve purchased upgrades between days.
- Spawn or enable purchased upgrades when restaurant service begins.

#### Human resources and employee applicants

Every in-game week, the restaurant receives procedurally generated applicants through the management computer.

The manager can:

- Review applicant names, roles, ratings, attributes, salary expectations, and experience.
- Hire applicants to fill available staff positions.
- Keep current employees or replace them.
- Compare applicants against existing employees.

Long-term employees gain experience:

- Continued employment increases employee experience.
- Experience can increase an employee's star rating and attributes.
- Higher experience and ratings increase the employee's salary grade.
- Retaining a strong employee therefore improves performance but raises operating costs.

Weekly applicant generation must not automatically replace current employees. Replacement is always a management decision.

#### Supplier price changes and menu pricing

At the beginning of a management phase, the computer can report changes to ingredient or product costs.

The manager can then adjust menu prices. Customer reactions depend on the relationship between menu price, food value, restaurant reputation, and the selected restaurant type.

Possible reactions to excessive prices include:

- Leaving shortly after being seated
- Showing an angry reaction but continuing to order
- Leaving a negative review after the visit
- Reducing overall satisfaction and restaurant reputation

Price changes should be data-driven. Customer behavior should use configured thresholds and probability curves rather than one hard-coded price check.

#### Reviews

The management computer provides a weekly restaurant review summary containing:

- Average restaurant performance
- Average customer satisfaction
- Service speed
- Food and drink quality
- Cleanliness
- Price satisfaction
- Selected customer comments
- Reputation change over the week

Customer comments should be generated from actual visit outcomes so the review explains why the rating changed.

#### Marketing — proposed feature

Marketing can be added as a management-computer feature after the core daily loop works.

Potential campaigns include:

- Online advertisements
- Billboards
- Limited-time promotions
- Restaurant launch campaigns

Marketing spends money to temporarily influence customer demand. It may increase customer spawn rate, group frequency, or restaurant awareness, but it must not directly guarantee customer satisfaction.

Recommendation: include a basic marketing system after the customer spawn curve, finance loop, and reputation systems are stable.

#### Opening the restaurant

The restaurant opens when the player selects `Open Restaurant` on the management computer.

Before accepting the command, the game should validate critical requirements such as:

- Required employees or fallback coverage
- At least one available menu item
- Required service stations
- Valid customer spawn, queue, and seating configuration

Warnings may allow the player to open under poor conditions, but invalid scene configuration should block opening and provide a clear reason.

### 3.2 Restaurant Service Phase

When the restaurant opens:

- Scheduled employees spawn or become active.
- Purchased upgrades become available.
- Customer spawning begins according to the current day and restaurant configuration.
- The manager can assist with every supported task.
- Inventory consumption, revenue, wages, satisfaction, and reputation events are tracked.

The initial Casual Dining customer loop is:

```text
Arrive
-> Queue
-> Receptionist seats group
-> Waiter takes order
-> Food and drinks are prepared
-> Waiter delivers order
-> Customers eat
-> Cashier processes payment
-> Customers leave
-> Busser clears and cleans table
-> Table becomes available
```

### 3.3 Current kitchen limitation

In the current prototype, food can spawn after lobby employees process an order. Kitchen employees do not yet perform the complete preparation workflow.

This temporary behavior is acceptable for the first unified-scene milestone. It should later be replaced with the original Kitchen-scene workflow adapted to the shared restaurant task system:

- Orders create kitchen preparation tasks.
- Chef and barista bots reserve compatible tasks.
- The manager can reserve and complete the same tasks.
- Ingredients are consumed during preparation.
- Completed food or drinks become available for delivery.
- An order cannot be completed or consumed twice.

### 3.4 Mid-service stock orders

The management computer remains available while the restaurant is open.

- The manager can order additional products during service.
- Delivery may be immediate during early development and use a configured delay later.
- Sold-out menu items become unavailable to new orders.
- Existing orders need an explicit fallback rule if stock becomes invalid after ordering.
- Customer and cashier UI must clearly show unavailable products.

## 4. End-of-Day Flow

The service phase ends when the configured closing condition is reached. Customer spawning stops, active customers finish or follow a defined closing rule, and the game calculates daily results.

At the end of the day:

- Restaurant reputation changes according to actual performance.
- Employee wages are paid automatically.
- Revenue, costs, and profit are finalized.
- Employee experience is updated.
- Campaign and alien objectives are evaluated.
- Progress is saved.
- A daily report panel appears.

### 4.1 Daily report

The daily report gives the restaurant a rating from one to three stars.

Suggested inputs:

- Customers served versus customers lost
- Average waiting time
- Order accuracy
- Food and drink completion time
- Customer satisfaction
- Cleanliness
- Price satisfaction
- Stockouts and unavailable orders
- Daily profit or loss

The report should show:

- One-to-three-star daily rating
- Revenue
- Stock and operating costs
- Employee wages
- Net profit or loss
- Customers served and lost
- Reputation change
- Alien objective progress
- Important positive and negative events
- `Next Day` button

Selecting `Next Day` increments the day, prepares weekly events when applicable, saves the campaign, and returns the same scene to the Management phase.

## 5. Campaign Progression

### 5.1 Days 1–7: guided onboarding

The first week teaches one responsibility at a time. Tutorial progress should use the real gameplay systems wherever possible rather than isolated fake mechanics.

| Day | Lesson | Completion target |
| ---: | --- | --- |
| 1 | Receptionist and seating | Seat five customer groups |
| 2 | Waiter service | Handle five tables or table orders |
| 3 | Cashier interface | Complete five simulated or real payments |
| 4 | Busser and cleaning | Clean five dirty tables |
| 5 | Chef and food preparation | Complete five food orders |
| 6 | Barista and drink preparation | Complete five drink orders |
| 7 | Management computer | Complete the guided planning and opening workflow |

Day 8 begins the normal game loop with all learned systems available.

Tutorial rules:

- Each tutorial uses contextual steps, highlights, dialogue, and completion conditions.
- Tutorials temporarily limit unrelated actions only when necessary.
- The player remains the manager; tutorials teach capabilities rather than changing the player's permanent role.
- Tutorial completion is saved.
- A reset option is available from settings.
- Tutorial logic must not be required during normal post-tutorial gameplay.

Chef and barista lessons depend on the real kitchen task workflow. Until that workflow exists, Days 5 and 6 should remain planned rather than implemented with misleading placeholder behavior.

### 5.2 Days 8–30: campaign

From Day 8 onward, the player runs the complete restaurant loop.

Customer volume and difficulty are controlled by the game manager using:

- Campaign day
- Restaurant type
- Restaurant reputation
- Marketing effects
- Current capacity
- Difficulty configuration
- Special events
- Alien demands or objectives

The day number should influence configuration data; it should not require thirty separately hard-coded day implementations.

### 5.3 Day 30 and endless play

The player completes the main campaign by reaching Day 30 and satisfying all required alien win conditions.

After campaign completion:

- Casual Dining remains playable in endless mode.
- The player can continue earning and saving money.
- Campaign failure pressure from alien approval ends or becomes informational only.
- Progress can be used to unlock additional restaurant types.

## 6. Restaurant Types

All restaurant types use the same daily phase system, save architecture, employee framework, task system, finance system, and customer satisfaction foundation.

Each type supplies its own:

- Scene layout or configured layout variant
- Menu and ingredient data
- Customer expectations
- Employee capacity and role rules
- Available upgrades
- Visual theme
- Balance configuration
- Unique mechanics

### 6.1 Casual Dining

Casual Dining is the first production restaurant and the campaign's primary restaurant.

Core staff roles:

- Receptionist
- Waiter
- Cashier
- Busser
- Chef
- Barista

This restaurant must be complete and stable before Fast Food and Fine Dining production work begins.

### 6.2 Fast Food

Fast Food uses a counter-ordering flow.

Planned baseline:

- One cashier by default
- Upgrade to a second cashier by purchasing another cash register
- Two kitchen employees
- One lobby employee acting primarily as a busser
- Customers order through a cashier or purchased kiosk
- After ordering, customers find an available clean seat

Unique upgrade:

- Self-service kiosk, purchased through management upgrades

The kiosk should reduce cashier demand without eliminating kitchen workload, seating limits, cleaning, stock consumption, or customer waiting behavior.

### 6.3 Fine Dining

Fine Dining uses the Casual Dining service structure with different food, higher expectations, and premium upgrades.

Core staff roles:

- Receptionist
- Waiter
- Cashier
- Busser
- Chef
- Barista

Unique content:

- Fine Dining menu and ingredients
- Higher service, cleanliness, and price expectations
- Hireable musician or pianist
- Pianist provides a configurable customer-attraction bonus, initially targeted at 20%

The pianist's effect must be configurable and balanced with seating capacity. Attracting more customers should be helpful only when the restaurant can serve them effectively.

## 7. Manager Player Requirements

The manager-player system is the first implementation milestone.

### 7.1 Player identity

- The player always has the Manager identity.
- Manager identity is separate from the task currently being performed.
- UI must not label the player as temporarily becoming a receptionist, waiter, cashier, busser, chef, or barista.
- The manager can perform any unlocked and valid task.

### 7.2 Shared capabilities

The manager should eventually support:

- Seat customer groups
- Take customer orders
- Deliver food and drinks
- Use the cashier interface
- Collect or process payments
- Clear and clean tables
- Prepare food
- Prepare drinks
- Restock stations
- Use the management computer during either phase when permitted

### 7.3 Task ownership

Bots and the manager must use a shared task-reservation system.

Required behavior:

- A task can have only one active executor.
- The manager may reserve an available task.
- A compatible bot may reserve an available task.
- A reserved task cannot be completed by another executor.
- Cancelled, invalid, timed-out, or interrupted tasks are released safely.
- Completion rewards and state changes happen exactly once.
- Player assistance does not require role switching.

### 7.4 First-day implementation scope

Today's realistic goal is not to implement every role. It is to establish the manager identity and prove that the manager can use existing Lobby interactions without role restrictions.

Recommended acceptance target:

1. A manager character spawns in the unified restaurant scene.
2. Movement, camera, and interaction input work.
3. The manager can perform at least one Receptionist action.
4. The manager can perform at least one Waiter or Busser action.
5. Existing bots continue their current Lobby behavior.
6. The player and a bot cannot complete the same interaction twice.
7. No scene transition or role switch is needed.
8. Kitchen access is structurally allowed, even if food preparation bots remain a later milestone.

Stretch target:

- Allow the manager to trigger the current placeholder kitchen/order completion path without introducing the final chef workflow yet.

## 8. Game Manager Responsibilities

The term `GameManager` should not become a container for every system. Responsibilities should be separated even if a central flow coordinator exposes them.

Suggested ownership:

| System | Responsibility |
| --- | --- |
| Game flow | Current day and Management/Service/Report transitions |
| Customer director | Spawn timing, group size, demand, and difficulty curve |
| Task board | Available tasks, priorities, reservations, and completion |
| Inventory | Product quantities, consumption, stockouts, and deliveries |
| Finance | Revenue, purchases, wages, costs, and profit |
| Employee system | Hiring, retention, experience, salary, and scheduling |
| Reputation | Visit results, daily reputation, reviews, and customer comments |
| Campaign | Alien objectives, Day 30 victory, and endless unlock |
| Save system | Persistent campaign, settings, employees, upgrades, and inventory |

The customer director may use the day number, but it must also accept restaurant configuration and runtime modifiers such as reputation and marketing.

## 9. Data That Must Be Saved

The unified save should eventually include:

- Current day and phase-safe resume state
- Campaign and alien objective progress
- Campaign completion and endless-mode unlock
- Current restaurant type
- Owned restaurant types
- Money
- Inventory and outstanding deliveries
- Menu prices
- Purchased and placed upgrades
- Current employees
- Employee experience, stars, attributes, and salary grades
- Weekly applicant state
- Supplier price changes
- Reputation and review history
- Tutorial progress
- Marketing campaigns and remaining duration
- Local settings

Mid-service saving should only be added after task, customer, and order state can be restored reliably. Until then, save at phase boundaries and clearly communicate this rule.

## 10. Implementation Roadmap

### Phase A — Manager vertical slice

- Add the Manager player to the unified restaurant scene.
- Select one movement, camera, and interaction implementation.
- Remove role gates from selected Lobby interactions through capability checks.
- Allow the manager to complete a small set of existing tasks.
- Prevent player/bot double completion.

Exit condition: the manager can help at reception and with one additional Lobby task in the same scene while current bots remain operational.

### Phase B — Daily phase loop

- Add Management, Opening, Service, Closing, and Daily Report states.
- Add the management computer and `Open Restaurant` command.
- Start and stop customer spawning by phase.
- Show a basic end-of-day report and advance to the next day.

Exit condition: two consecutive days can be played without changing scenes.

### Phase C — Inventory, finance, and stockouts

- Connect orders to ingredient consumption.
- Add pre-opening and mid-service stock ordering.
- Disable unavailable menu items.
- Calculate revenue, product costs, wages, and daily profit.

Exit condition: running out of stock affects ordering and restocking restores availability.

### Phase D — Real kitchen workflow

- Migrate food preparation from the original Kitchen scene.
- Add kitchen task generation and completion.
- Add chef and barista bot behavior.
- Allow manager assistance with all preparation tasks.

Exit condition: an order travels from table or cashier through real preparation and delivery without placeholder food spawning.

### Phase E — Employees and weekly HR

- Generate weekly applicants.
- Add hiring, replacement, experience, stars, attributes, and salary progression.
- Pay wages automatically at the end of each day.

Exit condition: employees persist across weeks and retention creates a meaningful performance-versus-cost tradeoff.

### Phase F — Pricing, reputation, and reviews

- Add supplier price changes.
- Add menu price editing.
- Add price-sensitive customer reactions.
- Generate weekly review summaries and comments from visit results.

Exit condition: menu pricing visibly affects behavior, satisfaction, reputation, and reviews.

### Phase G — Tutorial week and campaign

- Implement Days 1–7 as contextual tutorials.
- Add scalable Days 8–30 progression.
- Implement alien win conditions and endless-mode transition.

Exit condition: a new player can learn the complete loop and continue playing after a valid Day 30 victory.

### Phase H — Additional restaurant types

- Build Fast Food from shared systems and add kiosks.
- Build Fine Dining from shared systems and add the musician.
- Balance each restaurant independently through configuration.

Exit condition: each restaurant has a complete daily loop and its unique mechanic works without duplicating core gameplay code.

### Phase I — Marketing and extended polish

- Add marketing campaigns.
- Balance demand against reputation and capacity.
- Improve review variety, UI feedback, animations, audio, accessibility, and onboarding.

## 11. Scope Priorities

### Required for the first playable management-game build

- One Casual Dining restaurant
- Manager player with unrestricted assistance
- Existing Lobby bots
- One-scene Management and Service phases
- Customer arrival-to-payment loop
- Basic stock and finance behavior
- End-of-day report and next-day loop

### Required after the first playable build

- Real kitchen preparation
- Chef and barista bots
- Employee progression and weekly applicants
- Supplier price changes and menu pricing
- Reputation and weekly reviews
- Days 1–7 tutorial sequence
- Days 8–30 campaign and endless unlock

### Defer until the Casual Dining loop is stable

- Fast Food
- Fine Dining
- Kiosks
- Pianist
- Marketing campaigns
- Advanced employee personalities
- Large content expansion
- Multiplayer hardening

## 12. Key Risks

### Player and bot conflicts

If legacy interactions directly change objects without shared reservation or completion rules, the manager and bots can process the same customer, order, payment, or table twice.

Mitigation: introduce task ownership before expanding manager capabilities across every role.

### One-scene system collisions

Systems built for separate Office, Lobby, and Kitchen scenes may assume they are the only active manager or UI owner.

Mitigation: migrate one workflow at a time and define one owner for flow, saving, input, and persistent UI.

### Overloading the first milestone

Adding all player roles, real kitchen bots, management features, campaign progression, and restaurant variants together would make failures difficult to isolate.

Mitigation: first prove a Manager plus Lobby vertical slice, then add the daily phase loop, then replace placeholder kitchen behavior.

### Tutorial dependency

Days 5 and 6 cannot teach real cooking and drink preparation before those systems exist in the unified scene.

Mitigation: implement tutorial days only after their real gameplay capability passes normal-play testing.

### Economy balancing

Stock prices, menu pricing, salaries, upgrades, marketing, and reputation all affect profitability and demand.

Mitigation: keep formulas and thresholds in configuration assets and add debug reporting for every daily calculation.

## 13. Decision Summary

The immediate development direction is:

```text
Add Manager player
-> Prove unrestricted Lobby assistance
-> Add daily Management/Service/Report loop
-> Connect inventory and finance
-> Replace placeholder food spawning with real kitchen tasks
-> Add employee, pricing, reputation, and review systems
-> Build tutorial week and Day 30 campaign
-> Add Fast Food and Fine Dining
-> Add marketing and extended polish
```

This sequence preserves the full vision while keeping each implementation milestone testable.
