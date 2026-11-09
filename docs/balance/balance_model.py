# 2CTRL 최종 밸런스 계산 (기획서 docs/balance-design.md 의 모든 표를 이 스크립트로 생성)
import math, random, json, sys
import numpy as np

# ===== 1. 무기 =====
# 현재 프리팹 기준 기본 유효 DPS = 데미지 x 공속 x 발사수 x 표준 밀도 평균 타격 수
# steps = 1~10단계 각 단계의 유효 DPS 배율 (현재 업그레이드 표 기준 추정)
W = {
 "Boomerang": dict(base=12*0.7*1*3.0, a=0.6, steps=[1.2,2.0,1.05,1.4/1.2,1.25,1.15,1.5,1.6/1.4,1.05,1.5]),
 "Chain":     dict(base=10*0.8*1*4.0, a=0.5, steps=[1.2,1.25,1.15,1.05,1.8,1.4/1.2,1.4,1.3/1.15,1.6/1.4,1.45]),
 "Drone":     dict(base=2*6*1.5*1.0, a=0.1, steps=[1.2,1.15,1.5,1.4/1.2,2.0,1.05,4/3,1.6/1.4,1.3/1.15,1.8]),
 "Explosive": dict(base=18*0.6*1*3.0, a=0.9, steps=[1.2,1.2**1.5,1.15,1.4/1.2,2.0,1.03,(1.4/1.2)**1.5,1.6/1.4,1.3/1.15,1.23]),
 "Laser":     dict(base=14*0.8*1*2.5, a=0.6, steps=[1.2,1.28,1.15,1.15,1.9,1.4/1.2,1.25,1.05,1.6/1.4,1.9]),
 "Mine":      dict(base=25*0.5*1*2.5*0.7, a=0.8, steps=[1.2,1.2**1.5,2.0,1.08,(1.6/1.2)**1.5,1.15,1.4/1.2,1.1,1.5,1.25]),
 "PoisonZone":dict(base=(4/0.5)*1.5*2.5, a=0.9, steps=[1.2,1.2**1.5,4/3,1.25,2.0,1.4/1.2,(1.4/1.2)**1.5,5/4,1.6/1.4,1.4]),
 "Shockwave": dict(base=6*0.35*1*8.0, a=1.0, steps=[1.2,1.15**2,1.15,1.03,1.9,1.4/1.2,(1.3/1.15)**2,1.3/1.15,1.6/1.4,1.5]),
 "SplitShot": dict(base=12*0.9*(1+6*0.4*0.4), a=0.5, steps=[1.2,1.16,1.15,1.19,2.0,1.1,1.15,1.6/1.4,1.15,1.5]),
 "Sweep":     dict(base=15*1.2*1*2.0, a=0.9, steps=[1.2,1.3,1.15,1.03,1.8,1.4/1.2,1.25,1.3/1.15,1.6/1.4,1.35]),
}
TARGET_BASE = {k: 26.0 for k in W}; TARGET_BASE["Sweep"] = 32.0; TARGET_BASE["Shockwave"] = 22.0
TARGET_L10 = 9.0
R_REF = 2.0      # 기본 유효 DPS 를 잰 표준 스폰 속도(마리/초)
MAX_W = 6        # 플레이어 무기 보유 상한
PANELS = 3
import os
POOL_W = dict(up=float(os.environ.get("WU","1.5")), weapon=float(os.environ.get("WW","1.5")), buff=1.0)   # 보상 풀 항목 가중치

def scaled_steps(k):
    s = np.array(W[k]["steps"]); scale = math.log(TARGET_L10) / math.log(s.prod())
    return list(np.exp(np.log(s) * scale))
STEPS = {k: scaled_steps(k) for k in W}
CUM = {k: [float(np.prod(STEPS[k][:i])) for i in range(11)] for k in W}

# ===== 2. 패시브 =====  (버프%, 상대 너프%, DPS 반영 여부)
PASSIVE = {"Damage": (0.05, 0.03, 1), "AttackSpeed": (0.05, 0.03, 1), "Range": (0.10, 0.06, 0),
           "MoveSpeed": (0.05, 0.03, 0), "HP": (0.10, 0.06, 0)}
MIN_MUL = 0.5
SURV_VALUE = 0.35   # 생존 스텟 1% = DPS 0.35% 로 환산 (AI 선택용)

# ===== 3. 레벨 =====
LV_A, LV_P, LV_N = 23.0, 1.08, 60   # 6슬롯: 26.3 -> 23.0 (레벨업 약 12% 가속)
LEVEL_TIMES = [LV_A * k ** LV_P for k in range(1, LV_N + 1)]

