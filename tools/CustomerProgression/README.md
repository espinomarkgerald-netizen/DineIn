# Campaign customer introductions and Fast Food visitors

## Schedule and configuration

Normal Lobby2 starts with Green, Pink and Blue eligible on Day 1. Their existing scene weights remain **0.1 / 0.9 / 0.1**; these are relative weights, not percentages. No discovery briefing is shown for those familiar visitors in Fast Food. Lobby1 keeps its existing day-manager unlock schedule and legacy attributes.

Fast Food has its own campaign day and save (`dinein_fastfood_save.json`); Casual Dining's accumulated day is not used. Additional profiles under `Assets/_Project/Art/Models/Customer/New Alien Models/PlayerType` hold the editable days, weights, portraits, prefabs and behavior values.

| Profile | Provisional FF day | Added relative weight | Behavior defaults |
|---|---:|---:|---|
| Purple | 2 | 0.7 | Baseline timing, patience and movement |
| Orange | 5 | 0.2 | 0.75x ordering duration, 0.7x eating duration; 70% takeaway / 30% dine-in |
| Elderly | 10 | 0.15 | 0.7x walking speed, 1.4x ordering duration; 3x waiting-patience drain, matching existing Pink |
| Yellow | 15 | 0.15 | 3–4 total members (mobile cap currently 3), including 1–2 children; 1.5x eating duration |

**Later dates are provisional, not confirmed design decisions.** Additional types cannot unlock before Day 2. Existing types remain in the mix. Eligibility does not depend on acknowledging a briefing.

Portrait references explicitly map 1=Green, 2=Blue, 3=Pink, 4=Orange, 5=Yellow, 6=Purple, 7=Elderly. Enum IDs remain Green=0, Pink=1, Blue=2; additions are Purple=3, Orange=4, Elderly=5, Yellow=6.

Blue's existing asset specifies ordinary patience, messy tables and **2x eating duration**, not a separate slower-ordering mechanic. It remains unchanged; its introduction describes the existing meal behavior instead of promising an unwired ordering difference.

## Integration

- `GroupSpawner` retains the ordinary shift/population admission path and samples one service-mode roll per visit. New identities use their corresponding rigged prefabs, not random Green visual substitutions. Normal-scene legacy visual-variant lists were cleared for that reason.
- `CustomerGroup.CampaignVisit` applies visit-specific profiles and child composition. Children are the last members, leaving the first adult as the existing representative. Group order and seat counts include everyone.
- Elderly guests use actual staffed/player-operated counters, receive priority behind any already-serving queue front, and use existing table delivery for dine-in. Existing task claims arbitrate helpers; active owned counter work pauses service-wait patience. Own queue movement and ordering/thinking do not drain waiting patience. Table travel keeps its existing speed-aware bounded arrival check.
- Orange timing affects both kiosk and staff counter work, as well as the existing thinking/eating timers. Takeaway still completes through actual payment, order receipt and departure.
- Yellow children resize only the visual Animator subtree; navigation roots and colliders are unchanged. Every normal Lobby2 table has an editable, local dining-only play bound. Optional short complete NavMesh routes start around 10 seconds into eating, within a 1.5-unit radius, with a 5-second travel bound per leg. Seats remain reserved. Ending the meal/visit or disabling the group cancels play; departure never waits for it.
- Child movement uses the existing distance-sampled `HygieneWalker` and `HygieneState` floor marks (existing global maximum 512), not a second mess system.
- `RestaurantUnlockCoach` presents eligible, unseen guests during safe preparation after existing notices. The authored campaign guide reuses Big Boss dialogue/art and rectangular iris presentation, without a tutorial runner, hand cue or practice action. Final acknowledgement consumes the gesture before closing.
- Completion IDs use the existing career-scoped `restaurantBossTipsShown` collection with restaurant/type keys. Pending first encounters use the additional save list `pendingCustomerEncounterIDs`. They consume an ordinary compatible spawn opportunity and then return to the weighted mix. Loading/resetting the existing coach clears stale presentation/callback state.

## Changed areas

- Customer profile assets (seven), `CustomerTypeProfile`, `GroupSpawner` plus campaign partial, `CustomerGroup` plus visit partial, and `CustomerAgent`.
- Normal `Lobby1.unity` and `Lobby2.unity`; campaign `RestaurantUnlockGuide.prefab`.
- `GameDayManager`, campaign coach/UI, `GameSaveData`; read-only warning visibility property for notice coordination.
- Existing Fast Food restaurant/table, counter/kiosk timing, takeout queue/timeout, and autonomous cashier paths.
- `ConfigureCustomers.cs`: explicit Edit Mode authoring utility. Not a runtime bootstrap or automatic build/play hook. Later profile tuning is preserved on rerun, but scene play bounds are reauthored.
- `InspectCustomers.cs`: focused read-only serialized-reference inspection, not a gameplay test.

## Verification and manual follow-up

Performed: Unity source import/compilation; read-only identity/portrait/rig-reference checks; normal Lobby2 starting flags and existing weights; 17 authored play zones; guide showcase bindings and absence of a tutorial runner; focused lifecycle/save/diff inspection.

Not performed: Play Mode, builds, automated gameplay walkthroughs, desktop/browser automation, statistical spawn sampling, mobile visual verification, or live save round trips.

Manually verify fresh/resumed Lobby2 Day 1, later unlock/catch-up briefings and reload without repeats; Orange actual bag collection; Elderly player/bot handoff and patience; Yellow child ground alignment, seats, safe return/departure and cleanable footprints; portrait/dialogue spacing on 16:9 and mobile. Compilation and references do not establish runtime behavior.
