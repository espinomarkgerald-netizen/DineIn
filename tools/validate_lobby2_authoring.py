"""Read-only serialized-asset checks; does not launch Unity or prove playability.

Run with Python 3: python tools/validate_lobby2_authoring.py
Unity structure/navigation validation and the human-control checklist are separate.
"""
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[1]
ASSETS = ROOT / "Assets/_Project"
DOC = re.compile(r"^--- !u!(\d+) &(-?\d+)[^\n]*\n.*?(?=^--- !u!|\Z)", re.M | re.S)


def read(path):
    matches = list(DOC.finditer(path.read_text(encoding="utf-8-sig")))
    result = {int(m[2]): (int(m[1]), m[0]) for m in matches}
    assert len(matches) == len(result), f"Duplicate file IDs: {path}"
    assert result, f"No Unity objects: {path}"
    for ident, (_, text) in result.items():
        for ref_id in re.findall(r"\{fileID: (-?\d+)\}", text):
            assert int(ref_id) == 0 or int(ref_id) in result, (path, ident, ref_id)
    for ident, (typ, text) in result.items():
        for key, expected in (("m_GameObject", (1,)), ("m_PrefabInstance", (1001,)),
                              ("m_Father", (4, 224))):
            target = reference(text, key)
            if target:
                assert result[target][0] in expected, (path, ident, key, target, "wrong object type")
        if typ in (4, 224):
            for child in array(text, "m_Children"):
                assert result[child][0] in (4, 224), (path, ident, child, "child is not a transform")
        if typ == 1:
            for target in re.findall(r"^  - component: \{fileID: (-?\d+)\}", text, re.M):
                target = int(target)
                assert result[target][0] not in (1, 1001), (path, ident, target, "invalid component")
    return result


def value(text, key):
    match = re.search(r"^  " + re.escape(key) + r":(.*)$", text, re.M)
    return match[1].strip() if match else ""


def reference(text, key):
    match = re.search(r"fileID: (-?\d+)", value(text, key))
    return int(match[1]) if match else 0


def array(text, key):
    match = re.search(r"^  " + re.escape(key) + r":[ \t]*\n((?:  - .*\n)*)", text, re.M)
    return [int(x) for x in re.findall(r"fileID: (-?\d+)", match[1])] if match else []


def component(docs, guid):
    return [(i, text) for i, (typ, text) in docs.items()
            if typ == 114 and f"guid: {guid}," in value(text, "m_Script")]


def vector(text, key):
    return tuple(float(x) for x in re.findall(r"[xyzw]: ([^,}]+)", value(text, key)))


def guid(path):
    return re.search(r"guid: (\w+)", Path(str(path) + ".meta").read_text())[1]


