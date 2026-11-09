# 차원별 추가 무기 설계 (확정 2026-11-03)

무기 7종을 추가한다. 무기 보상은 각 차원 무기 풀(`DimensionData.weaponRewards`)에 넣는다.
수치는 `docs/balance-design.md` 3.2장 기준으로 정한 기본 유효 DPS 목표에 맞춘 초기값이다. 플레이테스트 후 조정한다.

| 차원 | 추가 무기 | 차원 무기 수 |
|---|---|---|
| 숲 | 덩굴(Vine), 정령(Spirit), 뿌리 속박(Root) | 4 → 7 |
| 공장 | 톱날(Saw), 화염 방사(Flame) | 3 → 5 |
| 폭풍 | 회오리(Tornado), 낙뢰(Lightning) | 3 → 5 |

## 공통 베이스 변경 (메인)
- `WEAPON_TYPE` 추가
  - `Capped`: 쿨타임마다 1개 발사. 살아 있는 발사체가 fireCount 이상이면 발사하지 않고 타이머도 멈춘다. (정령, 회오리)
  - `Wave`: 살아 있는 발사체가 0개가 된 뒤 쿨타임이 지나면 fireCount 개를 한 번에 발사한다. (톱날)
- `WeaponParam` 추가 (발사체 고유값 115~, 플래그 208~)
- `ArrowMovemnt.LastMoveDirection`: 마지막 이동 방향(정규화, 기본 오른쪽). 화염 방사가 사용한다.

## 숲

### 덩굴 (Vine) — `Weapons/Vine/`
- 플레이어가 지나간 자리에 가시 덩굴 조각을 남긴다. 조각은 틱마다 범위 안의 모든 적에게 데미지.
- Repeat, 공속 4(0.25초마다 조각 생성 시도). 마지막 조각과의 거리가 `segmentSpacing`(0.7) 미만이면 생성하지 않는다(가만히 서 있으면 쌓이지 않음).
- 기본: 데미지 6, 틱 0.5초, 반지름(areaRadius) 0.6, 수명 3초.
- `PoisonZoneProjectile` 을 상속해 재사용한다 (착지 이동 없음, 발사 위치 = 주인 위치).
- ★10 `FlagVineThorn`: 조각이 틱 데미지를 줄 때 주변 가장 가까운 적에게 가시 1발(관통 없음, 데미지 50%). 조각마다 가시 쿨타임 1초.

| 1 | 2 | 3 | 4 | 5 ★ | 6 | 7 | 8 | 9 | 10 ★ |
|---|---|---|---|---|---|---|---|---|---|
| 데미지 +20% | 범위 +20% | 틱 간격 -10% | 데미지 +20% | 덩굴 수명 +50% | 데미지 +20% | 범위 +20% | 데미지 +20% | 데미지 +20% | 가시 발사 |

### 정령 (Spirit) — `Weapons/Spirit/`
- `Capped`, 공속 0.5(2초마다 1마리), fireCount 3(최대 3마리).
- 대기: 플레이어 주변을 떠다닌다. 사거리(4) 안에 적이 오면 가장 가까운 적을 추적한다.
- 추적 중 대상에 닿으면 틱(0.4초)마다 데미지. 총 `hitCount`(6)틱을 주면 소멸.
- 틱을 다 쓰기 전에 대상이 죽으면 사거리 안의 다음 적을 추적한다. 없으면 대기로 돌아간다(남은 틱 유지).
- 기본: 데미지 8, 추적 속도 6.

| 1 | 2 | 3 | 4 | 5 ★ | 6 | 7 | 8 | 9 | 10 ★ |
|---|---|---|---|---|---|---|---|---|---|
| 데미지 +20% | 공속 +15% | 추적 속도 +20% | 데미지 +20% | 정령 +2 (최대 5) | 데미지 +20% | 공속 +15% | 데미지 +20% | 사거리 +1.5 | 공격 횟수 +4 |

