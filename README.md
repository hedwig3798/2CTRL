# 2CTRL

> 한 사람이 두 캐릭터를 동시에 조종하는 2D 분할 화면 서바이벌 게임

- 버전: 0.0.2
- 엔진: Unity 6000.6.7f1 (URP 2D Renderer)
- UI: uGUI + TextMeshPro / 입력: new Input System / 물리: 2D 물리

---

## 1. 게임 소개

**2CTRL**("two control")은 화면을 좌우로 나눠 **한 명의 플레이어가 두 캐릭터를 동시에 조종**하는 탑다운 서바이벌 게임입니다.
왼손은 `WASD`로 왼쪽 캐릭터를, 오른손은 방향키로 오른쪽 캐릭터를 움직입니다. 공격은 자동이므로 두 캐릭터를 몬스터에게서 지키는 데 집중하면 됩니다.

- 시작 전에 두 진영이 각각 **차원**(숲·공장·폭풍)을 하나씩 고릅니다. 같은 차원은 고를 수 없습니다.
- 내가 고른 차원의 몬스터는 **상대 진영**을 침공합니다. 왼쪽이 숲을 고르면 숲 몬스터가 오른쪽 화면에 나옵니다.
- 경험치는 두 캐릭터가 함께 쌓습니다. 레벨이 오르면 양쪽이 각자 보상을 고릅니다.
- 약 30분 동안 이어지는 14개 웨이브를 **두 캐릭터 모두 살아서** 버티면 승리합니다. 한쪽이라도 죽으면 게임 오버입니다.

### 조작

| 동작 | 왼쪽 진영 | 오른쪽 진영 |
|---|---|---|
| 이동 | `W` `A` `S` `D` | `↑` `←` `↓` `→` |
| 차원 넘기기 (차원 선택) | `A` / `D` | `←` / `→` |
| 차원 확정·취소 (차원 선택) | `Space` | `Enter` |
| 레벨업 보상 선택 | 마우스 클릭 | 마우스 클릭 |
| 일시정지 메뉴 | `ESC` | |

개발 빌드(에디터·Development Build) 전용 단축키: `F1` 스탯 패널, `F2` 웨이브 건너뛰기, `F3` 즉시 클리어.

---

## 2. 핵심 메커니즘

### 2.1 두 캐릭터 동시 조종
- 좌우 진영은 물리 레이어로 완전히 분리됩니다 (`LPlayer`↔`LMonster`, `RPlayer`↔`RMonster`). 한쪽 몬스터는 다른 쪽 캐릭터를 공격하지 않습니다.
- 진영마다 카메라와 WFC로 만든 맵이 따로 있고, 화면은 16:9 레터박스 안에서 좌우로 나뉩니다.
- 한쪽만 잘 키워서는 이길 수 없습니다. 두 캐릭터의 성장과 생존을 함께 관리해야 합니다.

### 2.2 차원 교차 침공
- 차원은 **시작 무기, 무기 보상 풀, 캐릭터 외형, 맵 타일**을 내 진영에 정하고, **웨이브와 몬스터 외형**은 상대 진영에 정합니다.
- 분열·소환으로 생긴 몬스터도 침공한 차원의 외형을 따릅니다.

| 차원 | 시작 무기 | 무기 풀 | 침공 몬스터 |
|---|---|---|---|
| 숲 | 부메랑 | 부메랑, 독장판, 휩쓸기, 분열탄, 덩굴, 정령, 뿌리 속박 | 박쥐, 슬라임, 미니 슬라임, 골렘 |
| 공장 | 레이저 | 레이저, 드론, 지뢰, 톱날, 화염 방사 | 슬라임, 미니 슬라임, 봄버, 골렘 |
| 폭풍 | 체인 번개 | 체인 번개, 충격파, 폭발, 회오리, 낙뢰 | 박쥐, 레이스, 샤먼 |

