# Multiplayer notice artwork V1

Six transparent component PNGs and the source sprite sheet for the approved disconnect/removal modal design. Labels and status text are intentionally absent and must remain editable UI text.

- PanelBody, HeaderTab, PrimaryButton and StatusStrip: use Image.Type.Sliced with supplied import borders.
- DisconnectedCable and RemovedDoor: use Image.Type.Simple, preserveAspect, centered pivot.
- manifest.json records source atlas rectangles in top-left coordinates and borders in Unity left/bottom/right/top order. Individual PNG import metadata is included; the source atlas is supplied as artwork with a rectangle manifest.
- Use an existing UI Image circle/spinner for reconnection status; it is not baked into the artwork.
- Single shared panel shell supports both states. Keep return-to-lobby labels in the game's rounded heading font, and body/status copy in its existing readable body font.

Integrated through Resources/UI/MultiplayerConnectionNotice.prefab and the existing MultiplayerSessionUI. The prefab exposes sprite/font references, copy and animation tuning; its view builds the UI hierarchy at runtime. Component slicing is deterministic cropping of the generated sheet. Live Unity 9-slice rendering, text layout, animations and reconnect/moderation behavior have not been tested.
