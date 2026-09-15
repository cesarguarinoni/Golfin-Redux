# Depth-peel the box-projection charts so no chart overlaps itself in projection.
import numpy as np, scipy.sparse as sp, time
from scipy.sparse.csgraph import connected_components
import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree
t0=time.time()
d=np.load('/tmp/claude-0/-home-claude/cde10577-e29a-5e04-9baf-ffcd4ff3f773/scratchpad/boxuv.npz')
V=d['V']; F=d['F']; chart=d['chart'].copy(); lab=d['lab'].copy()
nf=len(F)
axes=np.array([[1,0,0],[-1,0,0],[0,1,0],[0,-1,0],[0,0,1],[0,0,-1]],float)
bvh=BVHTree.FromPolygons([tuple(v) for v in V.tolist()], [tuple(f) for f in F.tolist()], all_triangles=True)
cent=V[F].mean(1)
layer=np.zeros(nf,int)
eps=0.05  # cm
for i in range(nf):
    a=Vector(axes[lab[i]]); o=Vector(cent[i])+a*eps; n=0
    for _ in range(12):
        loc,nrm,idx,dist=bvh.ray_cast(o,a,10000.0)
        if idx is None: break
        if chart[idx]==chart[i]: n+=1
        o=Vector(loc)+a*eps
    layer[i]=n
print('layers', np.bincount(layer), 'time', round(time.time()-t0,1))
import trimesh as _tm
_m=_tm.Trimesh(V,F,process=False); _adj=_m.face_adjacency
# smooth layer labels by neighbour majority (within same chart) x3
for _ in range(3):
    from collections import defaultdict as _dd
    nb=_dd(list)
    for a_,b_ in _adj:
        if chart[a_]==chart[b_]: nb[a_].append(layer[b_]); nb[b_].append(layer[a_])
    new=layer.copy()
    for i,l in nb.items():
        if len(l)>=2:
            vals,cnt=np.unique(l,return_counts=True); mj=vals[cnt.argmax()]
            if cnt.max()>=2 and mj!=layer[i]: new[i]=mj
    layer=new
print('layers smoothed', np.bincount(layer))
# split charts by layer, then reconnect components
key=chart*16+np.minimum(layer,15)
import trimesh
m=trimesh.Trimesh(V,F,process=False); adj=m.face_adjacency
same=key[adj[:,0]]==key[adj[:,1]]; e=adj[same]
G=sp.coo_matrix((np.ones(len(e)),(e[:,0],e[:,1])),shape=(nf,nf))
n,c=connected_components(G,directed=False)
sizes=np.bincount(c); print('charts after peel', n, 'tiny<40', int((sizes<40).sum()))
# absorb tiny charts into the neighbouring chart with the same layer preference (just largest neighbour)
for it in range(4):
    sizes=np.bincount(c,minlength=n); tiny=set(np.where(sizes<40)[0].tolist())
    if not tiny: break
    from collections import defaultdict
    votes=defaultdict(lambda: defaultdict(int))
    for a_,b_ in adj:
        ca,cb=c[a_],c[b_]
        if ca!=cb:
            wgt = 3 if (layer[a_]==layer[b_] and lab[a_]==lab[b_]) else 1
            if ca in tiny and cb not in tiny: votes[ca][cb]+=wgt
            if cb in tiny and ca not in tiny: votes[cb][ca]+=wgt
    for ch,vt in votes.items():
        best=max(vt.items(), key=lambda kv: kv[1])[0]; c[c==ch]=best
    # relabel
    _,c=np.unique(c,return_inverse=True); n=c.max()+1
    print('absorb iter',it,'charts',n)
# projection uvs per chart (axis = majority label)
uv=np.zeros((nf,3,2))
for ch in range(n):
    fidx=np.where(c==ch)[0]; ax=np.bincount(lab[fidx]).argmax(); a=axes[ax]
    u=np.array([0,1,0.]) if abs(a[1])<0.9 else np.array([1,0,0.])
    u=u-a*(u@a); u/=np.linalg.norm(u); w=np.cross(a,u)
    P=V[F[fidx]]; uv[fidx,:,0]=P@u; uv[fidx,:,1]=P@w
    uv[fidx]-=uv[fidx].reshape(-1,2).min(0); uv[fidx]+=np.array([ch*10.0,0.0])
np.savez('/tmp/claude-0/-home-claude/cde10577-e29a-5e04-9baf-ffcd4ff3f773/scratchpad/boxuv.npz', V=V, F=F, uv=uv, chart=c, lab=lab)
print('final charts', n, 'sizes top', sorted(np.bincount(c),reverse=True)[:8], 'time', round(time.time()-t0,1))
