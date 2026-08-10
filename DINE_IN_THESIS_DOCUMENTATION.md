# Dine In Thesis Project Documentation

## Project Title

**Dine In: A Unity-Based Educational Restaurant Management Game**

## Project Overview

Dine In is a thesis project developed using the Unity Engine. It is an interactive restaurant management simulation game designed to teach and reinforce hospitality-related decision-making, teamwork, time management, resource management, and service workflow awareness. The project combines restaurant operations with game-based learning by allowing players to experience different roles and management responsibilities inside a stylized restaurant environment.

The game is structured around a day-based restaurant cycle. Players manage resources in the office, assign employees, purchase supplies and equipment, serve customers in the lobby, prepare orders in the kitchen, and evaluate performance through financial and gameplay results. The project also includes tutorial scenes, save/load support, player customization, online account handling through PlayFab, and multiplayer support using Photon.

## Narrative Concept

Dine In uses a playful science-fiction framing to make restaurant management more engaging. In the implemented project materials, the game is described as an educational restaurant management game where aliens have arrived on Earth and must be convinced that human food is worth sparing humanity. The player operates a restaurant over a 30-day period, serving alien customers and trying to maintain an Alien Approval Rating.

This narrative gives educational restaurant tasks a clear dramatic goal:

- Serve customers efficiently.
- Prepare correct orders.
- Manage employees and payroll.
- Buy ingredients and equipment.
- Keep the restaurant clean.
- Maintain customer satisfaction.
- Survive financially until the final evaluation.

## Development Platform

| Category | Details |
| --- | --- |
| Game Engine | Unity |
| Unity Version | 6000.0.40f1 |
| Main Language | C# |
| Rendering Pipeline | Universal Render Pipeline |
| Main Target Platforms | Windows and Android builds are present in the project |
| Networking | Photon Unity Networking / Photon Realtime |
| Account and Cloud Data | PlayFab |
| UI Frameworks | Unity UI, TextMesh Pro |
| Navigation | Unity AI Navigation / NavMesh |
| Input | Unity Input System |

## Main Project Goals

The main goal of Dine In is to create a playable educational simulation that demonstrates restaurant operations through interactive gameplay. The project aims to:

1. Provide a game-based learning experience for hospitality or restaurant service concepts.
2. Simulate multiple restaurant roles such as waiter, cashier, busser, kitchen worker, and manager.
3. Implement a complete gameplay loop from preparation to service to daily evaluation.
4. Encourage strategic decision-making through money, inventory, staffing, and equipment systems.
5. Improve engagement by using an alien-themed narrative and progression system.
6. Support replayability through daily objectives, unlocks, and performance results.
7. Demonstrate Unity-based software development using scene management, UI systems, persistent managers, data saving, and multiplayer tools.

## Target Users

The intended users of Dine In include:

- Hospitality students who need a practical and interactive introduction to restaurant workflows.
- Instructors who want a game-based tool for demonstrating restaurant operations.
- Casual players interested in time-management and simulation games.
- Thesis evaluators assessing the technical and educational value of the system.

## Game Genre

Dine In can be classified as:

- Educational game
- Restaurant management simulation
- Time-management game
- Role-based service workflow simulator
- Single-player and multiplayer-supported Unity project

## Core Gameplay Loop

The game follows a repeated day cycle:

1. **Management Phase**
   The player starts in the office, checks available money, manages employees, buys supplies, unlocks equipment, and prepares for the day.

2. **Lobby Shift**
   The player serves customers in the restaurant lobby. This includes assigning customers to booths, taking or processing orders, delivering food, collecting payment, and responding to customer needs.

3. **Kitchen Shift**
   The player prepares orders using kitchen stations such as grills, fryers, counters, dispensers, plates, and ingredient shelves.

4. **End-of-Day Evaluation**
   The game calculates money, payroll, expenses, revenue, objectives, and approval. The result affects the next day.

5. **Progression**
   The player advances to the next day, unlocks new systems, and faces increasing difficulty.

## Main Scenes

The project contains several main gameplay and tutorial scenes.

| Scene | Purpose |
| --- | --- |
| `MainMenu` | Entry scene for starting the game, account UI, and player setup |
| `Bootstrap` | Initializes persistent systems |
| `CoreGameplay` | Core gameplay support scene |
| `Office` | Management scene for inventory, employees, finance, equipment, and day preparation |
| `Lobby1` | Main service scene where customers are seated, served, and billed |
| `Kitchen` | Food preparation scene where orders are cooked and completed |
| `Multiplayer` | Multiplayer gameplay scene |
| `OfficeTutorial` | Tutorial for management systems |
| `LobbyTutorial` | Tutorial for lobby/service systems |
| `KitchenTutorial` | Tutorial for kitchen systems |
| `WaiterLevel1` | Single-player waiter level scene |