### 뿌리 속박 (Root) — `Weapons/Root/`
- Repeat, 공속 0.4(2.5초마다), fireCount 1. 사거리(6) 안 랜덤한 위치에 뿌리 지역 생성.
- 지역 위 적: 틱(0.5초)마다 데미지 + 둔화 40%.
- 기본: 데미지 5, 반지름 1.2, 수명 4초.
- `PoisonZoneProjectile` 을 상속해 재사용한다 (둔화는 플래그 없이 항상).
- ★10 `FlagRootChase`: 지역이 가장 가까운 적을 향해 천천히(1.2) 이동.

| 1 | 2 | 3 | 4 | 5 ★ | 6 | 7 | 8 | 9 | 10 ★ |
|---|---|---|---|---|---|---|---|---|---|
| 데미지 +20% | 범위 +20% | 공속 +15% | 데미지 +20% | 지역 +1 (2개) | 데미지 +20% | 수명 +1초 | 데미지 +20% | 데미지 +20% | 추적 |

## 공장

### 톱날 (Saw) — `Weapons/Saw/`
- `Wave`, 공속 1(전부 소멸 후 1초 뒤 재생성), fireCount 2.
- 화면(그 진영 카메라 시야) 왼쪽 또는 오른쪽 끝, 랜덤한 높이에서 생성해 가로로 일직선 이동(속도 7).
- 관통. 한 톱날은 같은 적을 한 번만 타격. 반대편 화면 밖으로 나가면 소멸.
- 기본: 데미지 15.
- ★10 `FlagSawBounce`: 화면 경계에 닿으면 화면 안쪽의 랜덤한 방향으로 튕긴다. 이후 톱날은 소멸하지 않는다(수명 무한, 튕길 때마다 타격 기록 초기화). 업그레이드 시점에 날아가던 톱날도 적용.

| 1 | 2 | 3 | 4 | 5 ★ | 6 | 7 | 8 | 9 | 10 ★ |
|---|---|---|---|---|---|---|---|---|---|
| 데미지 +20% | 크기 +20% | 이동 속도 +20% | 데미지 +20% | 톱날 +2 (4개) | 데미지 +20% | 공속 +15% | 데미지 +20% | 데미지 +20% | 튕김 |

### 화염 방사 (Flame) — `Weapons/Flame/`
- Repeat(주기적 분사), 공속 0.8(1.25초마다), 분사 시간 0.8초.
- 분사 중 플레이어를 따라가며 `ArrowMovemnt.LastMoveDirection` 방향 부채꼴(사거리 3, 각도 60°)에 0.2초마다 범위 안 모든 적 타격.
- 기본: 데미지 4, 넉백 약하게.
- ★10 `FlagBurn`: 맞은 적에 화상. 0.5초마다 데미지 30%, 3초. 다시 맞으면 지속시간만 갱신(중첩 없음).

| 1 | 2 | 3 | 4 | 5 ★ | 6 | 7 | 8 | 9 | 10 ★ |
|---|---|---|---|---|---|---|---|---|---|
| 데미지 +20% | 사거리 +20% | 공속 +15% | 데미지 +20% | 부채꼴 각도 +30° | 데미지 +20% | 분사 시간 +0.3초 | 데미지 +20% | 데미지 +20% | 화상 |

## 폭풍

### 회오리 (Tornado) — `Weapons/Tornado/`
- `Capped`, 공속 0.33(3초), fireCount 1. 플레이어 근처에 생성, 랜덤한 방향으로 천천히(1.5) 떠돈다(1.5초마다 방향 변경). 수명 5초.
- 끌어당김 반경(2) 안 적을 중심으로 끌어당기고, 타격 반경(0.8) 안 적을 0.3초마다 타격.
- 기본: 데미지 6.
- ★10 `FlagTornadoPermanent`: 소멸하지 않는다. fireCount 개가 영구히 돌아다닌다. 플레이어와 `leashDistance`(6)보다 멀어지면 플레이어 쪽으로 이동.

| 1 | 2 | 3 | 4 | 5 ★ | 6 | 7 | 8 | 9 | 10 ★ |
|---|---|---|---|---|---|---|---|---|---|
| 데미지 +20% | 크기 +20% | 수명 +1초 | 데미지 +20% | 회오리 +1 (2개) | 데미지 +20% | 끌어당김 +30% | 데미지 +20% | 데미지 +20% | 영구 |

