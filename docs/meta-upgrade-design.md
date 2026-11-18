# 영구 강화 (차원력) 설계

기준: 2026-11-18. 한 판이 끝나면 진행도에 따라 재화 **차원력**을 얻고, 타이틀에서 영구 스탯 강화를 산다.

## 1. 결정 사항

| 항목 | 결정 |
|---|---|
| 상점 위치 | 타이틀 메뉴에 "차원 강화" 버튼 → 강화 패널 |
| 적용 대상 | 양쪽 플레이어 공통 (게임 시작 시 Left / Right PlayerStats 에 같은 값) |
| 획득 | 웨이브 · 처치 수 · 레벨 각각 계수. 패배 시 감소 배율 |
| 초기화(환불) | 구현하지 않음 (디버그용 `ResetAll` 만) |
| 데모 빌드 | `DEMO_INVINCIBLE` 정의 시 차원력 사실상 무한 (저장 키 분리) |

## 2. 강화 목록

가격(n → n+1) = `round(baseCost × costGrowth^n)`, 기본 baseCost 20, costGrowth 1.5.

| id | 스탯 | 방식 | 레벨당 | 최대 | 최대치 | baseCost |
|---|---|---|---|---|---|---|
| damage | Damage | Multiply | +2% | 10 | +20% | 20 |
| attack_speed | AttackSpeed | Multiply | +2% | 10 | +20% | 20 |
| range | Range | Multiply | +4% | 5 | +20% | 20 |
| move_speed | MoveSpeed | Multiply | +2% | 5 | +10% | 20 |
| hp | HP | Multiply | +5% | 10 | +50% | 20 |
| hp_regen | HPRegen | Add | +0.2/초 | 5 | +1.0/초 | 20 |
| pickup_range | PickupRange | Multiply | +5% | 5 | +25% | 20 |
| duration | Duration | Multiply | +3% | 5 | +15% | 20 |
| projectile_count | ProjectileCount | Add | +1 | 1 | +1 | 300 |

- 몬스터 감소는 진영 하나에만 걸리는 효과라 제외.
- 전부 최대 비용 ≈ 8,400 (레벨 10 스탯 3종 × 2,267 + 레벨 5 스탯 5종 × 264 + 투사체 300).

## 3. 적용 방식 (PlayerStats 영구 계층)

레벨업 패시브(ΣAdd / ΣMultiply)와 **별도 배열**로 둔다.

```
최종값 = (base + ΣAdd + 영구Add) × max(minMultiplier, 1 + ΣMultiply) × (1 + 영구Mul)
```

- 하한 0.5 는 레벨업 버프·너프에만 걸린다 (balance-design 4.2 의미 유지). 영구 강화는 그 위에 곱한다.
- 무기가 쓰는 `GetAdd` / `GetMultiplier` 도 영구 계층을 포함한다.
- `ResetModifiers` (무기 테스트 씬) 는 영구 계층을 지우지 않는다.
- 게임 시작 시 `PermanentUpgradeApplier` 가 `MetaProgress` 레벨을 읽어 양쪽 PlayerStats 에 한 번 적용한다.

## 4. 차원력 획득

```
차원력 = floor((클리어 웨이브 × A + 양쪽 처치 합 × B + 레벨 × C) × 결과 배율)
```

| 계수 | 초안 | 완주 시 기여 |
|---|---|---|
| A (웨이브, 최대 14) | 15 | 210 |
| B (처치 합) | 0.005 | ≈ 100 (추정, 플레이 후 조정) |
| C (레벨, ExpSystem 공유 레벨) | 2 | ≈ 100 |
| 승리 배율 | 1.0 | |
| 패배 배율 | 0.5 | |

- 완주 ≈ 400 / 판, 전체 강화 ≈ 20판.
- 계수는 `MetaRewardConfig` (ScriptableObject) 에 두고 인스펙터에서 조정.
- 지급은 `GameOverManager` 종료 처리에서 한 번만 (재시작은 씬 재로드라 새 판).
- 결과 패널에 항목별(웨이브·처치·레벨) 획득량, 배율, 합계, 보유량 표시.

## 5. 저장 (`MetaProgress`, static)

- PlayerPrefs 키: `Meta.Currency`, `Meta.Level.{id}`. GameSettings 와 같은 패턴 (처음 접근 시 로드, 변경 시 기록 + Save).
- API: `Currency`, `GetLevel(data)`, `GetCost(data)`, `CanPurchase(data)`, `TryPurchase(data)`, `AddCurrency(int)`, `ResetAll()`(디버그), `OnChanged`.
- 데모 빌드(`DEMO_INVINCIBLE`): 키 접두사 `MetaDemo.` 로 분리하고 차원력이 9,999,999 아래면 그 값으로 채운다 (일반 빌드 세이브 오염 방지 — 같은 PC 에서 PlayerPrefs 공유).

## 6. 구성 요소

| 종류 | 이름 | 위치 |
|---|---|---|
| SO | `PermanentUpgradeData`, `PermanentUpgradeRegistry`, `MetaRewardConfig` | `Script/MetaSystem/` |
| static | `MetaProgress`, `MetaReward` (계산) | `Script/MetaSystem/` |
| 컴포넌트 | `PermanentUpgradeApplier` (GameScene) | `Script/MetaSystem/` |
| UI | `PermanentUpgradePanel`, `PermanentUpgradeRow` (타이틀) | `Script/UI/Title/` |
| 빌더 | `Builders.PermanentUpgradeBuilder` (에셋 9종 + 레지스트리 + 설정, 멱등) | `Editor/Builders/` |
| 문자열 | `meta.*` 키 | `StringTable.csv` |

## 7. 테스트

- EditMode: 가격 공식, 최대 레벨에서 구매 실패, 잔액 부족 시 실패, 저장·로드, 보상 계산(승리/패배 배율), 영구 계층 계산식과 하한 분리.
- PlayMode: GameScene 시작 시 양쪽에 적용, 게임 종료 시 1회 지급, `ResetModifiers` 후 영구 계층 유지.