## System Architecture

Dine In is organized around Unity scenes, MonoBehaviour scripts, singleton-style managers, ScriptableObject data, and UI controllers. The project uses persistent managers for systems that must survive scene changes, such as game flow, saving, money, approval, and account/network services.

The general architecture is:

- **Scene controllers** handle transitions and scene-specific setup.
- **Manager classes** hold persistent gameplay state.
- **Interactable components** allow the player to click, tap, pick up, deliver, clean, or process objects.
- **UI scripts** update menus, HUDs, tutorial guidance, reports, and popups.
- **Data classes and ScriptableObjects** define ingredients, recipes, employees, equipment, inventory items, and customization values.
- **Network scripts** connect player sessions, rooms, and synced customization.

## Major Gameplay Systems

### Game Flow System

The `GameFlowManager` controls the day cycle and scene progression. It tracks:

- Current day
- Current phase
- Morning or afternoon state
- Lobby completion
- Kitchen completion
- Day advancement
- Game-over evaluation

The project uses three major phases:

- **Management**
- **Lobby**
- **Kitchen**

At the end of a completed day, the system evaluates whether the player should continue, lose, or win. The win/loss conditions include bankruptcy, approval collapse, and reaching Day 30.

### Office and Management System

The Office scene is the planning area of the game. It contains systems for:

- Buying ingredients and inventory items
- Managing restaurant money
- Hiring and assigning employees
- Viewing payroll and expenses
- Purchasing and unlocking equipment
- Unlocking recipes
- Starting lobby and kitchen shifts

This phase teaches planning before service begins. Players must balance spending with expected income and make decisions that affect the next service phase.

### Finance System

The finance system tracks restaurant income, expenses, payroll, and daily reports. Important finance-related components include:

- `MoneyManager`
- `FinanceManager`
- `DailyFinanceBridge`
- `DailyRevenueTracker`
- `MoneyUI`
- `DailyReportUI`

The player earns money from completed orders and loses money through expenses such as payroll and purchases. If the restaurant runs out of money after expense deduction, the game can trigger a bankruptcy loss.

### Inventory and Shop System

The inventory system manages restaurant supplies and purchased items. It includes:

- Item data
- Inventory entries
- Shop items
- Checkout and receipt handling
- Ingredient stock
- Purchase validation

This system supports the educational goal of showing that service quality depends on preparation and available resources.

### Employee and HR System

The HR system allows the player to manage restaurant staff. It includes employee data, generated employees, employee roles, role slots, and payroll calculations.

Supported role-related concepts include:

- Hiring employees
- Assigning employees to roles
- Locking role slots during shifts
- Calculating payroll
- Resetting assignments between days

This system connects human resource management with daily restaurant performance.

### Equipment and Unlock System

Dine In includes equipment purchasing and unlocking. Equipment and recipes can unlock by day, allowing gradual progression. Related systems include:

- `EquipmentManager`
- `EquipmentShopManager`
- `EquipmentLink`
- `EquipmentLinkActivator`
- `UnlockManager`
- `RecipeManager`

This encourages players to improve the restaurant over time instead of accessing everything immediately.

### Lobby Service System

The Lobby scene simulates front-of-house restaurant service. It includes:

- Customer groups
- Booths and seats
- Lobby queues
- Host assignment
- Waiter tasks
- Cashier interactions
- Busser cleaning tasks
- Order tickets and order numbers
- Payment pickup
- Money and tip popups
- Takeout flow

The customer flow generally follows this pattern:

1. Customers enter and queue.
2. Customers are assigned to booths.
3. Customers place orders.
4. Orders are processed and delivered.
5. Customers eat and request billing.
6. Payment is collected.
7. Tables may require cleaning.

### Customer Behavior System

Customer scripts manage customer state, patience, seating, ordering, eating, payment, and departure. Customer satisfaction affects revenue and approval. The system includes:

- Customer agents
- Customer groups
- Customer order data
- Speech or thought bubbles
- Patience indicators
- Happy, neutral, angry, or unhappy results

This creates the pressure and feedback loop expected in a restaurant service game.

### Kitchen System

The Kitchen scene handles food preparation. It includes interactive cooking stations and item handling:

- Grill
- Fryer
- Drink dispenser
- Cup spawner
- Plate spawner
- Counters
- Shelves and cupboards
- Ingredient stacks
- Delivery counters
- Trash can
- Order manager for kitchen tasks

