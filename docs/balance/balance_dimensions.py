import json, numpy as np
f=json.load(open('final2.json',encoding='utf-8'))
EFF=dict(bat=10,MiniSlime=6,Slime=36,Bomber=21,Wraith=22,Shaman=49,Golem=240)
EXP=dict(bat=1,MiniSlime=1,Slime=4,Bomber=3,Wraith=3,Shaman=6.5,Golem=20)
STARTS=[0,50,100]+[150*i for i in range(1,12)]; RUSH=10; c=0.8859523145029475
tgt=[r['hp']/c for r in f['rows']]; R=[r['R'] for r in f['rows']]; H=[r['H'] for r in f['rows']]
DIM={
 'Forest':([('bat',0,'w'),('MiniSlime',0,'w'),('Slime',50,'m'),('Golem',600,'s')],1.0),
 'Factory':([('MiniSlime',0,'w'),('Slime',0,'m'),('Bomber',150,'m'),('Golem',600,'s')],1.0),
 'Storm':([('bat',0,'w'),('Wraith',50,'m'),('Shaman',600,'s')],0.6),
}
def fit(ros,target):
    def make(k):
        w={m:EFF[m]**k*(2.0 if r=='w' else 1.0) for m,r in ros}; s=sum(w.values()); return {m:v/s for m,v in w.items()}
    lo,hi=-3,3
    for _ in range(80):
        mid=(lo+hi)/2; hp=sum(v*EFF[m] for m,v in make(mid).items())
        lo,hi=(mid,hi) if hp<target else (lo,mid)
    mx=make((lo+hi)/2); return mx, sum(v*EFF[m] for m,v in mx.items())
out={}
for d,(ros_all,s) in DIM.items():
    out[d]=[]; prevH=0
    print('##',d)
    for i in range(14):
        ros=[(m,r) for m,u,r in ros_all if u<=STARTS[i] and not (i==RUSH and r=='s')]
        sc = 1.0 if (s==1.0 or i<6) else 1.0-(1.0-s)*(i-5)/8
        mx,hp=fit(ros,tgt[i]*sc)
        h=H[i]*tgt[i]/hp
        if i==RUSH: h=max(prevH*1.03, H[i]*tgt[i]/hp) if d!='x' else h
        ok='' if h>=prevH*1.0299 else ' <<< NOT MONOTONE'
        itv={m:round(1/(R[i]*v),2) for m,v in mx.items() if v>0.004}
        ex=sum(v*EXP[m] for m,v in mx.items())
        out[d].append(dict(itv=itv,hp=hp,H=round(h,2),exp=ex)); prevH=h
        print(i+1,'hp %.1f H %.2f exp %.2f'%(hp,h,ex),itv,ok)
json.dump(out,open('dims.json','w'),indent=1)
