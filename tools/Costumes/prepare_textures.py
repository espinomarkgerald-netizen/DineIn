from pathlib import Path
import numpy as np,json
from PIL import Image,ImageFilter,ImageDraw
ROOT=Path.cwd();out=ROOT/'Assets/_Project/Player/Assets/Appearance/Costumes';work=ROOT/'Temp/Costumes'
work.mkdir(parents=True,exist_ok=True)
data=json.load(open(work/'body-uvs.json'))['Male']

def fill(rgb,mask,iterations=100):
 a=rgb.astype(np.float32).copy();known=~mask
 # Grow neighboring artwork into guide marks, then smooth only the repaired pixels.
 for _ in range(25):
  pad=np.pad(a,((1,1),(1,1),(0,0)),mode='edge');k=np.pad(known,1,mode='edge')
  total=np.zeros_like(a);count=np.zeros(mask.shape,np.float32)
  for dy,dx in [(0,1),(2,1),(1,0),(1,2)]:
   valid=k[dy:dy+mask.shape[0],dx:dx+mask.shape[1]];total+=pad[dy:dy+mask.shape[0],dx:dx+mask.shape[1]]*valid[:,:,None];count+=valid
  take=~known & (count>0)
  if not take.any():break
  a[take]=total[take]/count[take,None];known[take]=True
 for _ in range(iterations):
  pad=np.pad(a,((1,1),(1,1),(0,0)),mode='edge');avg=(pad[:-2,1:-1]+pad[2:,1:-1]+pad[1:-1,:-2]+pad[1:-1,2:])*.25
  a[mask]=avg[mask]
 return np.clip(a,0,255).astype('uint8')

def clean(name):
 im=Image.open(ROOT/'ArtSource/Costumes'/f'{name}-reference.png').convert('RGBA');a=np.array(im);rgb=a[:,:,:3];alpha=a[:,:,3];n=im.width
 # Detect narrow dark UV strokes against surrounding colored artwork; leave broad shadows intact.
 lum=np.array(Image.fromarray(rgb).convert('L'));closed=np.array(Image.fromarray(lum).filter(ImageFilter.MaxFilter(9)).filter(ImageFilter.MinFilter(9)))
 interior=np.array(Image.fromarray(alpha).filter(ImageFilter.MinFilter(7)))>220
 mask=(closed.astype(int)-lum.astype(int)>15)&(lum<closed*.86)&interior
 mask=np.array(Image.fromarray(mask.astype('uint8')*255).filter(ImageFilter.MaxFilter(3)))>0
 mask&=interior
 repaired=fill(rgb,mask)
 # The atlas is opaque on clothing. Extend neighboring colors into the external UV edges for mip padding.
 coverage=Image.new('L',im.size);draw=ImageDraw.Draw(coverage)
 for part in ['UpperBody','LowerBody','Shoes']:
  for uvs in data[part]['uvs']:draw.polygon([(u*(n-1),(1-v)*(n-1)) for u,v in uvs],fill=255)
 core=np.array(coverage)>0
 red=(rgb[:,:,0]>rgb[:,:,1].astype(float)*1.7)&(rgb[:,:,0]>rgb[:,:,2].astype(float)*1.5)&(rgb[:,:,1]<110)
 # Remove red editing marks outside the actual clothing islands, not the cowboy neckerchief.
 outside=np.array(coverage.filter(ImageFilter.MaxFilter(9)))==0
 alpha[outside & red]=0
 edge=core & ((alpha<230)|((lum<60)&~interior))
 repaired=fill(repaired,edge,60)
 result=np.dstack((repaired,alpha));Image.fromarray(result).save(out/f'{name}.png')
 Image.fromarray(mask.astype('uint8')*255).save(work/f'{name}-repair-mask.png')
 print(name,'guide pixels repaired',int(mask.sum()),'edge pixels',int(edge.sum()))
 return result

pink=clean('PinkBeaver');cow=clean('Cowboy')
# Reproject the supplied vest/scarf/belt artwork onto the actual torso UVs.
# The two trapezoids painted as vests in the reference are leg islands in the model.
source=cow.copy();n=cow.shape[0];target=cow.copy();painted=np.zeros((n,n),bool)
def sample(u,v):
 x=np.clip(u*(n-1),0,n-1);y=np.clip(v*(n-1),0,n-1);x0=x.astype(int);y0=y.astype(int);x1=np.minimum(x0+1,n-1);y1=np.minimum(y0+1,n-1);fx=(x-x0)[:,None];fy=(y-y0)[:,None]
 return source[y0,x0,:3]*(1-fx)*(1-fy)+source[y0,x1,:3]*fx*(1-fy)+source[y1,x0,:3]*(1-fx)*fy+source[y1,x1,:3]*fx*fy
for part in ['UpperBody','LowerBody']:
 mesh=data[part];verts=np.array(mesh['vertices'])
 for face,uvs in zip(mesh['faces'],mesh['uvs']):
  for j in range(1,len(face)-1):
   inds=[0,j,j+1];pts=np.array([uvs[i] for i in inds]);pts[:,1]=1-pts[:,1];pts*=n-1
   xyz=verts[[face[i] for i in inds]];lo=np.maximum(np.floor(pts.min(0)).astype(int)-1,0);hi=np.minimum(np.ceil(pts.max(0)).astype(int)+1,n-1)
   yy,xx=np.mgrid[lo[1]:hi[1]+1,lo[0]:hi[0]+1];p=np.stack([xx.ravel(),yy.ravel()],axis=1)
   mat=np.column_stack((pts[1]-pts[0],pts[2]-pts[0]));det=np.linalg.det(mat)
   if abs(det)<1e-6:continue
   ab=(p-pts[0])@np.linalg.inv(mat).T;weights=np.column_stack((1-ab[:,0]-ab[:,1],ab));inside=(weights>=-.012).all(1);p=p[inside].astype(int);coords=weights[inside]@xyz
   if not len(p):continue
   x,y,z=coords.T
   if part=='UpperBody':
    # Preserve the authored sleeve/cuff islands; remap only the torso shell.
    pick=np.abs(x)<.325
    # Back uses the supplied back-vest artwork; front includes the scarf and waist belt.
    u=np.where(y<0,.268+x/.325*.165,.543-x/.325*.15)
    v=np.where(y<0,.657+(1.235-z)/.647*.25,.298+(1.235-z)/.647*.145)
   else:
    pick=np.ones(len(p),bool);u=.272+x/.32*.11;v=.916+(.612-z)/.412*.06
   p=p[pick];rgb=sample(u[pick],v[pick]);target[p[:,1],p[:,0],:3]=np.clip(rgb,0,255);target[p[:,1],p[:,0],3]=255;painted[p[:,1],p[:,0]]=True
# Pad repaired UV boundaries to avoid dark texture seams when mipmapped.
for _ in range(5):
 arr=target[:,:,:3].astype(float);pad=np.pad(arr,((1,1),(1,1),(0,0)),mode='edge');known=np.pad(painted,1)
 total=np.zeros_like(arr);count=np.zeros(painted.shape)
 for dy,dx in [(0,1),(2,1),(1,0),(1,2)]:
  k=known[dy:dy+n,dx:dx+n];total+=pad[dy:dy+n,dx:dx+n]*k[:,:,None];count+=k
 take=~painted&(count>0);target[take,:3]=(total[take]/count[take,None]).astype('uint8');target[take,3]=255;painted[take]=True
Image.fromarray(target).save(out/'Cowboy.png')
print('Cowboy vest and trousers aligned to existing body UVs; both body UV sets are identical.')
