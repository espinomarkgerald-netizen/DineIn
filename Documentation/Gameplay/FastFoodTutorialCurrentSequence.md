# Fast Food Kitchen Tutorial — Current Sequence

This document describes the tutorial as it is currently authored in `Lobby2Tutorial` and interpreted by `FastFoodTutorialBridge`. It records existing behavior to support a future polish pass; it is not a proposed redesign.

The saved tutorial currently contains **95 ordered lessons**. Each lesson has a matching entry in `FastFoodTutorialBridge.lessons`. Big Boss is the speaker throughout. The opening uses the Welcome portrait; most explanations use the explaining portrait; lessons whose IDs end in `done` use the success portrait. The Fast Food presentation is configured to play the existing portrait pop on each line.

## How to read the sequence

- An **explanation** shows Big Boss's dialogue and a click/tap-to-continue prompt. It does not yet ask the player to perform the described action. The player dismisses the line before the action begins.
- An **action** shows its short instruction first. After the player dismisses it, the tutorial waits for the corresponding real game state or input. It accepts the expected action and restricts unrelated kitchen interactions for guided chapters.
- For a **drag**, the hand/cursor points from the existing source item to the world target. The full-screen mask is hidden during the drag so it cannot intercept the gesture; the target indicator and hand provide the guidance.
- For a **tap/click**, the mask and hand/cursor focus the actual control or ready food after the instruction is dismissed.
- For **passive waits** (food cooking, staff preparing sandwich protein, or the intentional burn), the action prompt may be dismissed, but then the mask, target and hand/cursor remain hidden. Big Boss's passive message uses the wait objective. Once the waited-for state is ready, the next lesson prompts the player to act and shows its guidance.
- Cooking work is chapter-specific and order deadlines are disabled. Pausing and recovery controls remain available. Retry Chapter and Skip Training are hidden during ordinary play and shown after a detected failure; the intentional-burn lesson is not treated as a failure.

Chapter names below are the saved progress checkpoints. Resuming starts at the first lesson in the saved chapter and recreates that chapter's training setup rather than resuming a mid-drag or mid-recipe state.

## 1. Briefing — lessons 1–7

The tutorial opens in the restaurant. The player is not asked to repeat casual-dining management training.

1. **Welcome.** Big Boss: “Welcome to Fast Food! Let's learn how this kitchen works.” Welcome portrait; click/tap to continue.
2. **Prior knowledge.** “You already know the restaurant basics: manage your menu, supplies, staff, and customers.” Continue.
3. **What is new.** “Here, you'll cook, prepare several orders at once, and put the meals together.” Continue.
4. **Pacing.** “Take your time. The pause button stays available throughout training.” Continue.
5. **Open the real kitchen.** “The Kitchen button on your HUD opens the kitchen. Select it now.” After dismissal, the mask and cursor point to the existing Kitchen button in the game HUD. The step completes when that button opens the real kitchen view.
6. **Kitchen welcome.** “Welcome to your kitchen! First, let's look at the controls you'll use.” Continue.
7. **Station roles.** “Grill handles burgers and sandwich preparation. Fryer cooks fried food. Assembler puts orders together.” Continue. No cooking action has started yet.

## 2. Burger — lessons 8–26

### Choose Grill and inspect the interface

8. **Choose a station.** “Choose Grill.” After dismissal, the tutorial points at the real Grill selection control. The station action completes when Grill is selected and its camera transition settles.

The next ten lessons are dialogue-only tours. Each line is dismissed before the next; the player is not yet asked to load food.

9. “The blue panel names your station and current food. Its count and progress bar show how much work is finished.”
10. “The arrows switch stations. Stations opens the station list, and Exit Kitchen returns you to the restaurant.”
11. “Use the pause button whenever you need a break. Resume returns you to the same lesson.”
12. “The TASK clipboard holds your current objective. If you're unsure what comes next, check it there.”
13. “A Restock notice appears when ingredients run low. Open it to review supplies and go to storage. We'll try that later.”
14. “The hotbar holds your ingredients. Each cell keeps its position so you can find the same ingredient quickly.”
15. “The number is your available quantity. Raw ingredients and cooked ingredients marked READY use separate cells.”
16. “A dim cell with zero available cannot be used yet. Cook more protein, wait for staff, or restock the ingredient it needs.”
17. “This is the cooking surface. Drag raw patties here. Each patty has its own timer; wait until it is ready, then click it to collect.”
18. “After cooking, the view moves to the prep table. Add the pictured ingredients in recipe order. Finished food moves to ready supply.”

### Cook and collect one burger patty

