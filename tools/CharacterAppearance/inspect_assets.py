"""Read supplied meshes in background Blender; no changes to originals."""
import bpy
import json
import zipfile
from pathlib import Path
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[2]
WORK = ROOT / 'Temp/CharacterAppearance'
WORK.mkdir(parents=True, exist_ok=True)
with zipfile.ZipFile('G:/Downloads/Player Customization&Employee.zip') as archive:
    archive.extractall(WORK / 'source')
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
for source in [WORK/'source/Player/Male_Base.fbx', WORK/'source/Player/Female_Base.fbx',
               ROOT/'Assets/_Project/MainMenu/NewDesign/Characters/Chef/Chef.fbx']:
    before=set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=str(source), use_image_search=False)
    objects=list(set(bpy.data.objects)-before)
    report=[]
    for obj in objects:
        if obj.type=='MESH':
            pts=[obj.matrix_world@v.co for v in obj.data.vertices]
            report.append(dict(name=obj.name,verts=len(pts),
                lo=[min(v[i] for v in pts) for i in range(3)],hi=[max(v[i] for v in pts) for i in range(3)],
                uv=[uv.name for uv in obj.data.uv_layers], mats=[m.name if m else None for m in obj.data.materials]))
        if obj.type=='ARMATURE':
            report.append(dict(rig=obj.name,bones=[dict(name=b.name,head=list(obj.matrix_world@b.head_local),tail=list(obj.matrix_world@b.tail_local)) for b in obj.data.bones]))
    print('ASSET',source.name,json.dumps(report))
    for obj in objects:bpy.data.objects.remove(obj,do_unlink=True)
