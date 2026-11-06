# Customer comment sprites V1

Nine transparent blank sprites: Happy / Neutral / Angry panel, tail and mood badge.
The existing illustrated mood faces are referenced from DayPanelSpritePackV2; they are not duplicated.

- Panels: 512 x 256; 9-slice borders **80 left / 80 bottom / 80 right / 80 top**.
- Tails: 128 x 96, separate from the sliced panel so they never stretch.
- Badges: 128 x 128; keep square with Preserve Aspect.
- All pivots: center. All labels remain editable TMP text.
- Named atlas: CustomerCommentsV1.png (1024 x 1024), with Unity metadata and rectangle/border manifest CustomerCommentsV1.json.

Runtime: CustomerCommentBubble styles the existing CustomerComments prefab from both customer thought paths. Message text, mood decisions, tip amounts, lifetime and outcome/audio owners remain in CustomerGroup. Decorative graphics ignore raycasts. The frame grows vertically for longer messages and Large Text, using the original world-follow/reveal owner.

Art and layout tuning are in Resources/UI/GameplayActionButtons.asset. The comment text uses Nunito Sans with dark ink on cream. Gameplay action labels retain their black outlines. Fine Dining has separate readable widths for action controls and comment bubbles; the management reservation UI is excluded from the action skin.

Rebuild these simple vector assets with Tools/Art/BuildCustomerComments.ps1. The source uses the palette and rounded shapes of the existing action pack. No text is baked into any component. Native Unity verification is in Tools/Verification/ActionButtons/Run.ps1; isolated renders do not establish live gameplay or device behavior.
