import bpy, bmesh, numpy as np, time
from collections import defaultdict
from mathutils.kdtree import KDTree
t0=time.time()
def log(*a): print(f"[{time.time()-t0:5.1f}s]",*a,flush=True)
OUT="/tmp/claude-0/-home-claude/cde10577-e29a-5e04-9baf-ffcd4ff3f773/scratchpad/v2"
RIG="/mnt/user-data/uploads/GolfinRedux/Assets/Art/3D/Characters/_Test/Olivia/MixamoNative/Olivia_TPose.fbx"
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=RIG, use_anim=False, ignore_leaf_bones=False, automatic_bone_orientation=False)
arm=[o for o in bpy.data.objects if o.type=='ARMATURE'][0]
mesh_ob=[o for o in bpy.data.objects if o.type=='MESH'][0]
me=mesh_ob.data
log("imported", arm.name, len(arm.data.bones), "bones; mesh", mesh_ob.name, len(me.vertices), len(me.polygons), "dims", [round(x,3) for x in mesh_ob.dimensions])
# --- 1. average skin weights across coincident vertices
V=np.zeros(len(me.vertices)*3); me.vertices.foreach_get('co',V); V=V.reshape(-1,3)
key=np.round(V,5)
groups=defaultdict(list)
for i,k in enumerate(map(tuple,key)): groups[k].append(i)
log("position groups", len(groups))
vg_names=[g.name for g in mesh_ob.vertex_groups]
# gather weights
W=[{g.group:g.weight for g in v.groups} for v in me.vertices]
changed=0
for k,idx in groups.items():
    if len(idx)<2: continue
    acc=defaultdict(float)
    for i in idx:
        for g,w in W[i].items(): acc[g]+=w
    n=len(idx); mean={g:w/n for g,w in acc.items()}
    for i in idx:
        if W[i]!=mean: changed+=1
        for g in list(W[i].keys()):
            mesh_ob.vertex_groups[g].remove([i])
        for g,w in mean.items():
            if w>1e-4: mesh_ob.vertex_groups[g].add([i], w, 'REPLACE')
log("weights averaged; vertices touched", changed)
# --- 2. weld positions (keeps UV seams per loop), smooth shading
bm=bmesh.new(); bm.from_mesh(me)
bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
bm.to_mesh(me); bm.free(); me.update()
for p in me.polygons: p.use_smooth=True
log("welded", len(me.vertices), len(me.polygons))
# --- 3. transfer the PACKED baked UVs from olivia_v2_box.blend (object 'Olivia') by face centroid match
with bpy.data.libraries.load(f"{OUT}/olivia_v2_box.blend", link=False) as (src_data, dst_data):
    dst_data.meshes=['Olivia.001']
log("meshes now", [m.name for m in bpy.data.meshes])
bme=[m for m in bpy.data.meshes if m is not me and m.uv_layers.get('UVBaked')][0]
BV=np.zeros(len(bme.vertices)*3); bme.vertices.foreach_get('co',BV); BV=BV.reshape(-1,3)
BV=np.stack([BV[:,0], BV[:,2], -BV[:,1]],axis=1)*100.0   # Z-up m -> Y-up cm (rig mesh local space)
BF=np.array([[me_l for me_l in p.vertices] for p in bme.polygons])
buv=bme.uv_layers['UVBaked']
BUVflat=np.zeros(len(buv.data)*2); buv.data.foreach_get('uv',BUVflat); BUVflat=BUVflat.reshape(-1,2)
loop_start=np.array([p.loop_start for p in bme.polygons])
mv=np.zeros(len(me.vertices)*3); me.vertices.foreach_get('co',mv); mv=mv.reshape(-1,3)
log("mesh bounds", mv.min(0).round(3), mv.max(0).round(3), "baked bounds", BV.min(0).round(3), BV.max(0).round(3))
cent=BV[BF].mean(1)
kd=KDTree(len(cent))
for i,c in enumerate(cent): kd.insert(c.tolist(), i)
kd.balance()
uvl=me.uv_layers.get('UVBaked') or me.uv_layers.new(name='UVBaked')
me.uv_layers.active=uvl; uvl.active_render=True
miss=0; bad=0
for p in me.polygons:
    co,idx,dist=kd.find(list(p.center))
    if dist>1e-3: miss+=1
    corners=BV[BF[idx]]
    for li in p.loop_indices:
        vpos=mv[me.loops[li].vertex_index]
        j=int(np.argmin(((corners-vpos)**2).sum(1)))
        if ((corners[j]-vpos)**2).sum()>1e-6: bad+=1
        uvl.data[li].uv=BUVflat[loop_start[idx]+j].tolist()
log("uv transferred; centroid misses", miss, "corner mismatches", bad)
a=np.zeros(len(uvl.data)*2); uvl.data.foreach_get('uv',a); log("uv range", a.min(), a.max())
# drop other uv layers
for u in list(me.uv_layers):
    if u.name!='UVBaked': me.uv_layers.remove(u)
bpy.ops.wm.save_as_mainfile(filepath=f"{OUT}/olivia_v2_rig_uv.blend")
log("saved")