def main():
    booth_guid = guid(ASSETS / "Customers/Booth/Booth.cs")
    table_guid = guid(ASSETS / "Restaurant/FastFoodTable.cs")
    delivery_guid = guid(ASSETS / "Restaurant/Items/BoothDeliverInteractable.cs")
    cleaning = ASSETS / "Restaurant/Booths/BoothMessCleanUI.cs"
    assert "public class BoothMessCleanUI" in cleaning.read_text(), "Cleaning script filename/class mismatch"
    cleaning_guid = guid(cleaning)
    outline_guid = guid(ROOT / "Assets/QuickOutline/Scripts/Outline.cs")
    # QuickOutline skips submesh preparation for unreadable models, leaving only
    # the last material section outlined. Check the import setting, not Editor-only mesh access.
    model_meta = ASSETS / "Art/Models/FastFoodRestaurant/Fast Food Revamp.fbx.meta"
    assert re.search(r"^    isReadable: 1$", model_meta.read_text(), re.M), "Fast Food model must enable Read/Write for complete outlines"
    assets = {}
    for path in sorted((ASSETS / "Restaurant/Prefabs/FastFood").glob("*.prefab")):
        docs = read(path)
        outlines = component(docs, outline_guid)
        assert len(outlines) == 1, (path, "expected one authored outline")
        assert value(outlines[0][1], "m_Enabled") == "0", (path, "outline must start hidden")
        assert value(outlines[0][1], "outlineWidth") == "4", (path, "match Lobby1 outline width")
        assets[guid(path)] = (path, docs)
        roots = 0
        for ident, (typ, text) in docs.items():
            if typ not in (4, 224):
                continue
            parent = reference(text, "m_Father")
            if parent:
                assert ident in array(docs[parent][1], "m_Children"), (path, "orphan transform", ident)
            else:
                roots += 1
            for child in array(text, "m_Children"):
                assert reference(docs[child][1], "m_Father") == ident, (path, "wrong parent", child)
        assert roots == 1, (path, "multiple roots", roots)
        if not component(docs, table_guid):
            continue
        assert len(component(docs, booth_guid)) == len(component(docs, table_guid)) == 1
        assert len(component(docs, delivery_guid)) == len(component(docs, cleaning_guid)) == 1
        _, booth = component(docs, booth_guid)[0]
        _, table = component(docs, table_guid)[0]
        _, delivery = component(docs, delivery_guid)[0]
        root_go = reference(booth, "m_GameObject")
        assert reference(outlines[0][1], "m_GameObject") == root_go, (path, "outline must cover the whole seating root")
        assert value(outlines[0][1], "precomputeOutline") == "1", (path, "enable editor outline baking")
        assert value(docs[root_go][1], "m_Layer") == "7"
        root_transform = next(i for i, (t, d) in docs.items()
                              if t == 4 and reference(d, "m_GameObject") == root_go)
        seats = array(booth, "seats")
        assert len(seats) == (2 if "Long Table" in path.name else 4), path
        assert len(set(seats)) == len(seats), path
        assert len({vector(docs[s][1], "m_LocalPosition") for s in seats}) == len(seats), path
        for seat in seats:
            assert reference(docs[seat][1], "m_Father") == root_transform
        for field in ("approachPoint", "tableLookTarget", "tableNumberAnchor"):
            assert reference(docs[reference(booth, field)][1], "m_Father") == root_transform
        assert reference(booth, "menuBookPrefab") == reference(booth, "currentGroup") == 0
        assert reference(booth, "cleanUI") == component(docs, cleaning_guid)[0][0]
        assert reference(delivery, "booth") == component(docs, booth_guid)[0][0]
        rotation = vector(docs[reference(delivery, "tableFoodSpawn")][1], "m_LocalRotation")
        x, y, z, w = rotation
        assert 2 * (y*z - w*x) > .99, (path, "tray model's local Z must face up at the tabletop")
        assert value(docs[reference(docs[reference(delivery, "tableFoodSpawn")][1], "m_GameObject")][1], "m_Name") == "TableFoodSpawn"
        renderer = docs[reference(table, "furniture")][1]
        art_go = reference(renderer, "m_GameObject")
        art_transform = next(i for i, (t, d) in docs.items()
                             if t == 4 and reference(d, "m_GameObject") == art_go)
        assert reference(docs[art_transform][1], "m_Father") == root_transform
        assert "furnitureName:" not in table and "furnitureRoot:" not in table

    scene = read(ASSETS / "Scenes/RoleBased/Lobby2.unity")
    scene_text = (ASSETS / "Scenes/RoleBased/Lobby2.unity").read_text(encoding="utf-8-sig")
    scene_roots = list(re.finditer(r"^--- !u!1660057539 &9223372036854775807\n", scene_text, re.M))
    assert len(scene_roots) == 1, "Expected one SceneRoots record"
    assert not re.search(r"^--- !u!", scene_text[scene_roots[0].end():], re.M), "SceneRoots must be the last serialized scene object"
    instances = {}
    for ident, (typ, text) in scene.items():
        if typ != 1001:
            continue
        source = re.search(r"guid: (\w+)", value(text, "m_SourcePrefab"))
        if source and source[1] in assets:
            instances[ident] = assets[source[1]]
            for match in re.finditer(r"    - target: \{fileID: (-?\d+), guid: (\w+), type: 3\}", text):
                assert match[2] == source[1] and int(match[1]) in assets[source[1]][1], (ident, match[1])
    for ident, (typ, text) in scene.items():
        instance = reference(text, "m_PrefabInstance")
        if instance in instances:
            assert reference(text, "m_CorrespondingSourceObject") in instances[instance][1], ident
    restaurant = component(scene, guid(ASSETS / "Restaurant/FastFoodRestaurant.cs"))[0][1]
    queue = component(scene, guid(ASSETS / "Restaurant/Takeout/TakeoutQueueManager.cs"))[0][1]
    queue_points = array(queue, "queuePoints")
    assert len(queue_points) > 0 and len(set(queue_points)) == len(queue_points), "Missing/duplicate queue anchors"
    queue_parent = reference(scene[queue_points[0]][1], "m_Father")
    assert value(scene[reference(scene[queue_parent][1], "m_GameObject")][1], "m_Name") == "Counter and Waiting Points"
    for point in queue_points + [reference(queue, "orderPoint"), reference(queue, "overflowRoot")]:
        assert point and reference(scene[point][1], "m_Father") == queue_parent, "Queue anchors must be editable under services"
        assert point in array(scene[queue_parent][1], "m_Children"), "Missing queue hierarchy child"
    assert len(component(scene, guid(ASSETS / "Restaurant/Managers/TapOutlineSelector.cs"))) == 1
    bag = read(ASSETS / "Art/Models/3D Models/PaperBag.prefab")
    assert len(component(bag, outline_guid)) == 1, "Missing takeaway bag outline"
    dining = array(restaurant, "diningTables")
    assert len(dining) == len(set(dining)) == 15, "Expected the 14 original tables plus the second round table"
    counts = {}
    for ident in dining:
        instance = reference(scene[ident][1], "m_PrefabInstance")
        path, prefab = instances[instance]
        assert reference(scene[ident][1], "m_CorrespondingSourceObject") == component(prefab, booth_guid)[0][0]
        counts[path.stem] = counts.get(path.stem, 0) + 1
    assert counts == {"Fast Food Booth": 8, "Fast Food Long Table": 5, "Fast Food Round Table": 2}, counts
    assert len(instances) == 20, "15 tables, two cashiers, two kiosks, and sink must remain prefab instances"
    station_guid = guid(ASSETS / "Restaurant/FastFoodServiceStation.cs")
    stations = component(scene, station_guid)
    assert len(stations) == 4 and sum(value(d, "kind") == "1" for _, d in stations) == 2
    assert set(array(restaurant, "serviceStations")) == {i for i, _ in stations}
    for field in ("queue", "flow"):
        assert len({reference(d, field) for _, d in stations}) == 4, "Stations cannot share service state"
    for _, station in stations:
        q = scene[reference(station, "queue")][1]
        f = scene[reference(station, "flow")][1]
        assert reference(f, "queueManager") == reference(station, "queue")
        assert reference(f, "kitchenManager") == reference(restaurant, "kitchen")
        assert reference(station, "staffApproach") in scene
        assert len(array(q, "queuePoints")) >= 2 and reference(q, "orderPoint") in scene
    for _, station in stations:
        assert all(float(value(station, field)) > 0 for field in ("kioskOrderSeconds", "cashierOrderSeconds", "cashierPaymentSeconds"))
        assert "kioskPayHereChance:" not in station, "Kiosks must use automatic payment"
        companions = array(station, "companionPoints")
        assert len(set(companions)) == 3
        order = reference(scene[reference(station, "queue")][1], "orderPoint")
        assert all(reference(scene[p][1], "m_Father") == order for p in companions)
        assert len({vector(scene[p][1], "m_LocalPosition") for p in companions}) == 3
    for field, count in (("outsideWaitingPoints", 2), ("customerPickupSlots", 3), ("waitingPoints", 6)):
        points = array(restaurant, field)
        assert len(set(points)) == count, (field, "missing/duplicate positions")
        assert len({vector(scene[p][1], "m_LocalPosition") for p in points}) == count
    previews = component(scene, guid(ASSETS / "Restaurant/FastFoodShelfPreview.cs"))
    assert len(previews) == 2 and {value(p, "storageType") for _, p in previews} == {"0", "1"}
    preview_path = ASSETS / "Restaurant/RestockRoom/Prefabs/StoredBoxPreview.prefab"
    preview = read(preview_path)
    assert not any(t in (54, 64, 65, 135, 136) for t, _ in preview.values()), "Preview has physical interaction components"
    allowed_ui = {"f4688fdb7df04437aeb418b961361dc5", "fe87c0e1cc204ed48ad3b37840f39efc", "0cd44c1031e13a943bb63640046fad76"}
    for t, doc in preview.values():
        if t == 114:
            assert any(g in value(doc, "m_Script") for g in allowed_ui), ("Preview includes non-art script", value(doc, "m_Script"))
    for _, preview_component in previews:
        slots = array(preview_component, "slots")
        assert len(set(slots)) == 12
        assert all(value(scene[p][1], "m_IsActive") == "0" for p in slots), "Preview stock must start hidden"
    tray_path = ASSETS / "Restaurant/Assets/Level1/GameObjects/RestaurantObjects/Customers/Food Tray.prefab"
    tray = read(tray_path)
    pose = component(tray, guid(ASSETS / "Restaurant/Items/FoodTrayCarryPose.cs"))[0][1]
    grips = [reference(pose, f) for f in ("carryOrigin", "leftGrip", "rightGrip")]
    assert len(set(grips)) == 3 and all(g in tray for g in grips)
    assert len({reference(tray[g][1], "m_Father") for g in grips}) == 1
    assert float(value(pose, "carryWorldScale")) > 0
    kitchen = scene[reference(restaurant, "kitchen")][1]
    outputs = array(kitchen, "traySpawnPoints")
    assert len(outputs) == 4
    xs = []
    for point in outputs:
        x,y,z,w = vector(scene[point][1], "m_LocalRotation")
        assert 2*(y*z-w*x) > .99, "Kitchen tray local Z must face up"
        xs.append(vector(scene[point][1], "m_LocalPosition")[0])
    assert all(b-a >= 1.875 for a,b in zip(sorted(xs), sorted(xs)[1:])), "Output trays overlap horizontally"
    print("PASS: bounded outside/paid/pickup spaces, companion positions, art-only shelf previews, tray grips and output spacing.")
    entrance = array(restaurant, "entranceRoute")
    assert len(entrance) == 2
    assert vector(scene[entrance[0]][1], "m_LocalPosition")[0] < -25
    assert vector(scene[entrance[1]][1], "m_LocalPosition")[0] > -25
    for ident, (typ, text) in scene.items():
        if typ != 4 or " stripped\n" in text: continue
        parent = reference(text, "m_Father")
        if parent and " stripped\n" not in scene[parent][1]:
            assert ident in array(scene[parent][1], "m_Children"), (ident, parent, "missing reciprocal hierarchy link")
        visited = {ident}
        while parent and parent in scene:
            assert parent not in visited, (ident, "hierarchy cycle")
            visited.add(parent)
            parent = reference(scene[parent][1], "m_Father")
    navigation = scene[reference(restaurant, "navigationSurface")][1]
    assert value(navigation, "m_Enabled") == "1"
    navigation_asset = ASSETS / "Scenes/RoleBased/Lobby2/NavMesh-Fast Food Revamp.asset"
    assert navigation_asset.is_file() and guid(navigation_asset) in value(navigation, "m_NavMeshData")

    bindings = component(scene, guid(ASSETS / "Restaurant/FastFoodLobbyAuthoring.cs"))
    assert len(bindings) == 1, "Expected one authored Lobby2 binding component"
    binding = bindings[0][1]
    assert reference(binding, "m_GameObject") == reference(restaurant, "m_GameObject")
    shelves = component(scene, guid(ASSETS / "Restaurant/RestockRoom/RestockStockRoomEntrance.cs"))
    assert len(shelves) == 2, "Two authored shelf entrances must reuse RestockScene"
    assert {i for i, _ in shelves} == {reference(binding, "dryStorageEntrance"), reference(binding, "freezerEntrance")}
    for field, storage in (("dryStorageEntrance", "0"), ("freezerEntrance", "1")):
        shelf = scene[reference(binding, field)][1]
        assert value(shelf, "storageType") == storage
        owner = reference(shelf, "m_GameObject")
        assert value(scene[owner][1], "m_Layer") == "10"
        parent = next(i for i, (t, d) in scene.items() if t == 4 and reference(d, "m_GameObject") == owner)
        assert reference(scene[reference(shelf, "standPoint")][1], "m_Father") == parent
        assert reference(shelf, "statusText") and reference(shelf, "signBackground")
        assert any(t == 65 and reference(d, "m_GameObject") == owner and value(d, "m_Enabled") == "1" for t, d in scene.values())
        assert any(reference(d, "m_GameObject") == owner for _, d in component(scene, outline_guid))
        meshes = [d for t, d in scene.values() if t == 4 and reference(d, "m_Father") == parent
                  and value(scene[reference(d, "m_GameObject")][1], "m_Name").startswith("Shelf")]
        assert len(meshes) == 2, "Each entrance must encompass both visible shelf meshes"
    # Resolve the component by its authored reference; GUID lookup remains independent of source directory layout.
    computer = scene[reference(binding, "roomComputer")][1]
    computer_go = reference(computer, "m_GameObject")
    assert value(scene[computer_go][1], "m_Name") == "Room Management Computer"
    assert reference(computer, "controller") and reference(computer, "standPoint")
    legacy = scene[8121800376007299293][1]
    assert re.search(r"propertyPath: m_IsActive\n      value: 0", legacy), "The duplicate lobby computer must stay inactive"
    assert reference(binding, "sink") and reference(binding, "cashierHome") and reference(binding, "busserHome")
    build_settings = (ROOT / "ProjectSettings/EditorBuildSettings.asset").read_text()
    assert re.search(r"- enabled: 1\n    path: .*RestockScene.unity", build_settings), "Existing RestockScene must remain build-enabled"
    assert "selfPickupChance:" not in restaurant, "Fast food has no waiter: dine-in customers collect every meal"
    worker_guid = guid(ASSETS / "Gameplay/AutonomousService/Kitchen/KitchenWorkerBot.cs")
    assert all(reference(d, "homePoint") for _, d in component(scene, worker_guid)), "Kitchen staff need authored home points"
    agent_guid = guid(ASSETS / "Customers/Customer/CustomerAgent.cs")
    customer_paths = list((ASSETS / "Art/Models/Customer/Old Alien Models").glob("*Customer.prefab"))
    customer_paths.append(ASSETS / "Restaurant/Assets/Level1/GameMechanics/Customer.prefab")
    assert len(customer_paths) == 4
    for path in customer_paths:
        docs = read(path)
        agent = component(docs, agent_guid)[0][1]
        agent_transform = next(i for i, (t, d) in docs.items() if t == 4 and reference(d, "m_GameObject") == reference(agent, "m_GameObject"))
        anchors = [reference(agent, key) for key in ("trayCarryAnchor", "trayLeftGrip", "trayRightGrip")]
        assert all(anchors) and len(set(anchors)) == 3
        for anchor in anchors:
            assert reference(docs[anchor][1], "m_Father") == agent_transform, (path, "carry anchors must follow the stable root")
            assert anchor in array(docs[agent_transform][1], "m_Children")
        x, y, z, w = vector(docs[anchors[0]][1], "m_LocalRotation")
        assert 2 * (y*z - w*x) > .99, (path, "carried tray model local Z must face up")
        assert vector(docs[anchors[1]][1], "m_LocalPosition")[0] < 0 < vector(docs[anchors[2]][1], "m_LocalPosition")[0]
    print("PASS: shared restock entrances, room computer, staff home points and four customer carry rigs.")

    hud = read(ASSETS / "Resources/UI/LobbyHUD.prefab")
    for _, (typ, canvas) in hud.items():
        if typ != 223:
            continue
        owner = reference(canvas, "m_GameObject")
        transform = next(d for t, d in hud.values() if t == 224 and reference(d, "m_GameObject") == owner)
        assert all(v > 0 for v in vector(transform, "m_LocalScale")), "Collapsed HUD canvas"
    for button in ("CameraButton", "ComputerButton", "NewspaperButton", "TaskButton"):
        nodes = [d for t, d in hud.values() if t == 1 and value(d, "m_Name") == button]
        assert len(nodes) == 1 and value(nodes[0], "m_IsActive") == "1", button
    print("PASS: prefab ownership, seats, service/cleaning links, horizontal tray mounts, authored outlines/queue anchors, 15 tables, two counters/two kiosks, entrance route, hierarchy links, navigation binding and HUD.")
    print("Unity compilation, navigation routes, animation and human gameplay still require external verification.")


if __name__ == "__main__":
    main()
