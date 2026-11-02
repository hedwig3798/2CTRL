# 차원 선택 설계 (확정 2026-11-02)

## 결정 사항
- 차원 3개(숲·공장·폭풍), 좌우 중복 금지.
- 선택은 별도 씬 `DimensionSelectScene`에서 한다. 씬 순서: Title(0) → DimensionSelect(1) → GameScene(2).
- 조작: 왼쪽은 A/D로 넘기고 Space로 확정, 오른쪽은 ←/→로 넘기고 Enter로 확정. 다시 누르면 확정 취소. 둘 다 확정하면 GameScene을 로드한다. 입력은 new Input System(`Keyboard.current`).
- 상대 진영이 이미 확정한 차원은 고를 수 없다(카드를 건너뛰거나 확정이 막힘).
- 시작 무기: 차원마다 고정 1개. **게임 시작 시 무기 고르기(LevelUpUI grantStartReward / SelectPanelGroup.RefreshStartRewards)는 제거한다.**
- 무기 보상 풀: 차원 전용. 패시브 버프·너프 풀은 공유한다.
- 플레이어 외형: 차원별 AnimatorOverrideController.
- 몬스터 외형: 차원별로 다르다. 침공하는 차원의 외형을 쓴다.
- 아트: 차원 3개의 타일 셋, 플레이어, 몬스터 외형을 이미지 생성으로 만든다(마지막 단계).
- 게임 안에서 설정을 텍스트로 설명하지 않는다. 선택 화면에는 차원 이름과 미리보기만 보여 준다.

## 데이터
`DimensionData` (SO, `Assets/Resources/Data/Dimension/`)
| 필드 | 용도 |
|---|---|
| displayName, previewSprite, themeColor | 선택 카드 |
| Weapon startWeapon | 시작 무기 (PlayerStats.AddWeapon) |
| RewardData[] weaponRewards | 이 차원 플레이어의 무기 풀 (startWeapon 보상은 빼거나 획득 처리) |
| WaveData[] waves | **상대 진영** 스포너에 넣을 웨이브 |
| MonsterSkin[] monsterSkins | (MonsterData, AnimatorOverrideController, Sprite) 이 차원 몬스터 외형 |
| chunkLoaderPrefab (WFCChunkLoader) | 이 진영의 맵 |
| AnimatorOverrideController playerAnimator, Sprite playerSprite | 플레이어 외형 |

`DimensionSelection` (static): `Left`, `Right`. 비어 있으면(GameScene을 직접 실행하면) `DimensionApplier`의 기본값을 쓴다.

`DimensionApplier` (GameScene, 진영마다 1개, Spawner·RewardManager·LevelUp보다 먼저 실행):
1. 자기 진영 플레이어: 애니메이터 오버라이드, 시작 무기.
2. 자기 진영 맵: chunkLoader를 생성하고 카메라를 연결.
3. **반대 진영** 스포너: `SetWaves(차원.waves)`, `SetMonsterSkins(차원.monsterSkins)`. 왼쪽 차원의 몬스터가 오른쪽에 나온다.
4. RewardManager: 자기 진영 Weapon 풀을 차원 무기로 설정.

몬스터 외형은 `Spawner.SpawnAt`에서 적용한다. 분열·소환체도 SpawnAt을 거치므로 같은 차원 외형이 된다. 키는 MonsterData다. 외형이 없으면 프리팹 기본값을 쓴다.

## 무기 배분 (임시, 나중에 바뀔 수 있음)
| 차원 | 테마 | 무기 (첫 번째 = 시작 무기) |
|---|---|---|
| 숲 | 자연·원시 | 부메랑, 독장판, 휩쓸기, 분열탄 |
| 공장 | 기계 | 레이저, 드론, 지뢰 |
| 폭풍 | 마력·전격 | 체인 번개, 충격파, 폭발 |

## 웨이브 배분
몬스터를 테마별로 나눠 차원마다 30분 웨이브 세트(12개)를 만든다.
- 숲: Slime, MiniSlime, bat, Golem
- 공장: Bomber, Golem, Slime
- 폭풍: Wraith, Shaman, bat
Wave30Builder를 차원별 테이블로 바꿔 DimensionData.waves에 연결한다. 스포너 waveArray를 직접 덮어쓰는 코드는 없앤다(MonsterKeywordBuilder 포함).

## 단계
1. 런타임 코드(content-programmer) → 기록 → 리뷰·테스트
2. 통합(unity-integrator): 선택 씬 빌더, DimensionData 3개, 웨이브 빌더, GameScene 적용기, Build Settings
3. 이미지 생성: 타일 셋 3종, 플레이어 3종, 몬스터 외형(차원별)
4. visual-designer: 선택 화면 연출, 아트 연결
