import json, math
from PIL import Image
from collections import deque
ROOT='Assets/Resources'
def load(h): return Image.open(f'{ROOT}/HoleImages/lomond-country-club/Hole_{h:02d}.png').convert('RGBA')
def mask(im, pred):
    w,ht=im.size; px=im.load()
    return [[pred(*px[x,y]) for x in range(w)] for y in range(ht)]
def grass(r,g,b,a): return a>=40 and 110<=r<=180 and 170<=g<=215 and 80<=b<=140 and g>r+30 and r>b+20
def sand(r,g,b,a):  return a>=40 and r>200 and g>195 and b>170 and abs(r-g)<25 and r-b>15
def blobs(m,minpx=25):
    ht=len(m); w=len(m[0]); seen=[[False]*w for _ in range(ht)]; out=[]
    for y0 in range(ht):
        for x0 in range(w):
            if not m[y0][x0] or seen[y0][x0]: continue
            q=deque([(x0,y0)]); seen[y0][x0]=True; pts=[]
            while q:
                x,y=q.popleft(); pts.append((x,y))
                for dx,dy in ((1,0),(-1,0),(0,1),(0,-1)):
                    nx,ny=x+dx,y+dy
                    if 0<=nx<w and 0<=ny<ht and m[ny][nx] and not seen[ny][nx]:
                        seen[ny][nx]=True; q.append((nx,ny))
            if len(pts)>=minpx:
                out.append({'n':len(pts),'cx':sum(p[0] for p in pts)/len(pts),'cy':sum(p[1] for p in pts)/len(pts)})
    return out
def wpts(o,out):
    if isinstance(o,dict):
        if 'x' in o and 'z' in o and isinstance(o['x'],(int,float)): out.append((o['x'],o['z'])); return
        for v in o.values(): wpts(v,out)
    elif isinstance(o,list):
        for v in o: wpts(v,out)
def zones(h): return json.load(open(f"{ROOT}/HoleData/lomond-country-club/Hole_{h:02d}/zones.json"))
def cen(p): return (sum(a for a,_ in p)/len(p), sum(b for _,b in p)/len(p)) if p else None
def zsel(d,types,each=False):
    if not each:
        o=[]
        for z in d.get('zones',[]):
            if z.get('type') in types: wpts(z.get('polygons'),o)
        return o
    res=[]
    for z in d.get('zones',[]):
        if z.get('type') in types:
            for poly in z.get('polygons',[]):
                o=[]; wpts(poly,o)
                if o: res.append(cen(o))
    return res
def transform(h):
    """world (x,z) -> image (px,py), from the tee and green anchors."""
    im=load(h); w,ht=im.size
    bs=blobs(mask(im,grass)); bs.sort(key=lambda b:b['cy'])
    if len(bs)<2: return None
    gi=(bs[0]['cx'],bs[0]['cy']); ti=(bs[-1]['cx'],bs[-1]['cy'])
    d=zones(h); gw=cen(zsel(d,{'Green'})); tw=cen(zsel(d,{'Tee'}))
    if not(gw and tw): return None
    wv=(gw[0]-tw[0], gw[1]-tw[1]); iv=(gi[0]-ti[0], gi[1]-ti[1])
    wl=math.hypot(*wv); il=math.hypot(*iv)
    if wl<1 or il<1: return None
    s=il/wl
    ang=math.atan2(iv[1],iv[0])-math.atan2(wv[1],wv[0])
    ca,sa=math.cos(ang),math.sin(ang)
    def f(x,z):
        dx,dz=x-tw[0], z-tw[1]
        return (ti[0] + s*(dx*ca-dz*sa), ti[1] + s*(dx*sa+dz*ca))
    return {'f':f,'w':w,'h':ht,'s':s,'green_img':gi,'tee_img':ti,'im':im}
