# 아트 리소스 작업 보고서 (2026-11-02)

계획: [art-resource-plan.md](art-resource-plan.md) · 출처: [../CREDITS.md](../CREDITS.md)

## 요약
- 모든 캐릭터·몬스터·타일·이펙트·아이콘·UI 를 **Ninja Adventure (CC0) 한 팩**에서 가져왔다. 계획 단계 후보였던 Warped Tech Lab / Lucifer Lava / Seven Sins 는 쓰지 않았다.
  - 이유: 분할 화면에 두 차원이 동시에 보여서 차원 간 화풍·픽셀 밀도를 맞추는 게 더 중요했고, D3 를 폭풍으로 유지하면서 폭풍 테마 무료 팩을 찾지 못했다. Ninja Adventure 에 로봇·번개 닌자·주술사·유령이 있어 세 차원을 모두 채울 수 있었다.
- 한글 픽셀 폰트 Galmuri11 (OFL) 적용.
- 이미지 생성 AI 는 쓰지 않았다. 팩에 없는 것은 파이썬 스크립트로 픽셀을 직접 그렸다 (`tools/art/`).

## 크기·밀도 결정
- 모든 스프라이트: PPU 32, Point, 무압축. 파일은 32×32 칸.
- 원본 16px 그림을 **확대하지 않고** 32×32 캔버스 가운데에 둔다 → 1 원본 픽셀 = 1/32 유닛. 캐릭터는 약 0.5 유닛으로 기존 판정 크기(콜라이더 반지름 0.16~0.2)와 비슷하다.
- 타일은 16px 조각 2×2 를 붙여 32px(1 유닛) 칸으로 만들어 캐릭터와 같은 밀도다. 기존 타일 칸(0.8~0.96 유닛)과 비슷해 맵 스케일은 거의 그대로다.
- Pixel Perfect Camera 는 **넣지 않았다**. 카메라 2대가 viewport rect 로 화면을 나누는 구조와 충돌하고, 화면 1080p 기준 1 원본 픽셀이 약 2.1 화면 픽셀이라 비정수 배율 때문에 픽셀 두께가 미세하게 고르지 않을 수 있다. 흐림(블러)은 없다 (Point 필터).

## 적용한 리소스

### 차원별
| | D1 숲 | D2 공장/사이버 | D3 폭풍 |
|---|---|---|---|
| 플레이어 (idle 4f / walk 4f) | Hunter | RobotGrey | NinjaThunder |
| Slime / MiniSlime | Slime(파랑) | Slime2(녹색 슬러지) | – |
| Golem | Bear | RobotGreen | – |
| Bomber | – | Eye2(붉은 눈) | – |
| Wraith / Shaman | – | – | Spirit / Shaman |
| bat | BlueBat (공통) | | |
| 바닥 WFC 타일 19종 | 풀 + 흙 | 강철 패널 + 부서진 바닥 (재색칠 + 직접 그림) | 보라 암반 + 빗물 웅덩이 (재색칠 + 번개 그을음) |
| 무기 | 부메랑(수리검), 분열탄(바위), 휩쓸기(참격), 독장판(웅덩이) | 레이저(빔), 드론(감시 로봇), 지뢰 | 체인 번개(라인 텍스처+스파크), 충격파(고리), 폭발(마력 구) |

- 몬스터 외형은 `DimensionData.monsterSkins` 로 차원별로 바뀐다 (침공하는 차원 외형). 프리팹 기본값은 각 몬스터의 원래 차원 외형.
- 타일은 `Assets/prefab/TileMap/Art/D*/` 프리팹과 `Assets/prefab/Managers/ChunkLoader_D1~3.prefab` 으로 새로 만들었다. 기존 chunkLoader / ForestTile / FactoryTile 은 건드리지 않았다.

### 공통
- 경험치 젬(노란 보석), 체력 바(머리 위 미니 바), 경험치 바(테두리+채움), 레벨업 카드 패널·선택 테두리, 버튼(일반/호버/눌림), 보상 아이콘 20종(무기 10, 버프 5, 너프 5 = 버프 아이콘 회색 + 하향 화살표), 폭발·피격 이펙트
- 타이틀 배경(세 차원 대각선 분할 + 캐릭터), 타이틀 로고, 차원 선택 카드·미리보기, 결과 화면 카드 패널

