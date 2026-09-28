"""Blender 4.5: bind the four supplied T-pose aliens and export Humanoid FBXs.

Run from the project with Blender --background --factory-startup --python this_file.
Joint positions below are in metres after scaling the full character to 2 m.
The .blend sources retain the editable armature, original UVs and packed textures.
"""
import bpy
import json
import math
from pathlib import Path
from mathutils import Vector, Matrix

ROOT = Path(__file__).resolve().parents[2]
WORK = ROOT.parent / 'AlienRiggingWork'
SOURCE = ROOT / 'ArtSource/Customers/AdditionalAliens'
# Per-character placement: hips height, leg spacing, shoulder/elbow/wrist, head base.
CONFIG = {
    'Old Alien': dict(hip=.44, leg=.165, shoulder=(.28,.90), elbow=(.55,.90), wrist=(.83,.90), head=1.04),
    'Orange Alien': dict(hip=.43, leg=.205, shoulder=(.35,.97), elbow=(.67,.91), wrist=(.98,1.00), head=1.13),
    'Purple Alien': dict(hip=.43, leg=.205, shoulder=(.32,.99), elbow=(.57,.95), wrist=(.84,.98), head=1.12),
    'Yellow Alien': dict(hip=.40, leg=.18, shoulder=(.29,.86), elbow=(.57,.87), wrist=(.86,.86), head=1.06),
}

def reset():
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)

def normalize(mesh):
    points=[mesh.matrix_world @ Vector(p) for p in mesh.bound_box]
    lo=Vector(tuple(min(p[a] for p in points) for a in range(3)))
    hi=Vector(tuple(max(p[a] for p in points) for a in range(3)))
    center=Vector(((lo.x+hi.x)/2,(lo.y+hi.y)/2,lo.z))
    scale=2/(hi.z-lo.z)
    matrix=mesh.matrix_world.copy()
    for v in mesh.data.vertices: v.co=(matrix @ v.co-center)*scale
    mesh.matrix_world=Matrix.Identity(4)
    mesh.data.update()

def skeleton(name,c):
    arm=bpy.data.armatures.new(name.replace(' ','')+'Skeleton')
    rig=bpy.data.objects.new(name.replace(' ','')+'Rig',arm)
    bpy.context.collection.objects.link(rig)
    bpy.context.view_layer.objects.active=rig
    rig.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT')
    def bone(name,head,tail,parent=None):
        b=arm.edit_bones.new(name); b.head=head; b.tail=tail
        if parent: b.parent=arm.edit_bones[parent]
        b.align_roll(Vector((0,1,0)))
        return b
    hip=c['hip']; sh=c['shoulder'][1]; head=c['head']
    bone('Hips',(0,0,hip),(0,0,hip+.12))
    bone('Spine',(0,0,hip+.12),(0,0,sh-.14),'Hips')
    bone('Chest',(0,0,sh-.14),(0,0,head-.09),'Spine')
    bone('Neck',(0,0,head-.09),(0,0,head),'Chest')
    bone('Head',(0,0,head),(0,0,1.72),'Neck')
    for side,sign in [('Left',1),('Right',-1)]:
        def arm_point(key):
            x,z=c[key]; return (x*sign,0,z)
        s=arm_point('shoulder'); e=arm_point('elbow'); w=arm_point('wrist')
        bone(side+'Shoulder',(sign*.10,0,sh),s,'Chest')
        bone(side+'UpperArm',s,e,side+'Shoulder')
        bone(side+'LowerArm',e,w,side+'UpperArm')
        bone(side+'Hand',w,(w[0]+sign*.095,-.015,w[2]),side+'LowerArm')
        x=sign*c['leg']
        bone(side+'UpperLeg',(x,0,hip),(x,-.025,.24),'Hips')
        bone(side+'LowerLeg',(x,-.025,.24),(x,0,.09),side+'UpperLeg')
        bone(side+'Foot',(x,0,.09),(x,-.17,.065),side+'LowerLeg')
        bone(side+'Toes',(x,-.17,.065),(x,-.23,.065),side+'Foot')
    bpy.ops.object.mode_set(mode='OBJECT')
    rig.show_in_front=True
    return rig