# ===== 4. 웨이브 =====
T_END = 1800.0
STARTS = [0, 50, 100] + [150 * i for i in range(1, 12)]   # 14 웨이브
NW = len(STARTS)
def wave_len(i): return (STARTS[i+1] if i+1 < NW else T_END) - STARTS[i]
R_START, R_LAST = 1.2, 10.0
RUSH = STARTS.index(1200)
def rate(i):
    r = R_START * (R_LAST / R_START) ** (STARTS[i] / STARTS[-1])
    return r * (1.5 if i == RUSH else 1.0)
HP_G = 1.10          # 150초당 HP +10%  (웨이브마다 반드시 증가)
def hp_mul(i): return HP_G ** (STARTS[i] / 150.0)
def margin(t):       # 목표 여유율 (전투력 / 요구 전투력), 시작 2.0 -> 27:30 1.05
    x = min(1.0, t / STARTS[-1]); return 1.05 + 0.95 * (1 - x) ** 1.6
DMG_G = 0.15         # 150초당 몬스터 데미지 +15% (선형)
def dmg_mul(i): return 1 + DMG_G * STARTS[i] / 150.0
SPD_G = 0.025
def spd_mul(i): return 1 + SPD_G * STARTS[i] / 150.0

class Player:
    def __init__(s, start):
        s.w = {start: 0}; s.buff = dict.fromkeys(PASSIVE, 0); s.nerf = dict.fromkeys(PASSIVE, 0)
    def mul(s, k): return max(MIN_MUL, 1 + PASSIVE[k][0]*s.buff[k] - PASSIVE[k][1]*s.nerf[k])
    def dps(s, r):
        d = r / R_REF
        raw = sum(TARGET_BASE[n] * CUM[n][lv] * d ** W[n]["a"] for n, lv in s.w.items())
        return raw * s.mul("Damage") * s.mul("AttackSpeed")
    def offers(s):
        items = [("weapon", n) for n in W if n not in s.w] if len(s.w) < MAX_W else []
        items += [("up", n) for n, lv in s.w.items() if lv < 10]
        items += [("buff", k) for k in PASSIVE]
        wts = [POOL_W[t] for t, _ in items]; res = []
        for _ in range(min(PANELS, len(items))):   # 가중치 비복원 추출 (RewardManager.GetRandomUnique 와 동일)
            j = random.choices(range(len(items)), weights=wts)[0]
            res.append(items.pop(j)); wts.pop(j)
        return res
    def _do(s, o, sign):
        t, n = o
        if t == "weapon":
            if sign > 0: s.w[n] = 0
            else: del s.w[n]
        elif t == "up": s.w[n] += sign
        else: s.buff[n] += sign
    def value(s, o, r):
        if o[0] == "buff" and PASSIVE[o[1]][2] == 0: return SURV_VALUE * PASSIVE[o[1]][0]
        b = s.dps(r); s._do(o, 1); a = s.dps(r); s._do(o, -1); return math.log(a / b)

def run(pa, pb, seed, cps):
    random.seed(seed)
    a, b = Player(random.choice(list(W))), Player(random.choice(list(W)))
    li, out = 0, {}
    for cp in cps:
        wi = max(i for i in range(NW) if STARTS[i] <= cp); r = rate(wi)
        while li < len(LEVEL_TIMES) and LEVEL_TIMES[li] <= cp:
            for me, op, pol in ((a, b, pa), (b, a, pb)):
                offs = me.offers()
                o = max(offs, key=lambda x: me.value(x, r)) if pol == "smart" else random.choice(offs)
                me._do(o, 1)
                if o[0] == "buff": op.nerf[random.choice(list(PASSIVE))] += 1
            li += 1
        out[cp] = (a.dps(r), len(a.w), sum(a.buff.values()), a.mul("Damage") * a.mul("AttackSpeed"))
    return out

def sim(pa, pb, n, cps):
    acc = {cp: [] for cp in cps}
    for i in range(n):
        for cp, v in run(pa, pb, i, cps).items(): acc[cp].append(v)
    return {cp: dict(p10=float(np.percentile([v[0] for v in vs], 10)), p50=float(np.median([v[0] for v in vs])),
                     p90=float(np.percentile([v[0] for v in vs], 90)), passives=float(np.median([v[2] for v in vs])),
                     mul=float(np.median([v[3] for v in vs]))) for cp, vs in acc.items()}