### 2.3 레벨업: 나는 강해지고, 상대는 약해진다
- 몬스터를 잡으면 경험치 보석이 떨어집니다. 레벨업하면 게임이 멈추고, 두 진영이 각자 선택지 3개 중 하나를 고릅니다.
- 선택지 종류
  - **무기 획득**: 내 차원의 무기 풀에서 새 무기 (최대 6개, 시작 무기 포함)
  - **무기 업그레이드**: 보유 무기 강화 (10단계, 5·10단계는 ★ 특수 효과)
  - **패시브 버프**: 내 스탯 상승. **상대 진영에 디버프 1개가 함께 붙습니다.**
- 패시브는 버프가 너프보다 항상 크게 설계되어 있습니다 (예: 데미지 +5% / 상대 −3%). 그래서 패시브를 고를 때는 "내가 얻는 것"과 "다른 쪽이 잃는 것"을 같이 따져야 합니다.
- 스탯 배율은 최소 0.5배 아래로 내려가지 않습니다.

### 2.4 30분 웨이브
- 14개 웨이브: 처음 3개는 50초, 나머지는 150초. 웨이브마다 몬스터 수, 체력·데미지·속도 배율이 올라갑니다.
- 20:00 웨이브는 약한 몬스터가 대량으로 나오는 "숨 고르기" 구간입니다. 경험치를 몰아 주어 마지막 10분 전에 빌드를 완성하게 합니다.
- 최종 레벨은 약 50 (필요 경험치 `10 + 20 × L^1.2`, 최대 99).
- 수치 근거와 시뮬레이션은 [docs/balance-design.md](docs/balance-design.md)에 있습니다.

---

## 3. 핵심 시스템

코드는 `Assets/Resources/Script/` 아래에 있습니다 (asmdef `Game.Runtime`).

| 시스템 | 폴더 | 내용 |
|---|---|---|
| 초기화 / 블랙보드 | `InitializeSystem/` | `BlackBoard`에 생성 정보(대상, 배율, 스포너 등)를 담아 `Initializable` 컴포넌트에 한 번에 전달. 풀링된 오브젝트 재사용 시 상태 초기화 |
| 이벤트 | `EventSystem/` | `EventManager` 전역 이벤트 (레벨업, 적 처치, 피격, 사망, 경험치 획득, 무기 데미지) |
| 스폰 / 풀링 | `SpawnSystem/` | `Spawner`가 `WaveData`(SO)대로 몬스터를 풀에서 꺼내 화면 밖에 생성. 웨이브 배율, 차원별 외형 적용, 남은 시간 API |
| 몬스터 | `MonsterSystem/` | `MonsterBrain` 상태 머신(이동→예고→행동→다운→사망)과 **키워드 조합**(SO). 키워드 10종: 돌진, 공전, 예측 이동, 순간이동, 소환, 자폭, 장판 남기기, 넉백 저항, 부활, 분열 |
| 전투 / 데미지 | `ComabtSystem/` | `DamagePipeline`이 무적→회피→고정→방어→보호막→체력→넉백 순서로 데미지 처리. `Weapon`(발사 방식 Repeat / Persistent / Capped / Wave)과 `Projectile` 파생 클래스 |
| 무기 | `Weapons/` | 17종: 부메랑, 체인 번개, 드론, 폭발, 레이저, 지뢰, 독장판, 충격파, 분열탄, 휩쓸기, 덩굴, 정령, 뿌리 속박, 톱날, 화염 방사, 회오리, 낙뢰 |
| 무기 업그레이드 | `WeaponUpgradeSystem/` | `WeaponUpgradeTable`(10단계)과 `WeaponParam` 수정치(합 / 배율 / ★ 플래그). 발사체가 발사 시점에 수치를 읽음 |
| 이동 | `MoveSystem/` | 플레이어 키 입력 이동, 추적, 직진, 궤도, 체인, 귀환 등 이동 컴포넌트 |
| 스탯 | `StatSystem/` | `PlayerStats`(진영별): 데미지, 공속, 사거리, 이동 속도, 체력, 체력 재생, 지속 시간, 투사체 수. 무기 태그(`WeaponStatTag`)로 무기마다 받는 스탯을 제한 |
| 보상 | `RewardSystem/` | `RewardManager`가 진영별 보상 풀(무기 / 업그레이드 / 버프 / 너프)을 가중치로 관리. 효과는 `RewardEffect` SO 파생으로 확장 |
| 경험치 / 아이템 | `ExpSystem/`, `ItemDropManager/` | 두 진영이 공유하는 `ExpSystem`, `LevelTable` 공식, 보석 드롭·수집 |
| 차원 | `DimensionSystem/` | `DimensionData`(SO), 씬 사이 선택 전달(`DimensionSelection`), GameScene에서 진영별 적용(`DimensionApplier`) |
| 맵 | `Tile/` | `WFCChunkLoader`: 소켓 규칙 기반 WFC(Wave Function Collapse)로 카메라 주변 청크를 무한 생성 |
| 통계 / 게임 종료 | `StatisticsSystem/` | 처치 수, 받은 피해, 경험치, 무기별 데미지 집계(프레임당 처리 수 제한 큐). `GameOverManager`가 승리·패배 판정 |
| 설정 | `Settings/` | `GameSettings`(PlayerPrefs): 화면 모드, 적 체력 바·데미지 텍스트 표시, 채널별 볼륨, 언어. 16:9 레터박스 |
| 다국어 | `Localization/` | `StringTable.csv`(한국어 / 영어) + `LocalizedText` |
| UI | `UI/` | 타이틀, 차원 선택, 레벨업 패널, HUD(웨이브 타이머, 무기 슬롯), 피격 비네트, 일시정지·설정 창, 결과 화면, 디버그 도구 |
| 연출 | `EffectSystem/` | 디졸브, 크기·투명도 변화, 풀링 이펙트 |

