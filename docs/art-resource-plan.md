# 아트 리소스 확보 계획 (초안 2026-11-02, 승인 대기)

## 1. 현재 상태 조사

### 사용 중인 이미지 (`Assets/Resources/`)
| 파일 | 쓰는 곳 |
|---|---|
| `Image/Dummy/DummyCircle.png`, `DummySquer.png` | 발사체·이펙트·UI 대부분의 플레이스홀더 |
| `Textuer/Player/Farmer 0.png` | 플레이어 (TempAnimationContorller / TempPlayerAnimatino) |
| `Textuer/Monster/bat.png` | 박쥐 (bat animator.controller, bat run.anim) — 애니메이션이 있는 유일한 몬스터 |
| `Textuer/Enemy 0~4.png` | 다른 몬스터 정지 스프라이트 |
| `Textuer/TileMap/Past/ForestTileMap.png` | 숲 WFC 타일 (ForstTile_1~24 프리팹, 23개) |
| `Textuer/TileMap/Future/FutuerTile.png` | 공장 WFC 타일 (Factory_1~21 프리팹) |
| `Textuer/Item/Stones_ores_gems_without_grass.png` | 경험치 젬 |
| `Textuer/SweepCrescent.png` | 휩쓸기 무기 |
| `Textuer/fbg.png`, `pbg.png` | 배경 |

### 씬
TitleScene → DimensionSelectScene → GameScene (좌우 분할 화면, 두 차원이 **동시에** 보인다), WeaponTest(디버그)

### 외형 교체 구조 (코드 수정 없이 연결 가능)
- 플레이어: `DimensionData.playerAnimator` (AnimatorOverrideController), `playerSprite`
- 몬스터: `DimensionData.monsterSkins` (MonsterData 키 → AnimatorOverrideController / Sprite). `Spawner.SpawnAt`에서 적용
- 맵: `DimensionData.chunkLoaderPrefab` → `WFCChunkLoader.tileDatas` (TileData 프리팹, 소켓 상하좌우 일치 규칙, 최대 31종). 셀 크기는 첫 타일 스프라이트 크기로 정한다
- 선택 카드: `DimensionData.previewSprite`, `themeColor`
- 보상 카드: `RewardData.image`, `WeaponUpgradeRewardEffect.image` → `SelectPanel.image`
- 체력·경험치 바: `SliderUI.backgroundSprite`, `fillSprite`
- 게임 오버: `GameOverUI` (TMP 텍스트만, 이미지 없음)

### 차원별 몬스터 (웨이브 배분 기준, 외형이 필요한 것만)
| 차원 | 몬스터 |
|---|---|
| D1 숲 | Slime, MiniSlime, bat, Golem |
| D2 공장/사이버 | Bomber, Golem, Slime |
| D3 (폭풍 → 지옥?) | Wraith, Shaman, bat |
→ 몬스터 외형 10세트

### 무기 (10종) — 차원별
| 차원 | 무기 | 필요한 그림 |
|---|---|---|
| D1 | 부메랑, 독장판, 휩쓸기, 분열탄 | 부메랑, 독 웅덩이, 참격 초승달, 탄·파편 |
| D2 | 레이저, 드론, 지뢰 | 빔(가로 늘이기용), 드론 본체·탄, 지뢰·폭발 |
| D3 | 체인 번개, 충격파, 폭발 | 번개 라인 텍스처, 충격파 링, 폭발 |
+ 보상 카드용 아이콘 무기 10개 + 패시브 8개(버프 4·너프 4)

## 2. 필요한 리소스 목록

### 요청 목록
1. 플레이어 3종 × (idle 4f, walk 4f)
2. 몬스터 10세트 × walk 4f
3. WFC 타일 셋 3종
4. 무기 그림 10종 (발사체·이펙트)
5. 체력 바 (플레이어용 + 몬스터 머리 위 작은 바)
6. 경험치 바
7. 타이틀 배경
8. 타이틀 로고
9. 결과 화면 패널 (승리/패배, 통계)

### 조사해서 추가한 항목
10. 경험치 젬 (expGem, 경험치 양에 따라 색 2~3단계)
11. 레벨업 선택 카드 프레임 (Select Panel) + 무기·패시브 아이콘 18개
12. 차원 선택 카드 프레임 + 차원 미리보기 3장 (`previewSprite`)
13. 버튼 (타이틀 시작 버튼, 일반/눌림)
14. 한글 픽셀 폰트 (TMP 폰트 에셋 교체. 현재 NotoSansKR)
15. 데미지 숫자 (FloatingTextUI, 폰트로 해결)
16. 폭발 이펙트 (Bomber 자폭, Explosive, Mine — 하나의 시트로 공유)
17. 피격 이펙트, 사망 이펙트 (현재 Dissolve 셰이더 — 유지하고 작은 파편만 추가)
18. 위험 지대(HazardZone) / 독장판 바닥 텍스처
- 일시정지 UI: `GamePause`는 레벨업 때만 쓰고 일시정지 메뉴는 없다. 메뉴 기능부터 필요하므로 **이번 범위에서 제외** 제안
- 사운드: 요청 범위 밖. 제외

## 3. 확보 계획

