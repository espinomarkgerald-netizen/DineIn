from pathlib import Path
import numpy as np,json
from PIL import Image
R=Path.cwd();O=R/'Assets/_Project/Player/Assets/Appearance/Costumes';uvs=json.loads((R/'Temp/Costumes/body-uvs.json').read_text())
for name,body in [('RedSpiderHoodie','Male'),('RedSpiderHoodieFemale','Female'),('PinkSpiderHoodie','Female'),('PinkSpiderHoodieMale','Male')]:
 src=Image.open(R/'ArtSource/Costumes'/('RedSpiderHoodie-reference.png' if name.startswith('Red') else 'PinkSpiderHoodie-reference.png')).convert('RGBA');a=np.array(src);n=src.width;result=a.copy();paint=np.zeros(a.shape[:2],bool)
 for part in ['UpperBody','LowerBody','Shoes']:
  m=uvs[body][part];verts=np.array(m['vertices'])
  for face,uv in zip(m['faces'],m['uvs']):
   for j in range(1,len(face)-1):
    ii=[0,j,j+1];p=np.array([uv[k] for k in ii]);p[:,1]=1-p[:,1];p*=n-1;xyz=verts[[face[k] for k in ii]]
    lo=np.maximum(np.floor(p.min(0)).astype(int),0);hi=np.minimum(np.ceil(p.max(0)).astype(int),n-1);yy,xx=np.mgrid[lo[1]:hi[1]+1,lo[0]:hi[0]+1];q=np.stack([xx.ravel(),yy.ravel()],1);mat=np.column_stack((p[1]-p[0],p[2]-p[0]))
    if abs(np.linalg.det(mat))<1e-6:continue
    ab=(q-p[0])@np.linalg.inv(mat).T;w=np.column_stack((1-ab[:,0]-ab[:,1],ab));ok=(w>=-.002).all(1);q=q[ok];pos=w[ok]@xyz
    if not len(q):continue
    x,y,z=pos.T
    if part=='UpperBody':
     take=np.ones(len(q),bool)
     # Front artwork: hoodie opening, white spider shirt, waistband. Rear: plain hoodie fabric.
     u=np.where(y<0,.278+x/.325*.19,.543-x/.325*.145)
     v=np.where(y<0,.464+(1.235-z)/.647*.30,.30+(1.235-z)/.647*.13)
     sleeve=np.abs(x)>=.325;u=np.where(sleeve,.54+(np.abs(x)-.325)/.29*.12,u);v=np.where(sleeve,.33+(1.23-z)/.4*.075,v)
     u=np.where(z<.59,.28+x/.32*.15,u);v=np.where(z<.59,.79+(.59-z)/.25*.10,v)
    elif part=='LowerBody':
     take=np.ones(len(q),bool);u=.28+x/.32*.15;v=.776+(.612-z)/.412*.193
    else:
     # Use the supplied shoe side artwork; retain the soles' lighter lower edge.
     take=np.ones(len(q),bool);u=.835+(y+.31)/.47*.135;v=.71+(.203-z)/.203*.067
    q=q[take];u=np.clip(u[take],0,1);v=np.clip(v[take],0,1);sx=(u*(n-1)).astype(int);sy=(v*(n-1)).astype(int)
    result[q[:,1],q[:,0]]=a[sy,sx];result[q[:,1],q[:,0],3]=255;paint[q[:,1],q[:,0]]=True
 # Pad only remapped island borders to avoid seams, keeping original source files untouched.
 for _ in range(4):
  pad=np.pad(result[:,:,:3].astype(float),((1,1),(1,1),(0,0)),mode='edge');known=np.pad(paint,1);total=np.zeros_like(result[:,:,:3],float);count=np.zeros(paint.shape)
  for dy,dx in [(0,1),(2,1),(1,0),(1,2)]:
   k=known[dy:dy+n,dx:dx+n];total+=pad[dy:dy+n,dx:dx+n]*k[:,:,None];count+=k
  take=~paint&(count>0);result[take,:3]=(total[take]/count[take,None]).astype('uint8');result[take,3]=255;paint[take]=True
 Image.fromarray(result).save(O/(name+'.png'))
 print(name,'mapped to',body,'UVs')