## 코드 변경
- `MoveSystem/MoveAnimator.cs` (신규): 위치 변화로 Animator `IsMoving` 을 켜고 끈다 → 플레이어 idle/walk 전환. 이동 스크립트는 수정하지 않음.
- `GameOverUI.Show(stats, deadSide)` 추가: 제목 "○○ 플레이어 승리!", 각 카드에 [승리]/[패배]. 기존 `Show(stats)` 는 그대로 동작. `GameOverManager` 가 새 오버로드를 호출.
- `Editor/Builders/Visual/ArtResourceBuilder.cs` (신규, 멱등): 임포트·슬라이스·애니메이션·타일·프리팹·씬 적용 전체. `Game.Editor.asmdef` 에 `Unity.2D.Sprite.Editor` 참조 추가.
  - 다시 적용: `unity command script eval --code 'return Builders.Visual.ArtResourceBuilder.Build();'`
- 이미지 재생성: `tools/art/tiles.py`, `actors.py`, `fx.py` (Ninja Adventure 압축 해제 경로를 인자로 받음)
- 추가 패시브 아이콘 7종: `tools/art/passive_icons.py <pack_root> Assets/Resources/Textuer/Art/Icon` 후 `ArtResourceBuilder.Build()`

## 확인한 것
- 플레이 모드 캡처: GameScene(숲 vs 공장, 폭풍 vs 공장, 숲 vs 폭풍), 타이틀, 차원 선택, 레벨업 선택창, 게임 오버(승패 표시).
- WFC 생성 정상 (경고 없음), 몬스터 차원 외형 교체 정상, 레벨업 아이콘·폰트 정상.
- EditMode 테스트 125/125 통과. PlayMode 테스트는 0개로 잡혔다.

## 부족하거나 아쉬운 점
1. **캐릭터가 작다**: 약 0.5 유닛(1080p 에서 약 34px). 판정과는 맞지만 화면에서 작게 느껴질 수 있다. 키우려면 카메라 orthographicSize(현재 8)를 줄이는 게 가장 깔끔하다 (밀도 유지). 게임플레이 시야가 바뀌므로 결정 필요.
2. **크게 늘어나는 이펙트의 픽셀이 커진다**: 충격파 고리·레이저·폭발·독장판은 기존 스케일 로직(반경에 맞춰 확대)을 그대로 쓰므로 범위가 커지면 픽셀 덩어리가 굵어진다.
3. **무기 이펙트 전부를 눈으로 확인하지 못했다**: WeaponTest 씬 자동 캡처에서 더미가 나오지 않아 수리검·드론·지뢰·충격파만 확인했다. 레이저·체인 번개·휩쓸기·분열탄·폭발은 사람이 플레이하며 확인 필요.
4. **bat 은 차원별 외형이 없다** (MonsterData 가 없어 스킨 키가 없음). 공통 BlueBat 으로 통일.
5. **D2/D3 바닥은 원본 재색칠 + 직접 그린 장식**이라 D1 보다 덜 다듬어졌다. 특히 D2 "부서진 바닥"(B 영역) 경계는 풀 테두리를 재색칠한 것이라 형태가 유기적이다.
6. 몬스터 걷기는 원본 시트의 왼쪽 방향 4프레임을 쓰고 좌우는 기존 flipX 로 뒤집는다. 위/아래 방향 그림은 쓰지 않는다. 플레이어 idle 은 원본에 idle 이 없는 캐릭터가 있어 걷기 첫 프레임에 1px 숨쉬기를 넣어 만들었다.
7. 갈무리 폰트는 12 배수 크기에서 선명해서 TMP 크기를 12 배수로 반올림했다 (예: 200→204, 40→36, 16→12). 레이아웃이 약간 달라졌을 수 있다. 일부 숫자(1 등)는 갈무리 디자인이 원래 세리프 모양이다.
8. 레벨업 창 버튼의 기존 문구 "Reaume" 오타는 그대로 두었다 (텍스트 수정은 범위 밖).
9. 일시정지 메뉴, 사운드는 범위에서 제외.
10. `TitleSceneBuilder` / `DimensionSelectSceneBuilder` 등 기존 씬 빌더를 다시 돌리면 이번 아트가 덮어써진다 → 그 뒤 `ArtResourceBuilder.Build()` 를 다시 실행해야 한다.
11. 에디터 Game 뷰가 Free Aspect(약 1.23:1)라 캡처가 찌그러져 보였다. 실제 비율 확인은 16:9 로 맞춰서 해 볼 것.
