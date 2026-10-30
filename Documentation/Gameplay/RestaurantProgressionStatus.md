# Restaurant progression implementation status

## Implemented

- Existing legacy/new/partial ownership migration and old snapshot checksum formats retained. Corrected the prior missing ownership assignment in ApplyProfile; account switching now installs that account's ownership, and same-account multiplayer credit survives cloud restore.
- Campaign settlement records unique successful day numbers in each career, excludes terminal failures, and commits results immediately. Success is frozen at settlement, so paying for failure recovery cannot retroactively turn a failed day into experience. No historical success is inferred from currentDay.
- Multiplayer records carry explicit per-participant successfulDays. Only present participants receive successful settled-day credit; failure, disconnection and unfinished days grant none. Existing success is never cleared on failure/departure. Room protocol shared-session-22 prevents mixing older clients.
- Stable restaurant/run/day receipt IDs use the existing ownedItemIds profile collection with reserved restaurant-day: identifiers. These travel through existing account/cloud profile transport. Repeated callbacks and reconnects reuse the same IDs. Failed local writes roll back new in-memory IDs and retain pending work. The existing run outbox/backend returns verified successful-day credit for offline/retry recovery.
- Campaign and multiplayer success counts feed the same Inspector-configurable requirements (10 Casual; 30 Casual plus 15 Fast Food). The existing panel caches progress on local-write/account changes instead of reading save files every frame.
- Existing sprite darkening remains; ownership signature changes immediately restore original colors. Existing Play/Campaign/Multiplayer/Tutorial entrypoints are gated; multiplayer create and joined-room checks also enforce ownership.
- The existing panel now quotes Classic catalog prices and authoritative GC through PlayFabWalletManager, requires a second confirmation click, displays missing experience/funds, and offers order recovery.
- Classic order sequence integrated through PlayFabWalletManager.Restaurants.cs. Each account/restaurant journal is atomically written before payment, with unique client transaction ID, server order ID, account, price, sent markers and explicit payment/ownership states. Ownership is persisted only after ConfirmPurchase returns the restaurant or GetPurchase reports Succeeded. Failed ownership writes remove the temporary unlock and leave the confirmed journal for retry without charging again.

## Recovery semantics and limitations

Opening the restaurant panel after restarting discovers its pending journal and queries GetPurchase. Successful orders only retry ownership persistence. An Approved order may submit one initial confirmation only if its durable marker proves confirmation has never been sent. Unknown payment/confirmation requests are never replayed. Only a journal proving no payment was sent, or authoritative Failed status, permits a fresh order. All other statuses stay blocked for later status queries/support. Completed journals remain on disk for same-device recovery after an older cloud restore; order references are also included in the existing cloud profile ownedItemIds collection. A journal with invalid data is preserved and blocks charging.

Inventory preflight blocks an already visible restaurant item but is not an atomic uniqueness lock. Inventory alone never unlocks a restaurant. Losing the journal/order reference requires support, even if inventory contains the item. A crash between persisting a sent marker and sending can leave an unpaid/unconfirmed order blocked indefinitely. Simultaneous purchases from different devices are not serialized by the existing Classic API. This implementation therefore does NOT claim provider-level idempotency or global exactly-once charging. No fake CloudScript payment guarantee was added.

Successful multiplayer credit uses the existing host-authority trust model; this does not add a dedicated authoritative gameplay server. Backend receipt retention remains the existing eight slots. Already locally credited success is permanent in the profile; recovery of never-persisted credit after its host receipt has aged out is not guaranteed. Protect/save local profiles and deploy the updated result handlers.

## Deployment (not performed)

See Backend/PlayFab/README.md. Import RestaurantsV1.catalog.json into the existing title as non-default Classic catalog RestaurantsV1 with permanent non-consumable items fastfood/finedining. Sample prices 100/200 GC match one/two existing 100-GC costume prices; these are provisional, not verified earning-rate balance. Edit catalog prices for tuning. Verify Classic VC order APIs and the title API policy. No replacement GC or new payment service is introduced.

Merge/publish updated MultiplayerRunsV2.js into the existing Classic CloudScript revision, preserving unrelated handlers and existing statistics. Deploy matching protocol-22 clients together. No credentials were used, backend published, catalog imported, or live payments made.

## Files changed in the completion pass

- Core/GameFlowManager.cs; Save/GameSaveManager.cs; Save/LocalSaveManager.cs; Save/CampaignSaveStore.Account.cs.
- MainMenu/GameMenu/Scripts/UI/GameModePopupController.cs and GameModePopupController.RestaurantAccess.cs.
- MainMenu/GameMenu/Scripts/WalletManager/PlayFabWalletManager.cs and new PlayFabWalletManager.Restaurants.cs (+ meta).
- Networking/Multiplayer/MultiplayerSessionManager.cs; MultiplayerRunRecords.cs; Networking/Photon/MultiplayerMenuController.cs; Tutorials/TutorialGameModeEntry.cs.
- Backend/PlayFab/MultiplayerRunsV2.js; new RestaurantsV1.catalog.json; backend README; this status document.

## Verification

Roslyn compilation with existing Unity references and current runtime sources passed with warnings. Backend JavaScript syntax and diff whitespace checks performed. No Play Mode, automated playtesting, live backend calls, or device testing performed.

Manually verify new/legacy/partial profiles; account switching/cloud restore; campaign failure then paid continue; multiplayer successful day followed by failure/disconnect; repeated callbacks/rejoin; purchase confirmation and restart; local ownership write failure; unknown payment recovery; phone panel layout and sprite restoration. Use disposable accounts/save copies for payment and failure scenarios. Full live safety is unverified until external deployment and manual acceptance.