# ===== 5. 몬스터 =====
MON = ["bat", "MiniSlime", "Slime", "Bomber", "Wraith", "Shaman", "Golem"]
BASE_HP = dict(bat=10, MiniSlime=6, Slime=24, Bomber=14, Wraith=22, Shaman=40, Golem=160)
# 유효 HP: Slime 분열(미니 2), Bomber/Golem 부활 50%, Shaman 생존 중 미니 1.5 소환
EFF_HP = dict(bat=10, MiniSlime=6, Slime=24+2*6, Bomber=14*1.5, Wraith=22, Shaman=40+1.5*6, Golem=160*1.5)
EXP = dict(bat=1, MiniSlime=1, Slime=2, Bomber=3, Wraith=3, Shaman=5, Golem=20)
EFF_EXP = dict(bat=1, MiniSlime=1, Slime=2+2, Bomber=3, Wraith=3, Shaman=5+1.5, Golem=20)
# 구성 비율 (스폰 수 기준, 합 1) — 아래 FIT 로 목표 평균 HP 에 맞춘다
UNLOCK = dict(bat=0, MiniSlime=0, Slime=100, Bomber=450, Wraith=300, Shaman=600, Golem=750)

def fit_mix(i, target):
    """해금된 몬스터로 목표 평균 유효 HP 를 맞춘다. 약한 순서로 기본 비중을 두고 강한 쪽 비중 k 를 이분 탐색"""
    t = STARTS[i]
    avail = [m for m in MON if UNLOCK[m] <= t]
    if i == RUSH:   # 물량 러시: 약한 몹 위주 고정
        mix = dict(bat=.45, MiniSlime=.25, Slime=.05, Bomber=.20, Wraith=.05, Shaman=0, Golem=0)
        return mix
    def make(k):
        w = {m: (EFF_HP[m] ** k if m in avail else 0.0) for m in MON}
        w["bat"] *= 2.0   # 박쥐는 항상 기본 물량
        s = sum(w.values()); return {m: w[m] / s for m in MON}
    lo, hi = -2.0, 2.0
    for _ in range(60):
        mid = (lo + hi) / 2
        hp = sum(make(mid)[m] * EFF_HP[m] for m in MON)
        lo, hi = (mid, hi) if hp < target else (lo, mid)
    return make((lo + hi) / 2)

def main():
    n = int(sys.argv[1]) if len(sys.argv) > 1 else 1000
    cps = sorted(set([s + 5 for s in STARTS] + [STARTS[i] + wave_len(i) - 5 for i in range(NW)] + [T_END]))
    smart = sim("smart", "smart", n, cps)
    rnd = sim("random", "smart", n, cps)
    S0 = [smart[s + 5]["p50"] for s in STARTS]
    raw = [S0[i] / (rate(i) * margin(STARTS[i]) * hp_mul(i)) for i in range(NW)]
    hp_scale = raw[0] / 9.0           # 1 웨이브 평균 유효 HP 9 (박쥐 75% + 미니 25%) 기준 HP 보정
    waves = []
    for i in range(NW):
        target = raw[i] / hp_scale
        mix = fit_mix(i, target) if i > 0 else dict(bat=.75, MiniSlime=.25, Slime=0, Bomber=0, Wraith=0, Shaman=0, Golem=0)
        avg_hp = sum(mix[m] * EFF_HP[m] for m in MON) * hp_scale
        req = rate(i) * avg_hp * hp_mul(i)
        e = STARTS[i] + wave_len(i) - 5
        waves.append(dict(i=i+1, start=STARTS[i], len=wave_len(i), rate=rate(i), hp_mul=hp_mul(i), dmg_mul=dmg_mul(i), spd_mul=spd_mul(i),
            target_hp=target, avg_hp=avg_hp, req=req,
            m_start=S0[i] / req, m_end=smart[e]["p50"] / req, p10_start=smart[STARTS[i]+5]["p10"] / req,
            rnd_start=rnd[STARTS[i]+5]["p50"] / req, rnd_end=rnd[e]["p50"] / req,
            S_start=S0[i], S_end=smart[e]["p50"], passives=smart[e]["passives"], mul=smart[e]["mul"],
            avg_exp=sum(mix[m] * EFF_EXP[m] for m in MON),
            mix={m: mix[m] for m in MON},
            interval={m: (1.0 / (rate(i) * mix[m]) if mix[m] > 0.005 else None) for m in MON}))
    # EXP 필요량: 목표 레벨 시각 사이 수입(양 진영 합, 처치 = 스폰) 적분
    def income(t):
        i = max(j for j in range(NW) if STARTS[j] <= t); return 2 * rate(i) * waves[i]["avg_exp"]
    levels, prev = [], 0.0
    for k, t1 in enumerate(LEVEL_TIMES):
        ts = np.linspace(prev, t1, 600); need = float(np.trapz([income(x) for x in ts], ts))
        levels.append(dict(lv=k + 1, t=t1, need=need)); prev = t1
    print(json.dumps(dict(hp_scale=hp_scale, waves=waves, levels=levels,
        weapons={k: dict(base_now=W[k]["base"], L10_now=float(np.prod(W[k]["steps"])), base_new=TARGET_BASE[k],
                         steps=STEPS[k], cum=CUM[k]) for k in W}), ensure_ascii=False, indent=1))

if __name__ == "__main__":
    main()
