import bpy,json
from pathlib import Path
root=Path.cwd();(root/'Temp/Costumes').mkdir(parents=True,exist_ok=True);data={}
for body in ['Male','Female']:
 bpy.ops.wm.open_mainfile(filepath=str(root/'ArtSource/CharacterAppearance'/f'{body}.blend'))
 data[body]={}
 for o in bpy.data.objects:
  if o.type!='MESH':continue
  me=o.data;uv=me.uv_layers.active
  if uv is None:continue
  print(body,o.name,len(me.vertices),len(me.polygons),*[tuple(round(f(v.uv[k] for v in uv.data),4) for k in range(2)) for f in (min,max)])
  data[body][o.name]={'vertices':[list(o.matrix_world@v.co) for v in me.vertices],'faces':[list(p.vertices) for p in me.polygons],'uvs':[[list(uv.data[i].uv) for i in p.loop_indices] for p in me.polygons]}
(root/'Temp/Costumes/body-uvs.json').write_text(json.dumps(data))

