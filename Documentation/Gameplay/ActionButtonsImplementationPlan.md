# Action-button skin and serving-guidance implementation plan

Status: implemented following user approval (2026-11-07). Shared action artwork, outlined editable labels and local held-food serving guidance are connected. Runtime/Editor compilation and isolated Unity checks passed; live gameplay, touch, multiplayer and audio listening remain unverified. See GamePolishPlan for the exact coverage.

## Changes required by the source review

- `TableNumberUI` is used by customer/table guidance and by food-tray number displays. Only a table-bound customer bubble may become Serve food. Keep tray identifiers and takeout guidance as identifiers.
- `FoodTray.Matches()` compares order numbers only. A green serving action additionally requires the current customer-group identity, its assigned booth and the same current order. Never broaden this helper globally merely to skin UI.
- Multiplayer `CanAttemptServe()` permits a serving attempt while holding a network-locked tray; it does not itself establish the correct destination. Reuse `HasLocalDeliveryGuidance` for local carrier, customer/order identity and recovery restrictions, together with the existing interaction eligibility checks.
- Fine Dining's `PlayerTaskBubbleFocus` compacts controls and caches captions. Numeric captions are identified by parsing their text; changing the numeric field to `TABLE 04` would break that protection. Keep a dedicated numeric TMP field, with any TABLE prefix separate. Refresh focus/caption bindings when switching presentation states, and prioritize the matching local delivery action through the existing focus policy.
- `UIFollowWorldPoint`, button animators and button-sound components already own positioning, reveal, press and click feedback. Reuse these owners; avoid competing scale tweens and duplicate listeners/sounds.

## Integration and behavior

1. Apply the sprite packs to existing gameplay controls: Greet Customer, Seat Table, Take order, number identifiers, Clean, Ask for bill, Collect bill, Collect payment, pickup, station/navigation and Pour UI controls. The entire management reservation UI is excluded. Keep existing Button instances, callbacks and action conditions.
2. Preserve cash/card distinctions, amounts, reservation state text, tutorial control names and original action labels, including existing bill-delivery steps. Illustrative labels in the asset previews do not define new commands.
3. Then extend `TableNumberUI` with two presentations: NumberOnly and ServeAvailable. Reuse SetGroup/SetBooth/SetNumber, the existing cleanup check, and `WaiterHands.OnHandsStateChanged`; subscribe/unsubscribe with the bubble lifecycle. Repaint only on a state change. Keep the inexpensive existing state check for changes that do not originate in hands, such as departure, role changes or reassignment.
4. Show ServeAvailable only after successful pickup is reflected in the active local waiter's held tray. Require a live pending customer, the current assigned booth/customer association, matching tray TargetGroup and current order, plus existing delivery restrictions. In multiplayer also require the established local-carrier guidance. Ordinary number visibility follows the existing mode-specific rules; do not manufacture remote players' bubbles.
5. ServeAvailable displays the green Serve food button, cloche icon and attached table-number badge. Other tables keep their existing contextual controls. Revalidate on click, then route through the existing movement/delivery interaction and its arrival-time checks. Do not deliver immediately, invoke both delivery paths or treat an attempted request as completed. Retain existing pending-request/claim guards and cancellation recovery.
6. Put-down, role loss, tray destruction, target departure/reassignment and carry recovery remove the actionable state promptly. Restore an ordinary number only if existing visibility rules allow it. Successful delivery removes delivery guidance. Number changes caused by table reuse cannot retarget stale food.
7. Leave takeout service and Fine Dining wine/water/refill validation with their current owners. Pouring stays a drag interaction. Cooking-state icons show existing values; they do not add cooking actions. No new save format, global manager or network event is planned.

## Layout and animation requirements

- Compose separate sliced background, icon, editable action label and number badge. Use the supplied slice borders and retain transparent padding. Size content against the visible artwork and actual canvas hierarchy, not a fixed legacy wrapper or the PNG's full transparent bounds.
- Measure label preferred width plus icon, gap and padding. Prefer one line; grow the button within the safe viewport. At narrow limits allow deliberate word-boundary wrapping and increase height. Never silently ellipsize an action, amount or table number, or shrink below the project's readable text scale.
- Give badges and the thicker patience bar reserved space above the button. Decorative graphics ignore raycasts. Keep the existing clickable target available in pressed/compact states. World controls remain attached to their customer/table through camera movement and the HUD region; hide them when their target is offscreen. Preserve same-target stacking and Fine Dining focus. Do not relocate world controls into HUD-safe screen positions.
- Keep layout/follow transforms separate from animated visuals. Reuse the current press animator and its timing; never attach a second owner to the same transform. Initialize skin/base colors before animation capture, or use the animator's existing base-color setter when styling later.
- The number-to-Serve transition uses a short, nonblocking fade/settle, approximately 0.15 seconds, once per actual state change. Do not continuously pulse or replay on snapshots/reconnect. It must remain clickable during cosmetic transitions. Reduced Motion uses the final geometry with a restrained fade, without movement.
- Finish/reset cosmetic state on pointer exit, release, cancellation, disable, scene unload and interrupted transitions. A hold-progress ring reads an existing real progress value only; a Focused sprite is not a progress indicator and does not add hold mechanics.