### 낙뢰 (Lightning) — `Weapons/Lightning/`
- Repeat, 공속 0.7, fireCount 1. 발사체 1개가 사거리(8) 안 무작위 적 `StrikeCount`(1)명을 고른다(중복 없음).
- 대상 위에 표식 0.5초 → 낙뢰. 낙뢰 반경(areaRadius) 0.8 범위 데미지.
- 기본: 데미지 25.
- ★10 `FlagStormLink`: 낙뢰 직후 대상들을 전기선으로 연결. 3명 이상이면 삼각형 안쪽, 2명이면 두 점 사이 선(두께 0.6) 위의 적에게 데미지 60%. 1명이면 없음.

| 1 | 2 | 3 | 4 | 5 ★ | 6 | 7 | 8 | 9 | 10 ★ |
|---|---|---|---|---|---|---|---|---|---|
| 데미지 +20% | 범위 +20% | 공속 +15% | 데미지 +20% | 대상 +2 (최대 3) | 데미지 +20% | 사거리 +1.5 | 데미지 +20% | 데미지 +20% | 연결 |

## 6슬롯용 추가 무기 (확정 2026-11-09)

공장·폭풍의 무기를 5종 → 6종으로 늘린다. 숲은 7종 유지.

### 회전 칼날 (SpinBlade) — 공장, `Weapons/SpinBlade/`
- 쿨타임 60초 고정(공속 1/60). 무기를 얻는 순간부터 카운트하고 첫 발동도 60초 뒤.
- 대상: 무기의 `targetLayer` 진영에서 살아 있는 몬스터 전부 (화면 밖 포함, 상대 진영 제외). 거리 제한 없음. 기존 `TargetFinder` 는 버퍼가 64라 쓰지 않고 전체 탐색을 따로 둔다.
- 기본 데미지 100. 무기 태그는 Damage 만 (공속·사거리·투사체 수·지속시간 패시브 영향 없음).
- 연출: 발동 약 1.5초 전부터 칼날 2개가 플레이어 몸에서 양쪽으로 천천히 나온다 → 발동 순간 약 0.4초에 빠르게 한 바퀴 회전 → 사라진다.
- 타격: 회전 0.4초 동안 칼날이 지나가는 각도 순서로 대상을 나눠 처리한다 (한 프레임 대량 처치 방지). 대상은 회전 시작 시점에 확정.
- ★10 `FlagSpinBladeExecute`: 데미지 대신 즉사. 부활·분열 등 사망 키워드를 무시한다 (다시 일어나지 않고 분열체도 남기지 않음). EXP 보석은 정상 드롭. 통계 데미지는 실제 남은 HP 기준.

| 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 ★ |
|---|---|---|---|---|---|---|---|---|---|
| 데미지 +60% | 데미지 +60% | 데미지 +60% | 데미지 +60% | 데미지 +60% (★ 없음) | 데미지 +60% | 데미지 +60% | 데미지 +60% | 데미지 +60% | 모든 몬스터 즉사 |

### 전격 구체 (StormOrb) — 폭풍, `Weapons/StormOrb/`
- 전기 구체 fireCount(2)개가 플레이어 주위를 공전한다 (반경 2, 1바퀴 1.8초). 무기를 얻으면 항상 유지.
- 닿은 적을 타격. 같은 구체는 같은 적을 0.5초에 한 번만 타격.
- 기본: 데미지 9, 무기 태그 Damage|Range|ProjectileCount (Range 는 공전 반경에 적용).
- ★5: 구체 +2 (4개).
- ★10 `FlagStormOrbLink`: 이웃한 구체를 전기선으로 잇고, 선 위의 적에게 0.3초마다 데미지 60%.

