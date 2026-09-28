"""Offline-only derived rigs. Originals and gameplay prefabs are never modified."""
import bpy
import math
import json
from pathlib import Path
from mathutils import Vector, Matrix

ROOT=Path(__file__).resolve().parents[2]
WORK=ROOT/'Temp/CharacterAppearance'
OUT=WORK/'export'
OUT.mkdir(parents=True,exist_ok=True)
SCALE=2/2.21696
def convert(p,offset):
    # Source characters face +X, with lateral Y and vertical Z.
    return Vector(((p.y-offset.y)*SCALE,-(p.x-offset.x)*SCALE,(p.z-offset.z)*SCALE))

def material(name,color):
    m=bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.diffuse_color=(*color,1)
    return m

def skeleton():
    data=bpy.data.armatures.new('CharacterSkeleton');rig=bpy.data.objects.new('CharacterRig',data)
    bpy.context.collection.objects.link(rig);bpy.context.view_layer.objects.active=rig;rig.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT')
    def bone(n,a,b,parent=None):
        v=data.edit_bones.new(n);v.head=Vector(a)*SCALE;v.tail=Vector(b)*SCALE
        if parent:v.parent=data.edit_bones[parent]
        v.align_roll(Vector((0,1,0)))
    bone('Hips',(0,0,.65),(0,0,.81))
    bone('Spine',(0,0,.81),(0,0,1.02),'Hips')
    bone('Chest',(0,0,1.02),(0,0,1.28),'Spine')
    bone('Neck',(0,0,1.28),(0,0,1.38),'Chest')
    bone('Head',(0,0,1.38),(0,0,1.95),'Neck')
    for side,s in [('Left',1),('Right',-1)]:
        bone(side+'Shoulder',(s*.10,0,1.24),(s*.30,0,1.22),'Chest')
        bone(side+'UpperArm',(s*.30,0,1.22),(s*.60,0,1.17),side+'Shoulder')
        bone(side+'LowerArm',(s*.60,0,1.17),(s*.87,0,1.15),side+'UpperArm')
        bone(side+'Hand',(s*.87,0,1.15),(s*1.055,0,1.15),side+'LowerArm')
        bone(side+'UpperLeg',(s*.18,0,.65),(s*.20,-.015,.39),'Hips')
        bone(side+'LowerLeg',(s*.20,-.015,.39),(s*.20,0,.14),side+'UpperLeg')
        bone(side+'Foot',(s*.20,0,.14),(s*.20,-.22,.08),side+'LowerLeg')
        bone(side+'Toes',(s*.20,-.22,.08),(s*.20,-.33,.08),side+'Foot')
    bpy.ops.object.mode_set(mode='OBJECT')
    return rig

def weights(obj,rig,part):
    for b in rig.data.bones:obj.vertex_groups.new(name=b.name)
    for v in obj.data.vertices:
        x,z=abs(v.co.x)/SCALE,v.co.z/SCALE
        side='Left' if v.co.x>=0 else 'Right'
        if part in ('Head','Ears','Face'):
            scores=[('Head',1)]
        elif part=='Shoes':scores=[(side+'Foot',1)]
        else:
            if part=='Hands':names=[side+'LowerArm',side+'Hand']
            elif part=='LowerBody':names=['Hips',side+'UpperLeg',side+'LowerLeg']
            elif x>.30 and z>.94:names=['Chest',side+'Shoulder',side+'UpperArm',side+'LowerArm']
            elif z<.68:names=['Hips',side+'UpperLeg']
            else:names=['Hips','Spine','Chest','Neck']
            scores=[]
            for name in names:
                b=rig.data.bones[name];delta=b.tail_local-b.head_local
                t=max(0,min(1,(v.co-b.head_local).dot(delta)/delta.length_squared))
                distance=(v.co-(b.head_local+t*delta)).length
                scores.append((name,1/max(.035,distance)**5))
            scores=sorted(scores,key=lambda p:p[1],reverse=True)[:4]
        total=sum(w for _,w in scores)
        for n,w in scores:obj.vertex_groups[n].add([v.index],w/total,'REPLACE')
    obj.parent=rig;mod=obj.modifiers.new('CharacterSkin','ARMATURE');mod.object=rig
    assert all(abs(sum(g.weight for g in v.groups)-1)<.0001 for v in obj.data.vertices)

