"""Render the actual meshes baked by Unity's existing customer animation clips."""
import bpy, json
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2]
WORK=ROOT.parent/'AlienRiggingWork'
bpy.ops.wm.read_factory_settings(use_empty=True)
names=['OldAlien','OrangeAlien','PurpleAlien','YellowAlien']
clips=['Idle','Walking','sitting']
for data in json.loads((WORK/'poses.json').read_text()):
    mesh=bpy.data.meshes.new(data['name']+data['clip'])
    triangles=data['triangles']
    mesh.from_pydata(data['vertices'],[],[triangles[i:i+3] for i in range(0,len(triangles),3)])
    mesh.update()
    layer=mesh.uv_layers.new()
    for poly in mesh.polygons:
        poly.use_smooth=True
        for index in poly.loop_indices: layer.data[index].uv=data['uv'][mesh.loops[index].vertex_index]
    obj=bpy.data.objects.new(mesh.name,mesh);bpy.context.collection.objects.link(obj)
    obj.location=(names.index(data['name'])*5,0,-clips.index(data['clip'])*5)
    mat=bpy.data.materials.new(data['name']);mat.use_nodes=True
    image=mat.node_tree.nodes.new('ShaderNodeTexImage')
    image.image=bpy.data.images.load(str(WORK/'export'/data['name']/(data['name']+'Albedo.png')),check_existing=True)
    shader=mat.node_tree.nodes.get('Principled BSDF')
    mat.node_tree.links.new(image.outputs['Color'],shader.inputs['Base Color'])
    shader.inputs['Roughness'].default_value=.65
    mesh.materials.append(mat)
bpy.ops.object.camera_add(location=(7.5,-25,-2.5))
camera=bpy.context.object; camera.rotation_euler=(Vector((7.5,0,-3))-camera.location).to_track_quat('-Z','Y').to_euler()
camera.data.type='ORTHO';camera.data.ortho_scale=22
scene=bpy.context.scene;scene.camera=camera
scene.world=bpy.data.worlds.new('World');scene.world.color=(.25,.25,.25)
for loc in [(0,-6,8),(15,-7,3),(7,-5,-8)]:
    bpy.ops.object.light_add(type='AREA',location=loc)
    light=bpy.context.object;light.data.energy=2000;light.data.size=8
    light.rotation_euler=(Vector((7.5,0,-3))-light.location).to_track_quat('-Z','Y').to_euler()
scene.render.engine='CYCLES';scene.cycles.samples=16
scene.view_settings.view_transform='Standard'
scene.render.resolution_x=1400;scene.render.resolution_y=1000;scene.render.resolution_percentage=100
scene.render.filepath=str(WORK/'preview'/'retargeted_poses.png')
bpy.ops.render.render(write_still=True)