### 에디터 빌더
씬, 프리팹, SO 데이터는 `Assets/Editor/Builders/`의 빌더 코드로 만듭니다 (메뉴 `Tools/Builders/*`). 시각 요소는 `Builders/Visual/`의 빌더가 덮어씁니다.
아트는 `Builders.Visual.ArtResourceBuilder.Build()`로 적용하며, 다른 빌더를 실행한 뒤에는 이 빌더를 다시 실행해야 합니다.

### 테스트
- EditMode: `Assets/Tests/EditMode` (`Game.Tests.EditMode`)
- PlayMode: `Assets/Tests/PlayMode` (`Game.Tests.PlayMode`)

---

## 4. 씬

빌드 순서는 Title → DimensionSelect → Game 입니다.

| # | 씬 | 설명 |
|---|---|---|
| 0 | `TitleScene` | 타이틀 로고와 세 차원 배경. **게임 시작**(차원 선택으로 이동), **설정** 버튼. 설정 창은 `ESC`로 닫음 |
| 1 | `DimensionSelectScene` | 좌우 진영이 동시에 차원 카드를 넘기고 확정. 상대가 확정한 차원은 잠김. 둘 다 확정하면 GameScene을 불러옴. 화면에는 차원 이름과 미리보기만 표시 |
| 2 | `GameScene` | 본 게임. 진영마다 플레이어, 카메라, `DimensionApplier`, 상대 진영을 노리는 `Spawner`. 공용으로 `ExpSystem`, `RewardManager`, 레벨업 UI, HUD, 일시정지 메뉴, 통계·결과 화면 |
| – | `WeaponTest` | 개발용 (빌드 제외). 무기를 골라 허수아비에게 쏘고 DPS 미터로 기본 DPS와 업그레이드 배율을 측정 |

GameScene을 에디터에서 바로 실행하면 각 `DimensionApplier`의 기본 차원으로 시작합니다.

### 결과 화면
게임이 끝나면 시간이 멈추고 진영별 통계(처치 수, 받은 피해, 경험치, 무기별 데미지 아이콘·수치)를 보여 줍니다. 패배했을 때는 먼저 쓰러진 쪽을 표시합니다.

---

## 5. 사용 에셋과 라이선스

