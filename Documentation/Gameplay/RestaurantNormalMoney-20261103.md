# Restaurant purchases and combined Normal Money

## Implemented rules

- In the game menus, Normal Money (₱) is the sum of saved, spendable cash across the current account's owned restaurants. It is a calculated view, not a second wallet or another copy of earnings.
- Inside a restaurant, the existing career/room operating-money system remains in use. Stock, salaries, equipment and ordinary gameplay do not spend the combined menu balance.
- Fast Food costs **₱10,000**, with the existing **10 successful Casual Dining days** requirement. Fine Dining costs **₱25,000**, with the existing **30 Casual Dining / 15 Fast Food successful days** requirements. Campaign and multiplayer successful-day records still count; duplicate recorded days do not create extra credit. Existing ownership is retained without retroactive fees.
- Prices remain editable as `fastFoodCashPrice` / `fineDiningCashPrice` on `GameModePopupController`. Restaurant models still use the existing locked/unlocked presentation.
- Expansion payments are deducted proportionally from owned careers' cash. Whole-peso rounding uses Casual Dining, Fast Food, then Fine Dining order, with the total deduction exactly equal to the price. There is no transfer into the target career and no GC debit.
- Locked careers never contribute funding. Missing/unplayed career files contribute zero; no default starting balances are invented for the menu. Existing target-career data is preserved when access is purchased. Starting capital follows the existing first-career initialization rules when that restaurant is played.
- If a career has both a save and an unfinished-day checkpoint, its spendable amount is the smaller of their balances. This prevents spending funds unavailable in one recovery document. The deduction and receipt are applied to both documents without discarding their other fields.

## Saving and compatibility

The existing account snapshot/journal commits all affected career balances and permanent ownership together. A crash after career-file writes can finish the same snapshot without subtracting again. The profile and paying careers retain a `restaurant-cash:` receipt; the existing cloud-sync notification uploads the updated account snapshot. There is no new service or PlayFab GC/NM currency grant/debit API for this transaction.

Restoring an older account snapshot that lacks a locally recorded cash-purchase receipt is rejected with an explanation to keep current-device progress. This prevents retaining a permanent unlock while refunding its payment through an old cloud restore. It does not provide a server-authoritative, concurrent multi-device economy. Use the existing account conflict flow instead of simultaneously purchasing from two devices.

Old GC restaurant orders retain their original status/confirmation recovery path. An unresolved old order blocks a new cash purchase for that restaurant. Known confirmed orders can still restore ownership; unknown payments are not resent. The old `RestaurantsV1` catalog is no longer the price source for new purchases, so an unpublished catalog no longer produces **Price: Unavailable** in this popup. Cosmetic purchases, GC exchanges and other existing GC behaviour were not changed.

## Popup design

The view still clones the authored shop purchase card. Only the restaurant clone is rearranged; no open scene or prefab was externally rewritten.

The blue frame, pale panel, existing fonts, red Back button, green purchase button and close artwork remain. The redundant circular preview backing is hidden. Text pivots/scales are normalized and arranged in separate, bounded bands:

1. Restaurant Access heading and close control.
2. Restaurant name.
3. Separate **Price** and **Normal Money** columns using ₱.
4. Successful-day requirements.
5. Status/confirmation/error message.
6. Back and Buy/Confirm/Continue controls.

The card fits inside the screen safe area; the two currency fields no longer share a long text line. Confirmation explains the proportional use of existing restaurant funds.

## Files changed

- `Assets/_Project/Save/CampaignSaveStore.NormalMoney.cs`: combined cash, preparation/allocation, cash purchase entry and receipt compatibility guard.
- `Assets/_Project/Save/CampaignSaveStore.cs` / `.Account.cs`: cache reset/invalidation and older-account-restore protection using the existing install journal.
- `Assets/_Project/MainMenu/GameMenu/Scripts/WalletManager/PlayFabWalletManager.cs`, `.Restaurants.cs`, `WalletUIController.cs`: menu display, cash checkout and retained legacy GC recovery.
- `Assets/_Project/MainMenu/GameMenu/Scripts/UI/GameModePopupController.RestaurantAccess.cs`, `RestaurantPurchaseView.cs`: editable prices, eligibility, confirmation and clone layout.
- `Assets/_Project/MainMenu/NewDesign/ShopItems/ShopPanelPopulator.cs`: combined cash in the shop/menu balance caption.
- `Backend/Tests/RestaurantCashHarness.cs`, `RestaurantCash.tests.cjs`: production-source-linked checks with simulated Unity/account-persistence boundaries.
- `Assets/_Project/Editor/RestaurantCashValidation.cs`: optional native validation menu; never runs automatically or builds a player.

## Checks and manual acceptance

Completed:

- Runtime and Editor assemblies compiled against the project's existing Unity references with zero errors. Output went to `.codex-checks/RestaurantCashCompile` outside the Assets folder; this was not a player build.
- `node Backend/Tests/RestaurantCash.tests.cjs`: **432 checks passed**, including ownership filtering, preserved target data, exact proportional deductions and rounding, insufficient funds, repeated purchase, checkpoint deductions, account isolation, interrupted-commit retry, old-restore receipt checks, and integer limits. Persistence and Unity native services are simulated in this suite.
- Targeted whitespace/diff checks. No real account balances were changed by these checks.

Not run: the native Unity validation command, Play Mode/visual checks, real disk-install interruption checks, live cloud upload/restore, multiplayer earnings flow, Windows player testing or Android device testing. Unity was kept open as requested; no second Editor instance, desktop automation, player builds or installations were used.

After scripts import, run **Dine In → Validation → Restaurant Cash and Purchase UI** outside Play Mode. It uses a preview scene and a uniquely named temporary directory; it does not save the open scene or change a live account. It checks the actual cloned popup's text geometry and the existing native account-journal recovery. Then manually verify:

- An account with Casual Dining ₱12,000 and Fast Food ₱8,000 sees ₱20,000 in the menu. In Casual Dining it sees/spends only ₱12,000; in Fast Food only ₱8,000. Fine Dining's unowned cash is excluded.
- With eligible days and enough pooled cash, confirm each restaurant once. Check the combined deduction, the paying careers, the unchanged GC balance and permanent access after restarting. Owned restaurants must not charge again.
- Try insufficient cash, missing day requirements, account switching, an old GC pending order and a cloud conflict. The popup must explain why buying is unavailable.
- Review both restaurants' popup text at supported UI scales and landscape aspect ratios/safe areas. Test long error messages, large balances, Back/close/Escape, and repeated clicks.
- On separate test accounts, check recovery and cloud synchronization before relying on purchases for the defense demonstration. Build Windows and Android yourself after acceptance; existing player artifacts do not contain these source changes.
