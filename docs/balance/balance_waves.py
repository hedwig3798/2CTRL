import sys, os; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import json, numpy as np
import balance_model as B
d=json.load(open('final.json',encoding='utf-8'))
W=d['waves']; EFF=B.EFF_HP; MON=B.MON; c=d['hp_scale']
MIX=[ # bat mini slime bomber wraith shaman golem
 [.75,.25,0,0,0,0,0],
 [.70,.20,.10,0,0,0,0],
 [.58,.17,.25,0,0,0,0],
 [.52,.14,.24,0,.10,0,0],
 [.46,.10,.26,0,.18,0,0],
 [.38,.08,.25,.13,.16,0,0],
 [.36,.06,.22,.13,.15,.08,0],
 [.36,.06,.20,.13,.14,.09,.02],
 [.34,.05,.19,.13,.15,.10,.04],
 [.32,.04,.19,.13,.16,.11,.05],
 [.32,.18,.18,.22,.10,0,0],
 [.30,.03,.18,.13,.16,.12,.08],
 [.27,.03,.18,.13,.17,.13,.09],
 [.24,.03,.18,.13,.18,.14,.10],
]
for r in MIX: assert abs(sum(r)-1)<1e-6, r
hp=[sum(f*EFF[m] for f,m in zip(MIX[i],MON)) for i in range(14)]
# 필요 곱 P = 시작 전투력 / (스폰속도 x 목표여유율) / c
P=[w['S_start']/(w['rate']*B.margin(w['start']))/c for w in W]
H=[]; 
for i in range(14):
    h=P[i]/hp[i]
    if i==1: h=1.90              # 55초 체크포인트가 2레벨 직후라 튀는 값을 완화 (S 60 -> 80 구간의 중간)
    if i==B.RUSH: h=H[-1]*1.03   # 러시: 추세만 유지 (숨 고르기 웨이브)
    if i and h<H[-1]*1.03: h=H[-1]*1.03
    H.append(h)
rows=[]
for i,w in enumerate(W):
    req=w['rate']*hp[i]*c*H[i]; k=w['req']/req
    exp=sum(f*B.EFF_EXP[m] for f,m in zip(MIX[i],MON))
    rows.append(dict(i=i+1,start=w['start'],R=w['rate'],hp=hp[i]*c,H=H[i],P=P[i],req=req,
      m0=w['S_start']/req,m1=w['S_end']/req,p10=w['p10_start']*k,r0=w['rnd_start']*k,r1=w['rnd_end']*k,
      exp=exp, dmg=B.dmg_mul(i), spd=B.spd_mul(i), mix=MIX[i],
      itv=[(round(1/(w['rate']*f),2) if f>0 else None) for f in MIX[i]], pas=w['passives'], mul=w['mul'], S0=w['S_start'],S1=w['S_end']))
    print(i+1,w['start'],'R%.2f hp%.1f H%.2f req%.0f'%(w['rate'],hp[i]*c,H[i],req),'m %.2f->%.2f p10 %.2f rnd %.2f->%.2f exp%.2f'%(rows[-1]['m0'],rows[-1]['m1'],rows[-1]['p10'],rows[-1]['r0'],rows[-1]['r1'],exp), rows[-1]['itv'])
# EXP: 양 진영 수입, 목표 레벨 시각
def income(t):
    i=max(j for j in range(14) if B.STARTS[j]<=t); return 2*W[i]['rate']*rows[i]['exp']
need=[];prev=0
for k,t1 in enumerate(B.LEVEL_TIMES[:55]):
    ts=np.linspace(prev,t1,800); need.append(float(np.trapz([income(x) for x in ts],ts))); prev=t1
L=np.arange(1,56)
# 공식 맞춤: need(L) = A * L^b  (로그 최소제곱)
b,lnA=np.polyfit(np.log(L[:50]),np.log(need[:50]),1); A=np.exp(lnA)
print('fit need = %.2f * L^%.3f'%(A,b))
# 공식으로 실제 레벨 시각 재계산
def sim_levels(f):
    t=0;e=0;lv=1;times=[];dt=0.5
    while t<1800 and lv<=55:
        e+=income(t)*dt; t+=dt
        while e>=f(lv): e-=f(lv); times.append((lv,t)); lv+=1
    return times
for A2,b2,o in ((A,b,0),(20,1.2,10),(17.5,1.2,9)):
    tl=sim_levels(lambda l:o+A2*l**b2); print(A2,b2,o)
    print([(l,round(t)) for l,t in tl if l in (1,2,3,5,10,15,20,25,30,35,40,45,50)], 'levels at 1800:',len(tl))
json.dump(dict(rows=rows,need=need,fit=[A,b]),open('final2.json','w'),indent=1)