| 1 | 2 | 3 | 4 | 5 ★ | 6 | 7 | 8 | 9 | 10 ★ |
|---|---|---|---|---|---|---|---|---|---|
| 데미지 +20% | 공전 반경 +20% | 공전 속도 +20% | 데미지 +20% | 구체 +2 (4개) | 데미지 +20% | 공전 속도 +20% | 데미지 +20% | 데미지 +20% | 전기선 연결 |

## 통합 정보 (프리팹 / 무기 값)
공통: 프리팹 폴더 `Assets/prefab/weapon/<Name>/`, 렌더러 정렬 레이어 `attack`. 업그레이드 테이블·보상은 기존 무기와 같은 방식(`WeaponUpgradeBuilder`, 가중치 1.5).

| 무기 | Weapon 값 | 발사체 프리팹 구성 |
|---|---|---|
| Vine | Repeat, dmg 6, as 4, range 0, fire 1, hit 1, areaRadius 0.6, lifetime 3 | VineProjectile + BlackBoardHandler + Lifetime + ScaleOverLifetime(useAreaRadius) + SpriteRenderer. LandMovement 없음. thornPrefab = Straight + OneHitProjectile + Lifetime + BlackBoardHandler + 트리거 콜라이더 |
| Spirit | Capped, dmg 8, as 0.5, range 4, fire 3, hit 6 | SpiritProjectile + SpriteRenderer. Lifetime 없음 |
| Root | Repeat, dmg 5, as 0.4, range 6, fire 1, hit 1, areaRadius 1.2, lifetime 4 | RootProjectile(rootSlowRate 0.4) + BlackBoardHandler + Lifetime + ScaleOverLifetime(useAreaRadius) + SpriteRenderer |
| Saw | Wave, dmg 15, as 1, fire 2, speedRate 1, preLoad 4 | 루트 SawProjectile + 자식 스프라이트. 콜라이더·Rigidbody·Lifetime·이동 컴포넌트 없음 |
| Flame | Repeat, dmg 4, as 0.8, range 3, fire 1, knockback ~1, preLoad 2 | 루트 FlameProjectile + 자식 visualRoot(+x 가 분사 방향, 파티클/스프라이트). 콜라이더·Lifetime 없음. FlameBurn 은 런타임 추가 |
| Tornado | Capped, dmg 6, as 0.33, fire 1, lifetime 5, knockback 0 | 루트 TornadoProjectile + 자식 SpriteRenderer(scaleTarget). 콜라이더·Lifetime·이동·ScaleOverLifetime 없음 |
| Lightning | Repeat, dmg 25, as 0.7, range 8, fire 1, areaRadius 0.8 | 루트 LightningProjectile + 자식 Marker(원 SpriteRenderer → markerTemplate), Bolt(LineRenderer → boltTemplate), Link(LineRenderer → linkLine) |

업그레이드 매핑 (Multiply 0.2 = +20%):
- Vine: 데미지 Damage×0.2, 범위 AreaRadius×0.2, 틱 TickInterval×-0.1, 수명 Lifetime×0.5, ★ FlagVineThorn +1
- Spirit: 공속 AttackSpeed×0.15, 추적 ChaseSpeed×0.2, 정령 FireCount +2, 사거리 Range +1.5, 공격 횟수 HitCount +4
- Root: 범위 AreaRadius×0.2, 공속 AttackSpeed×0.15, 지역 FireCount +1, 수명 Lifetime +1, ★ FlagRootChase +1
- Saw: 크기 ProjectileScale×0.2, 속도 SpeedRate×0.2, 톱날 FireCount +2, 공속 AttackSpeed×0.15, ★ FlagSawBounce +1
- Flame: 사거리 Range×0.2, 공속 AttackSpeed×0.15, 각도 FanAngle +30, 분사 BurstDuration +0.3, ★ FlagBurn +1
- Tornado: 크기 ProjectileScale×0.2, 수명 Lifetime +1, 회오리 FireCount +1, 끌어당김 PullForce×0.3, ★ FlagTornadoPermanent +1
- Lightning: 범위 AreaRadius×0.2, 공속 AttackSpeed×0.15, 대상 StrikeCount +2, 사거리 Range +1.5, ★ FlagStormLink +1