def bind(mesh,rig,c):
    bpy.ops.object.select_all(action='DESELECT')
    mesh.select_set(True); rig.select_set(True)
    bpy.context.view_layer.objects.active=rig
    bpy.ops.object.parent_set(type='ARMATURE_AUTO')
    # Bone heat can fail on the disconnected/non-manifold source shells. For
    # those meshes use anatomical envelopes, restricted to the correct limb.
    fallback=any(sum(g.weight for g in v.groups)<.001 and v.co.z<c['head']+.12
                 for v in mesh.data.vertices)
    if fallback:
        for group in mesh.vertex_groups: group.remove(range(len(mesh.data.vertices)))
        for v in mesh.data.vertices:
            side='Left' if v.co.x>=0 else 'Right'
            x=abs(v.co.x); z=v.co.z
            if z<c['hip']+.04:
                names=['Hips','Spine']+[side+b for b in ('UpperLeg','LowerLeg','Foot','Toes')]
            elif x>c['shoulder'][0]-.06 and z<c['head']+.02:
                names=['Chest',side+'Shoulder',side+'UpperArm',side+'LowerArm',side+'Hand']
            else:
                names=['Hips','Spine','Chest','Neck','Head']
            scores=[]
            for name in names:
                b=rig.data.bones[name]; delta=b.tail_local-b.head_local
                t=max(0,min(1,(v.co-b.head_local).dot(delta)/delta.length_squared))
                distance=(v.co-(b.head_local+t*delta)).length
                scores.append((name,1/max(.025,distance)**4))
            scores=sorted(scores,key=lambda pair:pair[1],reverse=True)[:4]
            total=sum(weight for _,weight in scores)
            for name,weight in scores: mesh.vertex_groups[name].add([v.index],weight/total,'REPLACE')
    # Keep disconnected eyes, brows and antennae attached to the head. Smooth
    # the head/body transition so these stylized neckless meshes can still turn.
    head=mesh.vertex_groups.get('Head')
    for v in mesh.data.vertices:
        blend=max(0,min(1,(v.co.z-(c['head']-.04))/.16))
        if blend<=0: continue
        old=[(g.group,g.weight) for g in v.groups]
        for index,weight in old:
            mesh.vertex_groups[index].add([v.index],weight*(1-blend),'REPLACE')
        head_weight=next((weight for index,weight in old if index==head.index),0)
        head.add([v.index],head_weight*(1-blend)+blend,'REPLACE')
    bpy.context.view_layer.objects.active=mesh
    bpy.ops.object.vertex_group_limit_total(limit=4)
    bpy.ops.object.vertex_group_normalize_all(lock_active=False)
    bpy.ops.object.vertex_group_clean(group_select_mode='ALL',limit=.0001,keep_single=True)
    bpy.ops.object.vertex_group_normalize_all(lock_active=False)
    weights=[sum(g.weight for g in v.groups) for v in mesh.data.vertices]
    unbound=sum(w<.999 for w in weights)
    if unbound: raise RuntimeError(f'{mesh.name}: {unbound} unweighted vertices after bone heat')
    return {'vertices':len(weights),'unweighted':unbound,'envelope_binding':fallback,
            'max_influences':max(len(v.groups) for v in mesh.data.vertices),
            'max_normalization_error':max(abs(w-1) for w in weights)}

def export(name,mesh,rig):
    folder=WORK/'export'/name.replace(' ','')
    folder.mkdir(parents=True,exist_ok=True)
    for material in mesh.data.materials:
        material.name=name.replace(' ','')+'Material'
        for node in material.node_tree.nodes:
            if node.type=='TEX_IMAGE' and node.image:
                image=node.image
                image.filepath_raw=str(folder/(name.replace(' ','')+'Albedo.png'))
                image.file_format='PNG'; image.save()
                image.pack()
    bpy.ops.object.select_all(action='DESELECT')
    mesh.select_set(True);rig.select_set(True)
    bpy.context.view_layer.objects.active=rig
    bpy.ops.export_scene.fbx(filepath=str(folder/(name.replace(' ','')+'.fbx')),
        use_selection=True,object_types={'MESH','ARMATURE'},add_leaf_bones=False,
        use_armature_deform_only=True,bake_anim=False,axis_forward='-Z',axis_up='Y',
        path_mode='COPY',embed_textures=False,apply_unit_scale=True)
    SOURCE.mkdir(parents=True,exist_ok=True)
    bpy.ops.outliner.orphans_purge(do_recursive=True)
    bpy.context.preferences.filepaths.save_version=0
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/(name.replace(' ','')+'.blend')))

