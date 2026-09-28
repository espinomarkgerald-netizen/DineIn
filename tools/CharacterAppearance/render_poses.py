"""Offline contact sheet of meshes actually retargeted and baked by Unity."""
import bpy, json
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2]
WORK=ROOT/'Temp/CharacterAppearance'
TEX=ROOT/'Assets/_Project/Player/Assets/Appearance/Textures'
bpy.ops.wm.read_factory_settings(use_empty=True)
clips=['Idle','Walking','Running','WalkingCarry','CarryIdle']
materials={}
def mat(body,part):
    key=(body,part if part in ('BodyHead','Hands','Ears','Face','Hair') else 'Outfit')
    if key in materials:return materials[key]
    m=bpy.data.materials.new(str(key));m.use_nodes=True
    nodes=m.node_tree.nodes;shader=nodes.get('Principled BSDF');shader.inputs['Roughness'].default_value=.75
    shader.inputs['Base Color'].default_value=(.66,.36,.18,1)
    if part=='Hair':shader.inputs['Base Color'].default_value=(.045,.016,.008,1)
    if key[1] in ('Outfit','Face'):
        path=TEX/'Face_01.png' if part=='Face' else next((TEX/body/'Casual Dining').glob('*Chef*.png'))
        image=nodes.new('ShaderNodeTexImage');image.image=bpy.data.images.load(str(path),check_existing=True)
        m.node_tree.links.new(image.outputs['Color'],shader.inputs['Base Color'])
        if part=='Face':m.node_tree.links.new(image.outputs['Alpha'],shader.inputs['Alpha'])
    materials[key]=m;return m
for data in json.loads((WORK/'poses.json').read_text()):
    mesh=bpy.data.meshes.new(data['part']);t=data['triangles']
    mesh.from_pydata(data['vertices'],[],[t[i:i+3] for i in range(0,len(t),3)]);mesh.update()
    layer=mesh.uv_layers.new()
    for poly in mesh.polygons:
        poly.use_smooth=True
        if data['uv']:
            for i in poly.loop_indices:layer.data[i].uv=data['uv'][mesh.loops[i].vertex_index]
    obj=bpy.data.objects.new(data['part'],mesh);bpy.context.collection.objects.link(obj)
    obj.location=(clips.index(data['clip'])*2.5,0,0 if data['body']=='Male' else -2.8)
    mesh.materials.append(mat(data['body'],data['part']))
for row,body in enumerate(['Male','Female']):
    for col,clip in enumerate(clips):
        bpy.ops.object.text_add(location=(col*2.5-.9,-.8,-row*2.8-.18),rotation=(1.5708,0,0))
        text=bpy.context.object;text.data.body=body+' / '+clip;text.data.size=.14
bpy.ops.object.camera_add(location=(5,-22,2.0))
camera=bpy.context.object;camera.rotation_euler=(Vector((5,0,-.3))-camera.location).to_track_quat('-Z','Y').to_euler();camera.data.type='ORTHO';camera.data.ortho_scale=13
scene=bpy.context.scene;scene.camera=camera
scene.world=bpy.data.worlds.new('World');scene.world.color=(.22,.22,.22)
for loc in [(0,-5,6),(10,-6,4),(5,-4,-3)]:
    bpy.ops.object.light_add(type='AREA',location=loc);light=bpy.context.object;light.data.energy=900;light.data.size=7
    light.rotation_euler=(Vector((5,0,0))-light.location).to_track_quat('-Z','Y').to_euler()
scene.render.engine='CYCLES';scene.cycles.samples=16;scene.view_settings.view_transform='Standard'
scene.render.resolution_x=1800;scene.render.resolution_y=900;scene.render.resolution_percentage=100
scene.render.filepath=str(WORK/'poses.png');bpy.ops.render.render(write_still=True)
