# Windows debugging

Open the console with **F10** in a restaurant. Enter `help()` to list commands.

- Unity Editor: available for development.
- Windows **Development Build**: available offline, without PlayFab login.
- Windows release build: still requires the server-verified authorized PlayFab account (Kali by default).
- Cloud wallet commands still require their existing online wallet service; enabling the console does not create an offline wallet.

## Fresh progression tests

**`day(n)` is destructive to the selected restaurant's current run.** Back up its save if needed before using it. It is not a calendar-only jump anymore.

Use `day(1)` through `day(30)`. The command clears the active day checkpoint, resets purchases, hires/assignments, funds, approval, stock, menu settings, daily history and unlock guidance, then reloads preparation on that day. Equipment/recipes available by that day become eligible again, but upgrades must be bought again. Earlier-day notifications are suppressed; the target day's notifications and optional Big Boss guide can replay. Other restaurant saves and account coins are not reset. The command is rejected in tutorials, multiplayer and while a save is loading.

For example:

1. `day(10)` to repeat the second Lobby Person milestone.
2. `money(50000)` and `fillStocks(100)` if testing without the normal economy.
3. Hire staff, buy the wanted upgrades, then `startDay()`.
4. `day(15)` to repeat the second Cashier milestone. Buy the second register and hire two Cashiers.

## Commands

- `day(n)`, `resetRun()` (fresh Day 1), `startDay()`, `endDay()`, `gameOver()`, `recover()`.
- `reputation(n)` / `approval(n)`, `money(n)`, `addMoney(n)`.
- `zeroStocks()`, `fillStocks(n)`.
- `upgrade(1)` cleanup trolley, `upgrade(2)` delivery trolley, `upgrade(3)` card payments.
- `unlockPopup(1)`, `unlockPopup(2)`, `unlockPopup(3)` replay the corresponding upgrade notification.
- `complaint(1)` / `wrongOrder()`, `complaint(2)` / `burntFood()` (need service and an eligible customer).
- `cardPayment()` forces the next eligible card payment.
- `timeScale(n)`, `save()`, `status()`, `help()`.
- `setCoin(n)`, `addCoin(n)` use the existing PlayFab wallet debug service.

## Manual regression checks

- Buy both trolleys: inspect parking clearance from counters, sink, idle staff and customer queues.
- Send Lobby #1 on a cleanup batch: the cart docks beside the sink before washing; verify that trays are cleared and it can start another batch.
- Hire Lobby #2: verify the worker walks to the counter-side home, delivers food and returns there.
- Staff both registers: each Cashier stays with their own counter. With only one active Cashier, both unlocked counters remain serviceable.
- Advance a day and reload: two hires for two available posts should still be assigned. Three hires remain a player roster choice.
- Repeat `day(10)` and `day(15)` twice each: confirm purchases reset and the correct day's optional guide can replay.
- Test F10 and Enter in an actual Windows Development Build, including offline startup. Confirm unauthorized release-build accounts remain locked out.

Parking tuning remains in `FastFoodLobbyAuthoring` under **Service-side parking**. Offsets are metres relative to the counter/sink staff approach, excluding imported model scale. Disable counter-side parking to use the scene's original markers instead.