19. **Load.** “Drag the raw ingredient onto the highlighted cooking spot.” The hand demonstrates a drag from the raw beef-patty hotbar cell to Grill slot 1. The tutorial accepts the matching raw patty in that slot.
20. **Wait.** “Watch the timer. Collect the food when it is ready.” After dismissal, the passive objective is “Wait for the food to finish cooking.” No mask, target, or cursor is shown during the cook timer. The tutorial advances when the patty reaches Ready.
21. **Collect.** “Tap the ready patty.” The mask/hand now identify the actual ready patty. The real click transaction collects it into cooked READY supply; the next action does not start until the state confirms collection.

### Assemble the burger in the existing recipe order

The actual recipe's four visual steps are **Bottom bun → Cooked BurgerPatty → Cheese → Top bun**. Each step is a separate action: after dismissing Big Boss's line, the hand guides the correct hotbar item to the active prep position. The game accepts only the expected ingredient and current prep slot. The last bun completes the product and returns it to ready supply.

22. “Add Bottom bun to the highlighted prep position.”
23. “Add Cooked BurgerPatty to the highlighted prep position.”
24. “Add Cheese to the highlighted prep position.”
25. “Finish with Top bun. The completed food moves to ready supply.”
26. **Confirmation.** “Good! The food is ready, and the prep position is free again.” Continue.

## 3. Fries — lessons 27–32

27. **Choose Fryer.** “Choose Fryer.” The player dismisses the instruction, then follows the station-selection focus. The action completes after Fryer is selected and the camera settles.
28. **Fryer tour.** “The Fryer uses baskets. Load the raw food, wait for the timer and raised basket, then collect it onto the drying rack.” Continue.
29. **Load fries.** “Drag the raw ingredient onto the highlighted cooking spot.” The hand guides raw fries from the hotbar to the highlighted fryer position/basket.
30. **Wait.** “Watch the timer. Collect the food when it is ready.” After dismissal, the passive objective says to wait for cooking to finish; no mask or cursor remains over the basket while it cooks. The lesson advances when the fries are ready.
31. **Collect.** “Tap the ready basket.” The mask/hand identify the raised basket. The real basket click collects the ready fries onto the drying rack.
32. **Confirmation.** “The fries are on the drying rack. Let's make an order.” Continue.

## 4. Serving an order — lessons 33–40

33. **Choose Assembler.** “Choose Assembler.” The player selects the real station control; the action completes after the Assembler view settles.
34. **Read the ticket.** “The order card shows what the tray needs. Match each item and quantity.” Continue.
35. **Learn the tray.** “The tray is your assembly area. Drag the ready food and drinks from the hotbar onto it. Serve submits the completed order.” Continue.

The tutorial creates a real training order for **one Burger, one Fries, and one Coke**. Each placement is a separate drag from ready supply to the tray; the actual order ticket and item matching logic validate each one.

36. “Drag Burger onto the tray.”
37. “Drag Fries onto the tray.”
38. “Drag Coke onto the tray.”
39. **Submit.** “Everything is on the tray. Press Serve.” After dismissal, the hand/mask focus the real Serve button. This step waits for the ticket's actual submitted state; placing food alone does not finish it.
40. **Confirmation.** “Nice work. Orders are only submitted when you press Serve.” Continue.

## 5. Three-slot parallel prep — lessons 41–64

41. **Return to Grill.** “Choose Grill.” Select Grill and wait for its camera to settle.

### Cook three patties

The tutorial reserves three burger portions. It asks the player to load all three cooking spots **before** it starts the wait/collect prompts. Each load is a separate drag to the named cooking slot:

42. “Drag the raw ingredient onto the highlighted cooking spot.” Load patty 1.
43. “Load another spot while the others cook.” Load patty 2.
44. “Load another spot while the others cook.” Load patty 3.

The wait and collect prompts then run in slot order. Ready patties are protected from overcooking during this guided chapter while the player handles the remaining steps.

45. “Watch the timer. Collect the food when it is ready.” Passive wait for patty 1.
46. “Tap the ready patty.” Collect patty 1.
47. “Watch the timer. Collect the food when it is ready.” Passive wait for patty 2.
48. “Tap the ready patty.” Collect patty 2.
49. “Watch the timer. Collect the food when it is ready.” Passive wait for patty 3.
50. “Tap the ready patty.” Collect patty 3.

### Use each of the three prep positions

51. **Slot explanation.** “There are three prep positions. Each keeps its own progress.” Continue.

The tutorial starts three burgers independently by putting a Bottom bun in each slot before finishing the remaining layers. The source order is slot 1, then slot 2, then slot 3:

52. “Start a burger in prep position 1.” Drag Bottom bun to prep slot 1.
53. “Start a burger in prep position 2.” Drag Bottom bun to prep slot 2.
54. “Start a burger in prep position 3.” Drag Bottom bun to prep slot 3.

It then finishes one burger at a time, in slot order. For each burger, drag the cooked patty, cheese, then top bun into that same slot:

| Lesson | Big Boss instruction | Target |
| --- | --- | --- |
| 55 | “Add Cooked BurgerPatty to the highlighted prep position.” | Slot 1 |
| 56 | “Add Cheese to the highlighted prep position.” | Slot 1 |
| 57 | “Finish with Top bun. The completed food moves to ready supply.” | Slot 1 |
| 58 | “Add Cooked BurgerPatty to the highlighted prep position.” | Slot 2 |
| 59 | “Add Cheese to the highlighted prep position.” | Slot 2 |
| 60 | “Finish with Top bun. The completed food moves to ready supply.” | Slot 2 |
| 61 | “Add Cooked BurgerPatty to the highlighted prep position.” | Slot 3 |
| 62 | “Add Cheese to the highlighted prep position.” | Slot 3 |
| 63 | “Finish with Top bun. The completed food moves to ready supply.” | Slot 3 |

64. **Confirmation.** “You can work across all three positions. Finished food leaves immediately so you can keep working.” Continue.

## 6. Staff-cooked sandwich proteins — lessons 65–74

65. **Choose Grill.** “Choose Grill.” Select Grill and wait for the camera to settle. Sandwich assembly stays on Grill; staff handle the fryer protein work.

For each sandwich, the player waits at Grill while staff cook its protein. During that wait, the objective reads “Wait here for the cooked sandwich ingredient.” The hand/mask remain hidden. The step completes only when the protein is ready and the Grill prep state can accept it.

### Chicken Sandwich

66. “Stay at Grill. Staff will fry the protein for Chicken Sandwich.” Passive wait for staff to cook the chicken protein; the player does not switch to Fryer or cook it themselves.

The recipe layers are **Bottom bun → Cooked Chicken → Top bun**. The existing Grill prep target is used.

67. “Add Bottom bun to the highlighted prep position.”
68. “Add Cooked Chicken to the highlighted prep position.”
69. “Finish with Top bun. The completed food moves to ready supply.”

### Fish Fillet Sandwich

70. “Stay at Grill. Staff will fry the protein for Fish Fillet Sandwich.” Passive wait for the staff-cooked fish fillet to be handed to the Grill workflow. No player station switch is requested.

The recipe layers are **Bottom bun → Cooked FishFillet → Top bun**. The tutorial assigns the second sandwich to prep slot 2, leaving its own assembly state independent of the first.

71. “Add Bottom bun to the highlighted prep position.” Target slot 2.
72. “Add Cooked FishFillet to the highlighted prep position.” Target slot 2.
73. “Finish with Top bun. The completed food moves to ready supply.” Target slot 2.
74. **Confirmation.** “That's the sandwich sequence: staff fry the protein, and you assemble it here.” Continue.

## 7. Burn, discard, and recover — lessons 75–85

This is a deliberate teaching demonstration, not a failure state. Retry/Skip controls should stay hidden during it.

75. **Choose Grill.** “Choose Grill.” Select Grill and wait for the view to settle.
76. **Load a training patty.** “Drag the raw ingredient onto the highlighted cooking spot.” Load the raw burger patty.
77. **Intentionally wait through cooking and burning.** “Let's safely see what happens if a ready patty is left too long.” Passive objective: “Let this training patty burn.” The mask and hand stay hidden while the food cooks and overcooks. The tutorial waits for the Burnt state.
78. **Discard it.** “Drag the burnt patty to the discard area.” After dismissal, the drag hand guides the burnt patty to the real discard target. The action completes only when the burnt portion has been discarded and its cooking spot is free.
79. **Reload.** “The cooking spot is free. Load a fresh patty.” Drag a new raw patty to Grill.
80. **Wait for readiness.** “Watch this one until it is ready.” Passive wait; no mask/hand. The Burn chapter continues the burn timer for this fresh patty too, so the player must move directly to the next collect action when it becomes ready.
81. **Collect promptly.** “Collect the patty before it burns.” The tap focus appears for the ready patty; collect it through the normal click path. Ready-food protection used in the other guided chapters is disabled throughout the Burn chapter.
82. Add Bottom bun to the highlighted prep position.
83. Add Cooked BurgerPatty to the highlighted prep position.
84. Add Cheese to the highlighted prep position.
85. “Finish with Top bun. The completed food moves to ready supply.” This finishes the burger and demonstrates that work continues after discarding a burnt item.

## 8. Restock — lessons 86–89

At chapter setup, training fries stock is emptied and a training restock order is prepared so the tutorial has a real box for the exercise.

86. **Explain the notice.** “If an ingredient runs out, the kitchen shows a Restock notice. A training box is already in your supply hotbar.” Continue.
87. **Open storage.** “Open the kitchen Restock notice and choose Go to Restock.” The real kitchen Restock card is opened, and the player uses its Go to Restock action. The flow enters the existing storage scene.
88. **Store the supplied box.** “Store the supplied box on an open shelf, just as you learned before.” A hand guides the actual training box from the storage hotbar to a compatible open shelf. The tutorial checks the storage transaction and stock state.
89. **Return.** “The ingredient is available again. Exit storage to return to the restaurant.” The player uses the actual Exit Storage control and waits for the transition back to finish.