def pose(rig,seated=False):
    def turn(name,axis,angle):
        b=rig.pose.bones[name];b.rotation_mode='XYZ';b.rotation_euler[axis]=math.radians(angle)
    for side,sign in [('Left',1),('Right',-1)]:
        # Global-space deltas avoid relying on individual edit-bone roll.
        for suffix,axis,angle in [('UpperArm','Y',sign*65),('LowerArm','Z',sign*25)]:
            b=rig.pose.bones[side+suffix]
            rest=b.bone.matrix_local.to_3x3()
            rotation=Matrix.Rotation(math.radians(angle),3,axis)
            b.rotation_mode='QUATERNION';b.rotation_quaternion=(rest.inverted()@rotation@rest).to_quaternion()
        for suffix,angle in [('UpperLeg',-65 if seated else sign*22),('LowerLeg',75 if seated else max(0,-sign*25))]:
            b=rig.pose.bones[side+suffix];rest=b.bone.matrix_local.to_3x3()
            b.rotation_mode='QUATERNION';b.rotation_quaternion=(rest.inverted()@Matrix.Rotation(math.radians(angle),3,'X')@rest).to_quaternion()
    turn('Head',1,12)

def render_preview():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    for row,seated in enumerate((False,True)):
        for col,name in enumerate(CONFIG):
            with bpy.data.libraries.load(str(SOURCE/(name.replace(' ','')+'.blend')),link=False) as (src,dst):
                dst.objects=[n for n in src.objects]
            objects=[o for o in dst.objects if o]
            for o in objects:bpy.context.collection.objects.link(o)
            rig=next(o for o in objects if o.type=='ARMATURE')
            rig.location=(col*2.6,0,-row*2.5)
            pose(rig,seated)
    bpy.ops.object.camera_add(location=(3.9,-13,1))
    camera=bpy.context.object
    camera.rotation_euler=(Vector((3.9,0,-.2))-camera.location).to_track_quat('-Z','Y').to_euler()
    camera.data.type='ORTHO';camera.data.ortho_scale=10.3
    scene=bpy.context.scene;scene.camera=camera
    for loc in [(0,-4,6),(8,-4,5)]:
        bpy.ops.object.light_add(type='AREA',location=loc)
        light=bpy.context.object;light.data.energy=1600;light.data.size=8
        light.rotation_euler=(Vector((3.9,0,-.2))-light.location).to_track_quat('-Z','Y').to_euler()
    scene.render.engine='CYCLES';scene.cycles.samples=16
    scene.world=bpy.data.worlds.new('PreviewWorld')
    scene.world.color=(.22,.22,.22);scene.view_settings.view_transform='Standard'
    scene.render.resolution_x=1600;scene.render.resolution_y=880;scene.render.resolution_percentage=100
    scene.render.filepath=str(WORK/'preview'/'rig_poses.png')
    bpy.ops.render.render(write_still=True)

report=[]
for name,config in CONFIG.items():
    reset()
    bpy.ops.import_scene.fbx(filepath=str(WORK/'input'/(name+'.fbx')),use_image_search=False)
    meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
    if len(meshes)!=1: raise RuntimeError('Expected one supplied mesh: '+name)
    mesh=meshes[0];mesh.name=name.replace(' ','')+'Mesh'
    normalize(mesh)
    rig=skeleton(name,config)
    checks=bind(mesh,rig,config)
    export(name,mesh,rig)
    report.append(dict(name=name,bones=len(rig.data.bones),**checks))
(WORK/'rig_report.json').write_text(json.dumps(report,indent=2))
render_preview()
print('RIG_REPORT',json.dumps(report))
