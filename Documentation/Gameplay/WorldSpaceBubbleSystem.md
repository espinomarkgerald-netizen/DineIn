# World Space Bubble System

## Purpose

Spatial lobby UI is rendered in the restaurant world instead of under
`CanvasMainHUD`. Gameplay state and button callbacks remain owned by their existing
customer, booth, tray, bill, and takeout scripts.

`CanvasMainHUD` remains a Screen Space Overlay canvas for fixed HUD elements,
menus, order tickets, payment confirmations, warnings, and tutorial overlays.

## Runtime Structure

Every spatial UI prefab already has `UIFollowWorldPoint` on its root. Calling
`UIFollowWorldPoint.Init` performs the migration at runtime:

1. The prefab records its authored scale and the gameplay HUD scale factor.
2. It moves beneath the scene-local `[World Bubbles]` hierarchy.
3. Its root receives a World Space `Canvas` and `CanvasScaler`.
4. A `GraphicRaycaster` is added only when the prefab contains a `Selectable`.
5. The canvas receives the gameplay camera and an isolated sorting order.
6. `LateUpdate` follows the world anchor, faces the camera, applies a small
   camera-depth offset, and updates scale.

Customer UI uses one additional layout stage. `CustomerGroup` projects the
active customer renderers and keeps `GroupUIAnchor` at the top-center of the
group's visible silhouette. The follower then places the lowest visible edge of
the bubble a fixed number of screen pixels above that anchor. This avoids both
character overlap and excessive floating at every camera zoom.

The runtime root belongs to the target anchor's loaded gameplay scene. Scene
unloading therefore destroys the root and every remaining bubble even when the
Bootstrap scene remains loaded.

## UIFollowWorldPoint Methods

| Method | Responsibility |
| --- | --- |
| `Init` | Stores the target, offset, and camera, then initializes World Space presentation. |
| `InitAboveTarget` | Anchors the bubble's visible lower edge above a target using a zoom-independent pixel gap and stack priority. |
| `InitializeWorldSpace` | Detaches the UI from the screen HUD and configures its Canvas, scaler, sorting, and optional raycaster. |
| `UpdateWorldSpacePose` | Places the bubble at its target, applies standard or above-target layout, and billboards it toward the camera. |
| `ResolveAboveTargetOffset` | Measures enabled, visible UI graphics so transparent prefab padding cannot create a false gap above the character. |
| `ResolveWorldScale` | Converts UI pixels to world units for orthographic or perspective cameras. |
| `UpdateScreenSpacePose` | Retains the previous screen projection path for explicitly Screen Space instances. |
| `SetVisible` | Applies target, camera, and `GameplayUIBlocker` visibility through the root `CanvasGroup`. |
| `WorldBubbleRuntimeRoot.GetOrCreate` | Resolves the current scene's `[World Bubbles]` hierarchy. |
| `WorldBubbleStackLayout.GetOffsetPixels` | Stacks simultaneous UI for the same target without permanent empty space. |

## Customer Placement

`CustomerGroup.ConfigureCustomerBubble` is the common entry point for order,
bill, money, table number, comments, eating, tip, and greeting UI. It preserves
the prefab's existing gameplay component and callback while applying the shared
visual layout.

- The default visible-edge gap is 8 screen pixels.
- Lobby1's `CustomerSystem` exposes this as the
  `Global Customer Bubble Layout` section with separate `Max Zoom In Offset` and
  `Max Zoom Out Offset` sliders. Both use a signed `-80` to `80` pixel range and update every
  visible bubble immediately during Play Mode. Intermediate camera zoom levels
  smoothly interpolate between those two endpoint values. `GroupSpawner` also
  injects the settings into newly spawned groups.
- The anchor comes from each customer's animated humanoid head bone, with
  `CC_Base_Head` as the model fallback. For groups, the UI uses the topmost head
  and horizontal center of all member heads.
- A slider value of `0` uses the head-bone position, negative values move the UI
  lower, and positive values move it higher. Transparent `RectTransform` padding
  remains ignored.
- The table/order number follows the customer group instead of a remote booth
  point.
- Action bubbles occupy the first stack layer.
- The line-patience UI uses a higher priority, so it sits above an action button
  only while both are visible and moves back down when the button closes.
- Particle, trail, and line renderers are excluded from customer bounds so dust
  and movement effects cannot push UI away from the character.

## Screen Size Preservation

The default `preserveScreenSize` setting keeps the previous apparent UI size while
the orthographic camera zooms. The conversion is based on camera height:

`world units per UI unit = (orthographic size * 2 / camera pixel height) * HUD scale factor`

`visualScale` applies final per-prefab tuning. Disabling `preserveScreenSize` uses
the fixed `worldUnitsPerUiUnit` value instead, allowing bubbles to grow or shrink
on screen as the camera zooms.

## Migrated Spatial UI

- Customer order, bill, money, table number, comments, eating, patience, and tip UI
- Customer greeting and host speech bubbles
- Food tray and takeout bag pickup UI
- Cashier bill-paper pickup UI
- Money popups attached to world anchors
- Booth assignment indicator

The dirty-booth cleaning UI and staff nametags were already World Space canvases
and continue using their existing components.

## Intentionally Screen Space

- `CanvasMainHUD`, pause/settings UI, warnings, and game menu
- `OrderFlowManager` order-ticket and payment-confirmation UI
- `TutorialArrowManager` screen tutorial overlays

## Interaction Rules

Interactive bubbles receive their own `GraphicRaycaster` and assign
`Canvas.worldCamera`. Passive bubbles do not block pointer input or camera panning.
The existing EventSystem and button callbacks continue to process mouse and touch
input; no gameplay interaction method is duplicated in this presentation layer.

This separation is intentional for the future playable manager. Worker and player
abilities should call the same existing interaction methods behind each button.
`UIFollowWorldPoint` only controls presentation, visibility, and raycast setup; it
does not own role authorization or task execution.

## Validation Checklist

1. Test close, middle, and far zoom while each customer bubble is visible; confirm
   its lowest edge keeps a small consistent gap above the visible customer group.
2. Click order, bill, money, tray, bill-paper, and takeout controls with mouse and touch.
3. Confirm passive comments, patience, eating, table number, and tip UI do not block camera input.
4. Open a gameplay-blocking panel and confirm spatial bubbles hide and restore.
5. Test multiple customer groups and verify bubbles remain isolated under `[World Bubbles]`.
6. Confirm fixed order tickets, payment confirmations, warnings, and tutorial overlays remain on the HUD.
7. Test Lobby1 at desktop and Android aspect ratios.
8. Show a greeting/action bubble while line patience is active; confirm the
   patience bar stacks above it and returns to the customer when the action closes.