## SFX requirements

- Use existing GameplayButtonSound for one accepted HUD click and its existing blocked-feedback path. Do not add per-skin AudioSources or duplicate onClick sound listeners.
- Keep confirmed service, payment and cleaning cues at their existing successful gameplay stages through GameplaySfx and its mixer. Asking for a bill, collecting paper or carrying payment must not play a credited-payment sound.
- Keep the Serve-state appearance silent. Preserve accepted pickup feedback where already present; visual restoration, reconnects and snapshot refreshes generate no success cue.
- Multiplayer success feedback stays authoritative and deduplicated. A rejected request or cancelled path must not generate a success animation or cue.
- Preserve pouring's real-flow audio gating and stop behavior on cancel/close/disable/pause. Respect Master/SFX volume, mute and current throttling. Cooking feedback continues through its existing owner.

## Acceptance gates

1. **Static/asset:** preserve original button identities/listeners and prefab/controller references; verify sprite rects, borders, pivots, font glyphs and every restaurant/mode binding. Confirm only table-bound bubbles gain serving presentation and no animator/sound ownership duplicates are introduced.
2. **Targeted behavior:** successful and rejected pickup, wrong tray, reused table number, stale customer reference, put-down/re-pickup, switching roles, cancellation while walking, target departure, rapid repeated clicks, completed delivery, destroyed tray and scene cleanup. Run the existing take-order, complete bill chain, cash/card payment, cleaning, reservation and pouring flows after skinning.
3. **Native Unity visual:** use the real prefab ancestry, authored follow/scale and focus components. Render/check the longest labels, two/three-digit table identifiers, large currency values, all four button states, crowded adjacent tables, camera zoom, landscape/portrait, safe areas and Large Text. Check glyph/rect bounds and inspect images; compilation or asset import cannot establish no overlap. Required text must remain visible and the badge, icons and neighboring controls must not collide.
4. **Live animation/input:** desktop pointer and mobile touch, pointer exit/cancel, touch dragging, keyboard submit where supported, Reduced Motion, pause, rapid reopening, disabled buttons and interruption mid-transition. Confirm actions are not delayed by decoration and progress follows the actual interaction.
5. **Live audio/network:** listen for one click and the correct confirmed action cue; test Master/SFX mute and pouring loop stop. Exercise host, acting guest and other guest, plus late join/reconnect/recovery, in the relevant Casual Dining, Fast Food and Fine Dining Campaign/Tutorial/Multiplayer flows. Confirm local-only guidance, no duplicate completion/payment and no replayed success feedback.

Record compilation, static checks, rendered inspection, live behavior and listening separately in GamePolishPlan. Fix discovered failures before claiming the corresponding gate passed; any unrun gate remains explicitly unverified.

## Implementation notes

- `GameplayActionButtons.asset` is the shared editable art/font/sizing reference. Existing customer controllers apply the skin using their real number/amount fields. The existing installer handles kitchen, cleaning-choice and pouring Back controls. Reservation panels, including their tabs, actions and cards, retain their original appearance.
- Lilita action labels and Nunito amounts use dedicated black-outline material presets (`OUTLINE_ON`, width 0.22). The pre-existing font assets were not edited by this implementation.
- The new Serve click uses existing deferred movement, revalidates on arrival, then calls one existing delivery handler. Direct serving owners additionally reject food for a different customer even if its number matches. Wine/refill and takeout retain their own flows.
- Expanded action bubbles and comments use target attachment; the prior HUD-safe relocation opt-in was removed following the user's live screenshot. Preview fixtures retain original prefab hierarchy and authored scale. Only actual caption/value/size changes trigger layout.
- Appearance changes fade content only. Existing press owners, native SpriteSwap states and GameplayButtonSound retain input feedback; gameplay owners retain confirmed action SFX.
- Readability revision: action controls use worldVisualScale 1.1 (previously 0.8), with a separate 1.4 multiplier for Fine Dining's fixed-width sizing. Customer comments use the new sliced speech panels, separate tails and illustrated mood badges. The existing comment messages, mood mapping, duration and outcome behavior remain unchanged.
- Reception uses the existing Greet Customer -> Seat Table action and original caption references/icons. The patience bar remains the original Slider, styled at 20 units high (previously 8) above the button, with editable gap/height and existing value/color ownership.
