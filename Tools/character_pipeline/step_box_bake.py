import bpy, bmesh, numpy as np, time
t0=time.time()
def log(*a): print(f"[{time.time()-t0:5.1f}s]",*a,flush=True)
OUT="/tmp/claude-0/-home-claude/cde10577-e29a-5e04-9baf-ffcd4ff3f773/scratchpad/v2"
bpy.ops.wm.open_mainfile(filepath=f"{OUT}/olivia_v2.blend")
for o in list(bpy.data.objects):
    if o.name!='src': bpy.data.objects.remove(o, do_unlink=True)
src=bpy.data.objects['src']
d=np.load("/tmp/claude-0/-home-claude/cde10577-e29a-5e04-9baf-ffcd4ff3f773/scratchpad/boxuv.npz")
Vy=d['V']/100.0
V=np.stack([Vy[:,0], -Vy[:,2], Vy[:,1]],axis=1)  # Y-up cm -> Z-up m
F=d['F']; UV=d['uv']
me=bpy.data.meshes.new('Olivia'); me.from_pydata(V.tolist(), [], F.tolist()); me.update()
ob=bpy.data.objects.new('Olivia', me); bpy.context.collection.objects.link(ob)
uvl=me.uv_layers.new(name='UVBaked')
flat=UV.reshape(-1,2).astype(np.float32).ravel()
assert len(uvl.data)==UV.shape[0]*3, (len(uvl.data), UV.shape)
uvl.data.foreach_set('uv', flat)
for p in me.polygons: p.use_smooth=True
bpy.context.view_layer.update()
log('mesh built', len(me.vertices), len(me.polygons), 'src dims', src.dimensions[:], 'dst dims', ob.dimensions[:])
# pack islands with margin
bpy.ops.object.select_all(action='DESELECT'); ob.select_set(True); bpy.context.view_layer.objects.active=ob
bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT'); bpy.ops.uv.select_all(action='SELECT')
bpy.ops.uv.pack_islands(udim_source='ACTIVE_UDIM', rotate=True, scale=True, margin_method='FRACTION', margin=0.008, shape_method='CONCAVE')
bpy.ops.object.mode_set(mode='OBJECT')
a=np.zeros(len(uvl.data)*2); uvl.data.foreach_get('uv',a); log('uv range', a.min(), a.max())
# bake
scene=bpy.context.scene; scene.render.engine='CYCLES'; scene.cycles.device='CPU'; scene.cycles.samples=4
b=scene.render.bake; b.use_selected_to_active=True; b.use_cage=False; b.cage_extrusion=0.0015; b.max_ray_distance=0.006; b.margin=16; b.margin_type='EXTEND'; b.use_clear=True
m=bpy.data.materials.new('M_Olivia'); m.use_nodes=True; me.materials.append(m); nt=m.node_tree
def mkimg(name,size,nc):
    im=bpy.data.images.new(name,size,size,alpha=False)
    if nc: im.colorspace_settings.name='Non-Color'
    return im
def target(img):
    for n in list(nt.nodes):
        if n.type=='TEX_IMAGE': nt.nodes.remove(n)
    n=nt.nodes.new('ShaderNodeTexImage'); n.image=img; n.select=True; nt.nodes.active=n
def sel():
    bpy.ops.object.select_all(action='DESELECT'); src.select_set(True); ob.select_set(True); bpy.context.view_layer.objects.active=ob
def stats(img):
    a=np.array(img.pixels[:]).reshape(img.size[1],img.size[0],4); return round(float(a[...,:3].mean()),3), round(float((a[...,:3].sum(-1)>0).mean()),3)
def save(img,name):
    img.filepath_raw=f"{OUT}/{name}.png"; img.file_format='PNG'; img.save()
