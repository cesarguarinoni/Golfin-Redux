import trimesh, numpy as np, time, scipy.sparse as sp
from scipy.sparse.csgraph import connected_components
t0=time.time()
m = trimesh.load('olivia60k_clean.obj', force='mesh', process=False)
m.merge_vertices(merge_tex=True, merge_norm=True)
trimesh.repair.fix_winding(m); trimesh.repair.fix_normals(m)
V=m.vertices.astype(np.float64); F=m.faces.astype(np.int64)
nf=len(F)
# face adjacency graph
adj=m.face_adjacency  # (n,2)
A=sp.coo_matrix((np.ones(len(adj)),(adj[:,0],adj[:,1])),shape=(nf,nf)); A=(A+A.T).tocsr()
deg=np.asarray(A.sum(1)).ravel(); deg[deg==0]=1
N=m.face_normals.copy()
for _ in range(12):
    N=0.5*N+0.5*(A@N)/deg[:,None]
    N/=np.linalg.norm(N,axis=1,keepdims=True)+1e-12
axes=np.array([[1,0,0],[-1,0,0],[0,1,0],[0,-1,0],[0,0,1],[0,0,-1]],float)
lab=np.argmax(N@axes.T,axis=1)
def components(lab):
    same=lab[adj[:,0]]==lab[adj[:,1]]
    e=adj[same]
    G=sp.coo_matrix((np.ones(len(e)),(e[:,0],e[:,1])),shape=(nf,nf))
    n,c=connected_components(G,directed=False)
    return n,c
n,c=components(lab); print('initial charts',n, 'time',round(time.time()-t0,1))
# absorb tiny charts into best neighbouring label
for it in range(6):
    sizes=np.bincount(c,minlength=n)
    tiny=np.where(sizes<40)[0]
    if len(tiny)==0: break
    tinyset=set(tiny.tolist()); changed=0
    # for each tiny chart, count neighbor labels across adjacency edges leaving the chart
    from collections import defaultdict
    votes=defaultdict(lambda: defaultdict(int))
    for a,b in adj:
        ca,cb=c[a],c[b]
        if ca!=cb:
            if ca in tinyset: votes[ca][lab[b]]+=1
            if cb in tinyset: votes[cb][lab[a]]+=1
    for ch,vt in votes.items():
        best=max(vt.items(), key=lambda kv: kv[1])[0]
        idx=np.where(c==ch)[0]; lab[idx]=best; changed+=1
    n,c=components(lab); print(f'iter {it}: absorbed {changed}, charts now {n}')
sizes=np.bincount(c,minlength=n); print('final charts',n,'top',sorted(sizes,reverse=True)[:10],'tiny<40',int((sizes<40).sum()))
# projection UVs per corner
uv=np.zeros((nf,3,2))
for ch in range(n):
    fidx=np.where(c==ch)[0]; ax=np.bincount(lab[fidx]).argmax()
    a=axes[ax];
    # basis: two axes perpendicular
    u=np.array([0,1,0.]) if abs(a[1])<0.9 else np.array([1,0,0.])
    u=u-a*(u@a); u/=np.linalg.norm(u); w=np.cross(a,u)
    P=V[F[fidx]]  # (k,3,3)
    uv[fidx,:,0]=P@u; uv[fidx,:,1]=P@w
    # offset each chart to its own origin so islands separate in UV space (pack later)
    uv[fidx]-=uv[fidx].reshape(-1,2).min(0)
    uv[fidx]+= np.array([ch*10.0, 0.0])
np.savez('boxuv.npz', V=V, F=F, uv=uv, chart=c, lab=lab)
print('saved', round(time.time()-t0,1))