### 원칙
- 분할 화면이라 세 차원 중 두 개가 **동시에** 보인다. 차원 간 픽셀 밀도(한 픽셀의 화면 크기)를 맞추는 것이 차원 안 스타일 통일만큼 중요하다.
- 기준은 32×32 / PPU 32. 16px 에셋은 ×2 최근접 확대해서 쓰면 월드 크기는 맞지만 픽셀 덩어리가 2배로 커져 32px 원본 에셋과 섞이면 티가 난다.

### 후보 에셋 (라이선스 확인함)
| 에셋 | 제작자 | 크기 | 라이선스 | 쓸 곳 |
|---|---|---|---|---|
| [Ninja Adventure Asset Pack](https://pixel-boy.itch.io/ninja-adventure-asset-pack) | Pixel-boy & AAA | 16px | CC0 | D1 플레이어·몬스터·타일, 공통 UI·아이콘·FX |
| [Lucifer Lava Dungeon Tileset](https://foozlecc.itch.io/lucifer-lava-dungeon-tileset) | Foozle | 32px 탑다운 | CC0 | D3 타일 |
| [Warped Top-Down Tech Lab](https://opengameart.org/content/warped-top-down-tech-lab) (+ 2, Extension) | Ansimuz (+knekko) | 32px 탑다운 | CC0 | D2 타일 |
| [32x32 Seven Sins Demons](https://coros-games.itch.io/32x32-seven-sins-demons-asset-bundle) | coros (Boris Marcone) | 32px 탑다운, 4방향 walk | CC-BY (상업 가능, 크레딧 필수) | D3 몬스터 |
| [Pixel Healthbars and Hearts](https://unbreaded.itch.io/pixel-health) | unbreaded | - | CC0 | 체력·경험치 바 |
| Galmuri (한글 픽셀 폰트) | quiple | - | SIL OFL 1.1 (상업 가능) | UI 전체 폰트 |

- 모두 무료(Name your own price, 0원 다운로드)다. Ninja Adventure의 "50% 할인" 문구는 후원 가격 표시이고 0원 다운로드 가능 여부는 다운로드 시점에 다시 확인한다. 0원이 안 되면 쓰지 않는다.
- Warped Tech Lab 페이지는 서버가 자동 접근을 막아(403) 상세 내용은 다운로드 때 확인한다.
- D2 사이버 몬스터(Bomber·Golem·Slime 외형)와 D2 플레이어는 같은 화풍의 무료 팩을 못 찾았다 → 직접 생성.

### 직접 생성 (로컬 이미지 생성기 → 32px 축소 → 팔레트 보정 → 외곽선 추가)
- D2 플레이어, D2 몬스터 3종, D3 플레이어
- 타이틀 배경, 타이틀 로고, 결과 화면 패널, 차원 미리보기 3장(게임 화면 캡처로 대체 가능)
- 에셋 팩에 없는 무기 이펙트 (레이저 빔, 체인 번개 텍스처 등은 손으로 그리는 수준의 단순 도형이라 스크립트로 직접 픽셀 생성)
- 한계: 생성 모델은 4프레임 걷기처럼 프레임 간 일관성이 필요한 것을 잘 못 만든다. 정지 1장을 생성하고 프레임은 부위 이동·찌그러뜨리기로 만드는 방식이 될 것이라 팩 에셋보다 움직임이 단순할 수 있다.

## 4. 적용 방법
- 파일명 `[차원]_[종류]_[이름]_[동작].png`, 공통은 `Common_...` → `Assets/Resources/Textuer/` 아래 차원별 폴더
- 임포트: Point, 압축 없음, PPU 32, 시트는 Grid 분할 (에디터 스크립트로 일괄 처리, `Assets/Editor/Builders/Visual/`)
- 애니메이션: 차원·캐릭터별 Clip → 기존 컨트롤러 기반 AnimatorOverrideController → DimensionData에 연결
- 타일: 기존 소켓(Grass/Sand/Base/경계) 방식을 그대로 쓰고 차원별 TileData 프리팹 세트와 chunkLoader 프리팹을 만든다. 차원당 기본 바닥 2~3종 + 경계 전환 12종(변 4·바깥 모서리 4·안쪽 모서리 4) + 장식 몇 종
- Pixel Perfect Camera: 분할 화면(카메라 2개, viewport rect)과 충돌하기 쉬워서 먼저 점검한 뒤 적용 여부를 정한다. 최소한 카메라 orthographicSize를 PPU 배수에 맞추고 스프라이트 위치 흔들림을 확인한다.

## 5. 발견한 문제
- **차원 테마 불일치**: 설계 문서는 숲·공장·폭풍(마력·전격), 요청은 숲·사이버 도시/폐연구소·지옥/용암. D2는 공장 ≈ 사이버/연구소로 맞출 수 있지만 D3 폭풍 ↔ 지옥은 다르다. D3 무기(체인 번개·충격파·폭발)는 지옥 테마에도 쓸 수 있다.
- **bat는 MonsterData가 없어** `monsterSkins`(MonsterData 키)로 차원별 외형을 바꿀 수 없다. bat는 D1·D3 웨이브에 다 나온다 → BatData 추가(코드 0~작은 수정) 또는 공통 외형 하나로 둔다.
- **결과 화면은 승패 구분이 없다** (`GameOverUI` 제목 "게임 오버" 고정). 승리/패배 표시를 하려면 GameOverManager가 승자를 넘겨 주는 코드 수정이 필요하다.
- `.meta` 파일 읽기 권한이 막혀 있어 현재 임포트 설정(PPU 등)은 Unity에서 확인한다.
