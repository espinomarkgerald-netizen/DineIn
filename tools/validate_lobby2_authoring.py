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
    assets = {}
    for path in sorted((ASSETS / "Restaurant/Prefabs/FastFood").glob("*.prefab")):
        docs = read(path)
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
    dining = array(restaurant, "diningTables")
    assert len(dining) == len(set(dining)) == 15, "Expected the 14 original tables plus the second round table"
    counts = {}
    for ident in dining:
        instance = reference(scene[ident][1], "m_PrefabInstance")
        path, prefab = instances[instance]
        assert reference(scene[ident][1], "m_CorrespondingSourceObject") == component(prefab, booth_guid)[0][0]
        counts[path.stem] = counts.get(path.stem, 0) + 1
    assert counts == {"Fast Food Booth": 8, "Fast Food Long Table": 5, "Fast Food Round Table": 2}, counts
    assert len(instances) == 17, "15 tables, cashier, and sink must remain prefab instances"
    navigation = scene[reference(restaurant, "navigationSurface")][1]
    assert value(navigation, "m_Enabled") == "1"
    navigation_asset = ASSETS / "Scenes/RoleBased/Lobby2/NavMesh-Fast Food Revamp.asset"
    assert navigation_asset.is_file() and guid(navigation_asset) in value(navigation, "m_NavMeshData")

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
    print("PASS: prefab ownership, seats, service/cleaning links, 15 scene tables, station instances, navigation binding, HUD scales and buttons.")
    print("Unity compilation, navigation routes, animation and human gameplay still require external verification.")


if __name__ == "__main__":
    main()