## 9. Hygiene — lessons 90–91

90. **Explain the decision.** “A dirty kitchen needs cleaning. The cleaning decision tells you how cooking will be affected.” Continue.
91. **Clean.** “Choose Clean Now, then wait for cleaning to finish.” After dismissal, the Clean Now control is highlighted and the real hygiene flow starts. When cleaning is underway, guidance becomes passive and the mask/hand are hidden. The step finishes when cleaning has ended and the hygiene decision is closed. A blocked wait eventually reveals Retry/Skip recovery controls.

## 10. Practice — lessons 92–94

92. **Hand over control.** “Your turn. Prepare two burgers, fries and a drink, then chicken and fish sandwich orders. There is no order deadline.” Continue.

93. **Free practice.** “Use the stations and serve both practice orders.” The task objective changes as the real state progresses. Navigation and normal cooking/assembly interactions are available; the guided-step interaction restriction is lifted for this action. Order deadlines remain disabled.

The practice order sequence is:

1. **Make two burgers.** Both burger portions must reach completed/ready state through the actual Grill assembly recipe. The practice stage then moves to Fryer.
2. **Cook and collect fries.** Fries must complete through the real fryer/basket/rack flow. The tutorial then creates a real ticket for **two Burgers, one Fries, and one Coke**.
3. **Assemble and serve that ticket.** Go to Assembler, place the two burgers, fries, and Coke on the actual ticket tray, then press Serve. The tutorial checks the submitted ticket; it then resets the practice kitchen order state and returns to Grill.
4. **Make both sandwiches.** Staff cook the chicken and fish proteins; the player assembles each sandwich on Grill using its real layers. Both must be complete before the second ticket is created.
5. **Assemble and serve the second ticket.** The ticket is **one Chicken Sandwich, one Fish Fillet Sandwich, and one Coke**. Place its products on the actual tray and press Serve.

Practice has no deadline and is not auto-completed by merely visiting stations. The order and recipe state checks must finish both served tickets.

94. **Practice confirmation.** “Well done! You can cook in batches, prepare three foods at once, and serve complete orders.” Continue.

## 11. Completion — lesson 95

95. Big Boss: “Kitchen training complete.” The tutorial shows its completion control. A fresh player is offered **Start Day 1** and returns to normal Lobby2; a replay or player with a career save is offered **Return to Game Mode** and returns to NewGameMenu. The bridge marks the tutorial complete as the player exits.

## Training setup and recovery rules behind the sequence

- Each chapter reconstructs its own setup when entered/resumed; the tutorial does not save half-loaded food or a mid-assembly gesture.
- Training uses cloned catalog items/recipes and training stock (80 per ingredient where the chapter provisions stock). It does not carry tutorial inventory or practice orders into career save data.
- Normal order/batch deadlines are effectively disabled. The lesson bridge drives the training kitchen clock and holds ordinary ticket/batch elapsed time.
- Guided actions are checked against the expected action, recipe, portion, ingredient, slot, control, or ticket state. For example, a burger assembly step cannot advance on the wrong layer or prep position.
- Outside the deliberate Burn chapter and free Practice, the bridge holds ready player food from overcooking while the lesson expects the player to perform another guided step.
- Timed dependencies have a bounded wait. A blocked wait or other detected failure shows the recovery message plus Retry Chapter and Skip Training. The intentional Burn chapter itself does not raise that recovery UI.
- Skip asks for confirmation. Retry rebuilds the current chapter setup. Ordinary station navigation is restricted during guided steps and restored for Practice.

## Source of truth

- Saved lesson and bridge arrays: `Assets/_Project/Scenes/RoleBased/Lobby2Tutorial.unity`.
- Exact generated messages, ordering, objectives, phases, hints and targets: `Assets/_Project/Editor/FastFoodTutorialAuthoring.cs` (`BuildLessons` and `WriteLessons`).
- Chapter setup, station restrictions, expected actions, completion tests, waits, practice orders and recovery: `Assets/_Project/Tutorials/FastFoodTutorialBridge.cs`.
- Dialogue/continue/mask/hand presentation and explanation-to-action transition: `Assets/_Project/Tutorials/TutorialSystem.cs` and `Assets/_Project/Tutorials/TutorialDialogueUI.cs`.
- Exact recipe layer labels: the Burger, Chicken Sandwich and Fish Fillet Sandwich recipe assets under `Assets/_Project/Office/Manager/Recipe` and `Assets/_Project/Resources/FastFood/Recipes`.
