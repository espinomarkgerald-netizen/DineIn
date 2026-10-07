from pathlib import Path
import numpy as np,json
from PIL import Image
R=Path.cwd();O=R/'Assets/_Project/Player/Assets/Appearance/Costumes';uvs=json.loads((R/'Temp/Costumes/body-uvs.json').read_text())
for name,body in [('BunnyCop','Female'),('BunnyCopMale','Male')]:
 src=Image.open(R/'ArtSource/Costumes'/'BunnyCop-reference.png').convert('RGBA');a=np.array(src);n=src.width;result=a.copy();paint=np.zeros(a.shape[:2],bool)
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
     # Front vest/badge/belt occupy the bottom central artwork; back uses plain navy fabric.
     front=y<0
     u=np.where(front,.285+x/.325*.19,.42)
     v=np.where(front,.69+(1.235-z)/.647*.28,.61+(1.235-z)/.647*.06)
     sleeve=np.abs(x)>=.325
     u=np.where(sleeve,.38+(np.abs(x)-.325)/.29*.045,u)
     v=np.where(sleeve,.08+(np.abs(x)-.325)/.29*.14,v)
     # Female torso extends below the belt; sample navy fabric rather than outside the artwork.
     u=np.where(z<.59,.68,u);v=np.where(z<.59,.65,v)
    elif part=='LowerBody':
     take=np.ones(len(q),bool);u=.675+x/.32*.04;v=.55+(.612-z)/.412*.15
    else:
     take=np.ones(len(q),bool);u=.84+(y+.31)/.47*.05;v=.715+(.203-z)/.203*.045
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