base=mkimg('T_Olivia_BaseColor',2048,False); target(base); sel()
bpy.ops.object.bake(type='DIFFUSE', pass_filter={'COLOR'}, use_selected_to_active=True); log("base", stats(base)); save(base,'T_Olivia_BaseColor')
nrm=mkimg('T_Olivia_Normal',2048,True); target(nrm); sel(); b.normal_space='TANGENT'
bpy.ops.object.bake(type='NORMAL', use_selected_to_active=True); log("normal", stats(nrm)); save(nrm,'T_Olivia_Normal')
rgh=mkimg('T_Olivia_Roughness',1024,True); target(rgh); sel()
bpy.ops.object.bake(type='ROUGHNESS', use_selected_to_active=True); log("rough", stats(rgh)); save(rgh,'T_Olivia_Roughness')
snt=src.active_material.node_tree; met=next(n for n in snt.nodes if n.type=='TEX_IMAGE' and n.image and 'metal' in n.image.name)
out=snt.nodes['Material Output']; bsdf=snt.nodes['Principled BSDF']
em=snt.nodes.new('ShaderNodeEmission'); snt.links.new(met.outputs['Color'], em.inputs['Color'])
for l in [l for l in snt.links if l.to_node==out]: snt.links.remove(l)
snt.links.new(em.outputs['Emission'], out.inputs['Surface'])
metimg=mkimg('T_Olivia_Metallic',1024,True); target(metimg); sel()
bpy.ops.object.bake(type='EMIT', use_selected_to_active=True); log("metal", stats(metimg)); save(metimg,'T_Olivia_Metallic')
snt.links.new(bsdf.outputs['BSDF'], out.inputs['Surface'])
for n in list(nt.nodes):
    if n.type=='TEX_IMAGE': nt.nodes.remove(n)
p=nt.nodes['Principled BSDF']
nb=nt.nodes.new('ShaderNodeTexImage'); nb.image=base; nt.links.new(nb.outputs['Color'], p.inputs['Base Color'])
nn=nt.nodes.new('ShaderNodeTexImage'); nn.image=nrm; nm=nt.nodes.new('ShaderNodeNormalMap'); nt.links.new(nn.outputs['Color'], nm.inputs['Color']); nt.links.new(nm.outputs['Normal'], p.inputs['Normal'])
nr=nt.nodes.new('ShaderNodeTexImage'); nr.image=rgh; nt.links.new(nr.outputs['Color'], p.inputs['Roughness'])
bpy.ops.object.select_all(action='DESELECT'); ob.select_set(True); bpy.context.view_layer.objects.active=ob
bpy.ops.export_scene.fbx(filepath=f"{OUT}/Olivia_v2_mesh.fbx", use_selection=True, object_types={'MESH'}, mesh_smooth_type='OFF', path_mode='STRIP', embed_textures=False, apply_unit_scale=True, apply_scale_options='FBX_SCALE_NONE', add_leaf_bones=False)
bpy.ops.wm.obj_export(filepath=f"{OUT}/Olivia_v2_mesh.obj", export_selected_objects=True, export_materials=False, export_normals=True, export_uv=True, global_scale=100.0)
# renders for comparison
scene.render.resolution_x=1200; scene.render.resolution_y=1600; scene.cycles.samples=32
w=bpy.data.worlds.new('W'); scene.world=w; w.use_nodes=True; w.node_tree.nodes['Background'].inputs[1].default_value=2.0
cam_data=bpy.data.cameras.new('C'); cam=bpy.data.objects.new('C',cam_data); scene.collection.objects.link(cam); scene.camera=cam; cam_data.lens=60
from mathutils import Vector
def shot(loc,target,name,who):
    src.hide_render=(who!='src'); ob.hide_render=(who!='dst')
    cam.location=loc; cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler()
    scene.render.filepath=f"{OUT}/{name}.png"; bpy.ops.render.render(write_still=True)
shot(Vector((0.9,-1.6,0.95)), Vector((0.1,0,0.95)), 'cmp_dst_skirt','dst')
shot(Vector((0.55,-0.35,0.95)), Vector((0.5,0.0,0.92)), 'cmp_dst_hand','dst')
shot(Vector((0.55,-0.35,0.95)), Vector((0.5,0.0,0.92)), 'cmp_src_hand','src')
bpy.ops.wm.save_as_mainfile(filepath=f"{OUT}/olivia_v2_box.blend")
log('done')
