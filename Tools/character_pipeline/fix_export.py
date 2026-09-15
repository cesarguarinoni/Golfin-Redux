import bpy, bmesh, numpy as np, time
t0=time.time()
def log(*a): print(f"[{time.time()-t0:5.1f}s]",*a,flush=True)
OUT="/tmp/claude-0/-home-claude/cde10577-e29a-5e04-9baf-ffcd4ff3f773/scratchpad/v2"
bpy.ops.wm.open_mainfile(filepath=f"{OUT}/olivia_v2_rig_uv.blend")
arm=[o for o in bpy.data.objects if o.type=='ARMATURE'][0]
ob=[o for o in bpy.data.objects if o.type=='MESH'][0]; me=ob.data
# winding check: count faces whose normal disagrees with neighbours before/after recalc
bm=bmesh.new(); bm.from_mesh(me); bm.faces.ensure_lookup_table()
before=np.array([f.normal.copy() for f in bm.faces])
bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
after=np.array([f.normal.copy() for f in bm.faces])
flipped=int(((before*after).sum(1)<0).sum())
log("faces flipped by recalc:", flipped, "of", len(bm.faces))
bm.to_mesh(me); bm.free(); me.update()
for p in me.polygons: p.use_smooth=True
# finger weight sanity: how many vertices near finger bones have their max weight on the Hand bone
names=[g.name for g in ob.vertex_groups]
def maxgroup(v):
    if not v.groups: return None
    g=max(v.groups, key=lambda g:g.weight); return names[g.group]
cnt={}
for v in me.vertices:
    n=maxgroup(v); cnt[n]=cnt.get(n,0)+1
fing={k:v for k,v in cnt.items() if k and ('Hand' in k)}
log("hand/finger max-weight vertex counts:", dict(sorted(fing.items())))
# material with baked textures
mat=bpy.data.materials.new('M_Olivia'); mat.use_nodes=True; nt=mat.node_tree; p=nt.nodes['Principled BSDF']
def tex(name, nc=False):
    im=bpy.data.images.load(f"{OUT}/{name}.png"); 
    if nc: im.colorspace_settings.name='Non-Color'
    n=nt.nodes.new('ShaderNodeTexImage'); n.image=im; return n
nb=tex('T_Olivia_BaseColor'); nt.links.new(nb.outputs['Color'], p.inputs['Base Color'])
nn=tex('T_Olivia_Normal',True); nm=nt.nodes.new('ShaderNodeNormalMap'); nt.links.new(nn.outputs['Color'], nm.inputs['Color']); nt.links.new(nm.outputs['Normal'], p.inputs['Normal'])
nr=tex('T_Olivia_Roughness',True); nt.links.new(nr.outputs['Color'], p.inputs['Roughness'])
me.materials.clear(); me.materials.append(mat)
ob.name='Olivia'; me.name='Olivia'
# export rig + mesh
bpy.ops.object.select_all(action='DESELECT'); arm.select_set(True); ob.select_set(True); bpy.context.view_layer.objects.active=arm
bpy.ops.export_scene.fbx(filepath=f"{OUT}/Olivia_TPose_v2.fbx", use_selection=True, object_types={'ARMATURE','MESH'},
    mesh_smooth_type='OFF', use_mesh_modifiers=False, add_leaf_bones=False, bake_anim=False, path_mode='STRIP', embed_textures=False,
    apply_unit_scale=True, apply_scale_options='FBX_SCALE_NONE', use_armature_deform_only=False, armature_nodetype='NULL',
    primary_bone_axis='Y', secondary_bone_axis='X')
log("exported")
bpy.ops.wm.save_as_mainfile(filepath=f"{OUT}/olivia_v2_final.blend")