def export(path,objects):
    bpy.ops.object.select_all(action='DESELECT')
    for o in objects:o.select_set(True)
    bpy.context.view_layer.objects.active=objects[0]
    bpy.ops.export_scene.fbx(filepath=str(path),use_selection=True,object_types={'ARMATURE','MESH'},
        add_leaf_bones=False,bake_anim=False,axis_forward='-Z',axis_up='Y',apply_unit_scale=True,
        apply_scale_options='FBX_SCALE_ALL',use_mesh_modifiers=True)

report=[]
for body in ('Male','Female'):
    bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
    bpy.ops.import_scene.fbx(filepath=str(WORK/'source/Player'/f'{body}_Base.fbx'),use_image_search=False)
    objects=[o for o in bpy.context.scene.objects if o.type=='MESH']
    offset=Vector((.01597,.003005+(.002071 if body=='Female' else 0),.000735+(.002294 if body=='Female' else 0)))
    for obj in list(objects):
        name=obj.name.split('.')[0];obj.name='Hands' if name=='Cube' else name
        world=obj.matrix_world.copy()
        for v in obj.data.vertices:v.co=convert(world@v.co,offset)
        obj.matrix_world=Matrix.Identity(4);obj.data.update()
        obj.data.materials.clear()
        obj.data.materials.append(material('Skin' if obj.name in ('Hands','Head','Ears') else 'Outfit',(.8,.48,.28) if obj.name in ('Hands','Head','Ears') else (.4,.4,.4)))
        if obj.name=='Head':
            face=obj.copy();face.data=obj.data.copy();bpy.context.collection.objects.link(face);face.name='Face'
            for v in face.data.vertices:v.co+=v.normal*.0008
            # The supplied face PNGs are front projections, not the head's multi-island UV atlas.
            # Give only the derived transparent face shell a matching projection; preserve body UVs.
            for layer in list(face.data.uv_layers):face.data.uv_layers.remove(layer)
            face_uv=face.data.uv_layers.new(name='FaceProjection')
            for poly in face.data.polygons:
                front=sum(face.data.vertices[i].co.y for i in poly.vertices)/len(poly.vertices)<-.20
                for loop in poly.loop_indices:
                    point=face.data.vertices[face.data.loops[loop].vertex_index].co
                    face_uv.data[loop].uv=(point.x/.82+.5,(point.z-1.20)/.82) if front else (0,0)
            face.data.materials.clear();face.data.materials.append(material('Face',(1,1,1)))
            objects.append(face)
    rig=skeleton()
    for obj in objects:weights(obj,rig,obj.name)
    # Humanoid bone names must be unique across the entire FBX hierarchy.
    for obj in objects:
        if obj.name=='Head':obj.name='BodyHead'
    export(OUT/f'{body}.fbx',[rig]+objects)
    source=ROOT/'ArtSource/CharacterAppearance';source.mkdir(parents=True,exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(source/f'{body}.blend'))
    report.append(dict(body=body,bones=len(rig.data.bones),vertices=sum(len(o.data.vertices) for o in objects)))
    # Rigid hairs retain the same authored position relative to the shared head joint.
    for source in sorted((WORK/'source/Player/Hair'/body).glob('*.fbx')):
        before=set(bpy.data.objects);bpy.ops.import_scene.fbx(filepath=str(source),use_image_search=False)
        hairs=[o for o in set(bpy.data.objects)-before if o.type=='MESH']
        for obj in hairs:
            matrix=obj.matrix_world.copy()
            for v in obj.data.vertices:v.co=convert(matrix@v.co,offset)-Vector((0,0,1.38*SCALE))
            obj.matrix_world=Matrix.Identity(4)
        export(OUT/source.name,hairs)
        for obj in hairs:bpy.data.objects.remove(obj,do_unlink=True)
print('CHARACTER_RIG_REPORT',json.dumps(report))