Players prepare orders by collecting ingredients, cooking them with the correct stations, plating them, and sending completed orders through the delivery counter.

### Order System

The order system connects customer requests to kitchen and service tasks. It includes:

- Order numbers
- Order tickets
- Order checklist UI
- Customer order bubbles
- Food trays
- Delivery interaction
- Kitchen order manager

This allows the game to simulate communication between dining area and kitchen operations.

### Cleaning and Random Event System

Dine In includes cleaning-related tasks and random events. These systems create service interruptions and teach players to maintain restaurant cleanliness. Examples include:

- Puddles or table mess events
- Hold-to-clean input
- Cleanable events
- Sink interactions
- Tray cleaning
- Busser tasks

Ignoring cleaning tasks may affect customer satisfaction, workflow, or objective results.

### Alien Approval System

The alien narrative is implemented through the Alien Approval Rating. The approval score represents how convinced the alien customers are by human food and service. It begins at a starting value and changes depending on customer outcomes and daily objective grades.

Approval affects:

- Game-over conditions
- Narrative progression
- Customer spawn scaling
- End-of-day evaluation

If approval reaches zero, the game can trigger an Earth Conquered ending.

### Daily Objective System

The daily objective system gives the player specific goals before each shift. These objectives may include:

- Reaching a minimum revenue
- Limiting failed orders
- Limiting angry departures
- Serving a required number of groups

At the end of the day, the objectives are evaluated and converted into a grade. This grade can affect Alien Approval, reinforcing the link between operational performance and narrative success.

### Difficulty Scaling System

The `ShiftScaler` adjusts the game as days progress. Scaling can affect:

- Customer patience
- Number of groups per shift
- Pressure on the player
- Impact of alien approval on customer turnout

This system supports progressive challenge over the 30-day game structure.

### Tutorial System

The project includes dedicated tutorial systems for teaching the player how to use the game. Tutorial scripts manage:

- Dialogue UI
- Arrows and pointers
- Guided phases
- Interaction locking
- Role highlights
- Practice flows
- Waiter, cashier, office, lobby, and kitchen instructions

Tutorial scenes help new players understand game controls and workflows before entering the full simulation.

### Save and Load System

The save system uses a JSON file stored in Unity's persistent data path. `GameSaveManager` collects data from major managers and writes it into `dinein_save.json`.

Saved data includes:

- Current day
- Current phase
- Day half
- Lobby and kitchen completion
- Money
- Alien approval
- Unlock status
- Inventory state

The game can automatically save on pause, quit, and scene progression. It can also load previous progress when the game starts.

### Player Customization System

The player customization system allows visual customization of the character. It includes:

- Head color
- Body color
- Arms color
- Legs color
- Hat selection
- Serialization of customization data
- Application of customization when the player spawns

Customization can be saved locally and synchronized through online services.

### Account System

Dine In uses PlayFab for account-related features. The PlayFab system supports:

- Registration
- Login
- Auto-login through linked custom ID
- Account panel display
- Username handling
- Saved customization data
- Optional session locking

This gives the project a stronger production-style structure compared with a purely local prototype.

### Multiplayer System

Photon is used for multiplayer functionality. The networking system supports:

- Connecting to Photon
- Joining lobbies
- Creating or joining rooms
- Room code display
- Network player spawning
- Network movement synchronization
- Player customization synchronization
- Duplicate session handling through Photon user IDs

The multiplayer systems make it possible for multiple users to participate in the restaurant experience.

## Key Scripts and Responsibilities

| Script / System | Responsibility |
| --- | --- |
| `GameFlowManager` | Controls days, phases, scene transitions, and game-over evaluation |
| `GameSaveManager` | Saves and loads persistent game state |
| `GameSaveData` | Holds serialized save data |
| `MoneyManager` | Tracks available restaurant money |
| `FinanceManager` | Records and deducts daily expenses |
| `DailyRevenueTracker` | Tracks completed and failed orders |
| `EmployeeManager` | Handles employees, assignments, and payroll |
| `InventoryManager` | Manages stock and inventory data |
| `ShopManager` | Displays and manages purchasable shop items |
| `EquipmentManager` | Tracks equipment purchases and unlocks |
| `RecipeManager` | Handles recipe unlocking and recipe UI |
| `UnlockManager` | Maintains unlocked gameplay content |
| `CustomerGroup` | Manages a group of customers and their service state |
| `OrderFlowManager` | Coordinates customer order flow |
| `KitchenManager` | Supports kitchen-related order handling |
| `AlienApprovalManager` | Tracks Alien Approval Rating |
| `DailyObjectiveManager` | Rolls and evaluates daily objectives |
| `ShiftScaler` | Applies difficulty scaling by day |
| `PlayfabManager` | Handles login, registration, cloud customization data, and session checks |
| `PhotonBootstrap` | Connects the game to Photon and joins lobbies |
| `RoomManager` | Handles multiplayer room flow |
| `LocalSaveManager` / save bridge scripts | Connect specific systems to saved state |