### 이 프로젝트의 라이선스
이 저장소에서 직접 만든 **코드와 리소스는 [CC0 1.0 Universal](https://creativecommons.org/publicdomain/zero/1.0/deed.ko)** 로 공개합니다.
저작권을 포기하므로 누구나 출처 표시 없이 복사, 수정, 배포, 상업적 이용을 할 수 있습니다.

CC0가 적용되는 것:
- `Assets/Resources/Script/`, `Assets/Editor/`, `Assets/Tests/`의 모든 코드
- `tools/art/*.py` 스크립트와 그것으로 생성한 이미지: 공장 바닥 강철 패널·환풍구·경고 줄무늬·조명, 폭풍 바닥 번개 그을음·균열, 레이저 빔, 체인 번개 라인, 지뢰, 독 웅덩이, 위험 지대, 게이지 테두리·채움, 타이틀 로고, 타이틀 배경, 차원 선택 미리보기
- 씬, 프리팹, ScriptableObject 데이터, 문자열 테이블, `docs/` 문서

### 외부 에셋
외부 에셋은 각자의 라이선스를 따릅니다. 아래 에셋은 CC0가 아닌 것도 있으니 재사용할 때 확인하세요.

| 에셋 | 제작자 | 출처 | 라이선스 | 사용한 곳 |
|---|---|---|---|---|
| Ninja Adventure - Asset Pack (2026-03-29 판) | Pixel-boy, AAA | <https://pixel-boy.itch.io/ninja-adventure-asset-pack> | CC0 1.0 | 플레이어·몬스터 스프라이트, 바닥 타일 원본, 무기·이펙트, 경험치 보석, 보상 아이콘, UI 패널·버튼·게이지 |
| Galmuri (v2.40.4, Galmuri11) | quiple (Minseo Lee) | <https://github.com/quiple/galmuri> | SIL Open Font License 1.1 | 게임 전체 한글 픽셀 폰트 |
| Noto Sans KR | Google | <https://fonts.google.com/noto/specimen/Noto+Sans+KR> | SIL Open Font License 1.1 | Galmuri에 없는 글자의 대체 폰트 |
| LiberationSans (TextMesh Pro 기본 폰트) | Red Hat / Unity | Unity TextMesh Pro 패키지 | SIL Open Font License 1.1 | TMP 기본 폴백 |

- 라이선스 원문: `Assets/Resources/Textuer/Art/NinjaAdventure_LICENSE.txt`, `Assets/Fonts/Galmuri/OFL.txt`, `Assets/TextMesh Pro/Fonts/LiberationSans - OFL.txt`
- Ninja Adventure 원본을 잘라 붙이거나 색을 바꾼 이미지(공장·폭풍 바닥, 어두운 패널, 너프 아이콘 등)도 CC0 원본의 수정본이므로 CC0입니다.
- OFL 폰트는 게임에 포함해 배포할 수 있지만 **폰트 파일만 따로 판매할 수 없고**, 수정한 폰트는 원래 이름을 쓸 수 없습니다. 이 폰트들에는 CC0가 적용되지 않습니다.
- Unity 엔진과 Unity 패키지(URP, Input System, TextMesh Pro 등)는 Unity 이용 약관을 따릅니다.
- 현재 게임에는 사운드 에셋이 없습니다.

자세한 출처는 [CREDITS.md](CREDITS.md)를 참고하세요.

---

## 폴더 구조

```
Assets/
  Resources/Script/   런타임 코드 (Game.Runtime)
  Resources/Data/     차원, 몬스터, 키워드 SO 데이터
  Resources/Textuer/  텍스처 (Art/ = 외부 에셋 가공본)
  Editor/             에디터 코드, Builders/ 씬·프리팹 빌더 (Game.Editor)
  Tests/              EditMode / PlayMode 테스트
  prefab/             프리팹 (Player, Monster, weapon, UI, TileMap, Managers)
  Scenes/             TitleScene, DimensionSelectScene, GameScene, WeaponTest
docs/                 설계 문서와 밸런스 계산 스크립트
tools/art/            아트 생성 스크립트
```
