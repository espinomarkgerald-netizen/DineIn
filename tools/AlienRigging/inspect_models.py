"""Read staged FBX copies and render a front-view contact sheet using Blender."""
import bpy
import json
from pathlib import Path
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[2]
WORK = ROOT.parent / "AlienRiggingWork"
bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete(use_global=False)
report = []
for index, name in enumerate(("Old Alien", "Orange Alien", "Purple Alien", "Yellow Alien")):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=str(WORK / "input" / (name + ".fbx")), use_image_search=False)
    objects = list(set(bpy.data.objects) - before)
    meshes = [obj for obj in objects if obj.type == "MESH"]
    points = [obj.matrix_world @ Vector(corner) for obj in meshes for corner in obj.bound_box]
    lo = Vector(tuple(min(p[axis] for p in points) for axis in range(3)))
    hi = Vector(tuple(max(p[axis] for p in points) for axis in range(3)))
    report.append({"name": name, "bounds": [list(lo), list(hi)],
                   "objects": [{"name": obj.name, "type": obj.type,
                                "vertices": len(obj.data.vertices), "faces": len(obj.data.polygons),
                                "materials": [m.name if m else None for m in obj.data.materials]}
                               for obj in meshes]})
    scale = 2 / (hi.z - lo.z)
    center = Vector(((lo.x + hi.x) / 2, (lo.y + hi.y) / 2, lo.z))
    for obj in meshes:
        matrix = obj.matrix_world.copy()
        for vertex in obj.data.vertices:
            vertex.co = (matrix @ vertex.co - center) * scale
        obj.matrix_world.identity()
        obj.location.x = index * 2.8
    bpy.ops.object.text_add(location=(index * 2.8, -.05, -.22), rotation=(1.5707963, 0, 0))
    label = bpy.context.object
    label.data.body = name
    label.data.align_x = "CENTER"
    label.data.size = .17

bpy.ops.object.camera_add(location=(4.2, -14, 1))
camera = bpy.context.object
camera.rotation_euler = (Vector((4.2, 0, 1)) - camera.location).to_track_quat('-Z', 'Y').to_euler()
camera.data.type = "ORTHO"
camera.data.ortho_scale = 11
bpy.context.scene.camera = camera
for location, energy, size in [((0,-4,6),1800,8), ((8,-3,5),1600,7)]:
    bpy.ops.object.light_add(type="AREA", location=location)
    light=bpy.context.object
    light.data.energy=energy
    light.data.shape='DISK'
    light.data.size=size
    light.rotation_euler=(Vector((4.2,0,1))-light.location).to_track_quat('-Z','Y').to_euler()
scene=bpy.context.scene
scene.render.engine='CYCLES'
scene.cycles.samples=16
scene.world.color=(.25,.25,.25)
scene.view_settings.view_transform='Standard'
scene.render.resolution_x=1600
scene.render.resolution_y=480
scene.render.resolution_percentage=100
scene.render.filepath=str(WORK/'preview'/'source_front.png')
report.append({"images": [{"name": i.name,"path":i.filepath,"packed":bool(i.packed_file)} for i in bpy.data.images]})
(WORK/'inspection.json').write_text(json.dumps(report,indent=2))
bpy.ops.render.render(write_still=True)
print('INSPECTION',json.dumps(report))