## Data Management

Dine In uses several types of data:

- **Runtime data** managed by singleton managers during play.
- **Serialized save data** stored as JSON.
- **ScriptableObject-style data** for ingredients, items, recipes, roles, and customer types.
- **PlayerPrefs** for account auto-login keys and local session values.
- **PlayFab user data** for cloud-stored player customization.
- **Photon custom properties** for multiplayer player appearance synchronization.

## User Interface

The game contains several UI categories:

- Main menu panels
- Login and registration forms
- Character customization UI
- Office management panels
- Shop and checkout UI
- Employee role cards and role slots
- Money and finance displays
- Lobby task UI
- Order tickets and order checklist
- Customer bubbles and indicators
- Kitchen order UI
- Tutorial dialogue and arrows
- Daily report UI
- Game-over screens
- Loading screen UI

The UI is implemented using Unity UI and TextMesh Pro.

## Educational Value

Dine In supports learning by converting hospitality concepts into interactive tasks. The player learns through repeated practice and feedback rather than passive reading.

Educational topics represented in the project include:

- Customer service workflow
- Food preparation workflow
- Order accuracy
- Time pressure and prioritization
- Inventory planning
- Staff assignment
- Payroll awareness
- Revenue and expense management
- Cleanliness and service quality
- Performance evaluation

The game format helps students understand how individual restaurant operations affect the whole business.

## Technical Features

The project demonstrates the following technical features:

- Unity scene-based architecture
- Persistent singleton managers
- JSON-based local save/load system
- Unity UI and TextMesh Pro integration
- NavMesh-based movement support
- Interactable object system
- Character customization
- PlayFab account and user data handling
- Photon multiplayer connection and room support
- Daily progression and game-over logic
- Tutorial guidance systems
- Difficulty scaling
- Unlockable recipes and equipment
- Windows and Android build outputs

## Game Progression

Dine In uses a 30-day structure. Each day can introduce greater challenge through customer volume, patience changes, equipment unlocks, recipe unlocks, and daily objectives. The player must maintain enough money and approval to survive until the final day.

Possible end states include:

- **Bankruptcy:** The restaurant runs out of money.
- **Approval Collapse:** Alien Approval reaches zero.
- **Earth Saved:** The player reaches Day 30 with sufficient approval.
- **Earth Conquered:** The player reaches Day 30 but fails to maintain sufficient approval.

## Testing Considerations

Testing for Dine In should cover:

- Scene transitions from menu to office, lobby, kitchen, and back.
- Save/load correctness after quitting and reopening.
- Employee hiring, assignment, locking, and payroll.
- Inventory purchases and stock deductions.
- Customer seating, ordering, payment, and departure.
- Kitchen preparation and delivery correctness.
- Daily objective rolling and evaluation.
- Approval changes after customer outcomes.
- Game-over screens and reset behavior.
- PlayFab login, registration, sign-out, and customization save.
- Photon connection, lobby joining, room creation, and player spawning.
- Tutorial steps and interaction locks.
- Windows and Android build behavior.

## Limitations

The current project is strong as a thesis prototype, but possible limitations include:

- Some features depend on correct Unity Inspector wiring.
- Online features require valid PlayFab and Photon configuration.
- Multiplayer behavior may need repeated real-device testing.
- Tutorial flows must be checked carefully after gameplay changes.
- Balancing of money, payroll, order difficulty, and approval may require user testing.
- Android performance may vary depending on device capability.

## Recommended Future Enhancements

Future development may include:

- More restaurant roles and advanced staff behavior.
- More recipes, ingredients, and kitchen equipment.
- Expanded analytics for student performance.
- Teacher dashboard or assessment export.
- Stronger multiplayer cooperative role assignment.
- More polish for animations, sound effects, and feedback.
- Cloud save for full game progress.
- More complete difficulty balancing based on playtest results.
- Additional accessibility settings.
- Narrative cutscenes or expanded alien story events.

## Conclusion

Dine In is a Unity-based educational restaurant management game that combines simulation, role-based gameplay, resource management, customer service, and a science-fiction narrative. Its systems demonstrate both game development and educational software design. Through its office, lobby, kitchen, tutorial, save, account, and multiplayer modules, the project provides a complete foundation for an interactive thesis application focused on hospitality learning and restaurant operations.

