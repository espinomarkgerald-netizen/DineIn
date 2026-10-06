import bpy, json, math, sys, re
from pathlib import Path
from mathutils import Vector
ROOT=Path.cwd(); OUT=ROOT/'Assets/_Project/Player/Assets/Appearance/Costumes'; WORK=ROOT/'Temp/Costumes'
WORK.mkdir(parents=True,exist_ok=True)
TEX=ROOT/'Assets/_Project/Player/Assets/Appearance/Textures'

def material(name,color=None,path=None,alpha=False):
 m=bpy.data.materials.new(name);m.use_nodes=True
 shader=m.node_tree.nodes.get('Principled BSDF');shader.inputs['Roughness'].default_value=.8
 if color:shader.inputs['Base Color'].default_value=(*color,1)
 if path:
  tex=m.node_tree.nodes.new('ShaderNodeTexImage');tex.image=bpy.data.images.load(str(path),check_existing=True)
  m.node_tree.links.new(tex.outputs['Color'],shader.inputs['Base Color'])
  if alpha:m.node_tree.links.new(tex.outputs['Alpha'],shader.inputs['Alpha'])
 return m

def render(costume,body,hairIndex=1):
 bpy.ops.wm.open_mainfile(filepath=str(ROOT/'ArtSource/CharacterAppearance'/f'{body}.blend'))
 for o in list(bpy.data.objects):
  if o.type not in ('MESH','ARMATURE'):bpy.data.objects.remove(o,do_unlink=True)
 rig=next(o for o in bpy.data.objects if o.type=='ARMATURE');rig.animation_data_clear()
 head=rig.matrix_world@rig.data.bones['Head'].head_local
 outfit=material('Costume',path=OUT/f'{costume}.png');skin=material('Skin',(.76,.49,.29))
 face=material('Face',path=TEX/'Face_01.png',alpha=True)
 hands=material('PinkGloves',(.94,.49,.66)) if costume=='PinkBeaver' else skin
 hairMat=material('Hair',(.08,.03,.014))
 for o in list(bpy.data.objects):
  if o.type!='MESH':continue
  o.hide_set(False);o.hide_render=False;o.data.materials.clear()
  o.data.materials.append(face if o.name=='Face' else hands if o.name=='Hands' else skin if o.name in ('BodyHead','Ears') else outfit)
 # Show the supplied hairstyle without modifying or hiding its mesh.
 before=set(bpy.data.objects)
 bpy.ops.import_scene.fbx(filepath=str(ROOT/'Assets/_Project/Player/Assets/Appearance/Models'/f'{body[0]}_Hair_{hairIndex:02}.fbx'),use_image_search=False)
 for o in set(bpy.data.objects)-before:
  if o.parent is None:o.location+=head
  if o.type=='MESH':o.data.materials.clear();o.data.materials.append(hairMat)
 catalog=(ROOT/'Assets/Resources/AppearanceCatalog.asset').read_text().split('  costumes:')[1]
 entry=catalog.split('  - id: '+('pink-beaver' if costume=='PinkBeaver' else 'cowboy')+'\n')[1].split('\n  - id: ')[0]
 posMatch=re.search(r'      position: \{x: ([^,]+), y: ([^,]+), z: ([^}]+)}',entry)
 base=Vector(tuple(map(float,posMatch.groups())))
 hairId=body.lower()+'-hair-'+str(hairIndex).zfill(2)
 fit=re.search(r'      - hairId: '+hairId+r'\n        position: \{x: ([^,]+), y: ([^,]+), z: ([^}]+)}.*?        scale: \{x: ([^,]+), y: ([^,]+), z: ([^}]+)}',entry,re.S)
 scaleVec=Vector((1,1,1))
 if fit:
  values=list(map(float,fit.groups()));base+=Vector(values[:3]);scaleVec=Vector(values[3:])
 dy=base.z;lift=base.y;scale=scaleVec.x
 if costume=='PinkBeaver':
  colors={'outer':(.94,.39,.58),'inner':(1,.73,.80)}
  for name,data in json.loads((ROOT/'ArtSource/Costumes/headband-geometry.json').read_text()).items():
   mesh=bpy.data.meshes.new(name);verts=[(v[0]*scaleVec.x,-v[2]*scaleVec.z-dy,v[1]*scaleVec.y+lift) for v in data['vertices']];t=data['triangles']
   mesh.from_pydata(verts,[],[t[i:i+3] for i in range(0,len(t),3)]);mesh.update()
   ob=bpy.data.objects.new(name,mesh);bpy.context.collection.objects.link(ob);ob.location=head
   mesh.materials.append(material(name,colors[name]))
   for p in mesh.polygons:p.use_smooth=True
 else:
  before=set(bpy.data.objects)
  bpy.ops.import_scene.fbx(filepath=str(ROOT/'Assets/_Project/Player/Assets/Customization/hats/CowboyHat.fbx'),use_image_search=False)
  hat=[o for o in set(bpy.data.objects)-before if o.type=='MESH']
  points=[o.matrix_world@v.co for o in hat for v in o.data.vertices]
  lo=Vector([min(v[k] for v in points) for k in range(3)]);hi=Vector([max(v[k] for v in points) for k in range(3)])
  width=max(hi.x-lo.x,hi.y-lo.y);height=hi.z-lo.z;center=(lo+hi)/2
  brown=material('CowboyHat',(.24,.095,.028))
  for o in hat:
   world=o.matrix_world.copy();o.parent=None;o.matrix_world.identity()
   for v in o.data.vertices:
    p=world@v.co;v.co=(head.x+(p.x-center.x)/width*1.85*scale,head.y+(p.y-center.y)/width*1.85*scale-dy,head.z+.79+lift+(p.z-lo.z)/height*.36)
   o.data.materials.clear();o.data.materials.append(brown)
 # Gentle studio lights; render asset previews only, never run Unity.
 bpy.ops.object.camera_add(location=(3.1,-8,3.2));camera=bpy.context.object
 camera.rotation_euler=(Vector((0,0,1.35))-camera.location).to_track_quat('-Z','Y').to_euler();camera.data.type='ORTHO';camera.data.ortho_scale=2.95
 scene=bpy.context.scene;scene.camera=camera;scene.world=bpy.data.worlds.new('Studio');scene.world.color=(.3,.3,.3)
 for loc,power,size in [((-3,-5,6),550,5),((4,-2,4),350,4),((0,3,5),400,3)]:
  bpy.ops.object.light_add(type='AREA',location=loc);light=bpy.context.object;light.data.energy=power;light.data.size=size
  light.rotation_euler=(Vector((0,0,1))-light.location).to_track_quat('-Z','Y').to_euler()
 scene.render.engine='CYCLES';scene.cycles.samples=24;scene.cycles.use_denoising=True
 scene.view_settings.view_transform='Standard';scene.render.film_transparent=True
 scene.render.image_settings.file_format='PNG';scene.render.image_settings.color_mode='RGBA'
 scene.render.resolution_x=scene.render.resolution_y=512;scene.render.resolution_percentage=100
 scene.render.filepath=str(OUT/f'{costume}-icon.png' if body=='Male' and hairIndex==1 else WORK/f'{costume}-{body}-hair{hairIndex}.png')
 bpy.ops.render.render(write_still=True)
 print('RENDERED',scene.render.filepath)
for costume in ['PinkBeaver','Cowboy']:
 for body,hairIndex in [('Male',1),('Female',3)]:render(costume,body,hairIndex)
