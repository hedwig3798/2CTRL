# 2CTRL

Unity 6000.6.7f1 / URP 2D Renderer / uGUI + TMP / new Input System (`Keyboard.current`) / 2D 물리 / DOTS 미사용.
Unity 제어: Unity CLI(`unity`, Pipeline 패키지). 열린 에디터는 `unity command`, 없으면 `unity run`/`unity test`. Unity MCP는 쓰지 않는다.

## 폴더
- 런타임 `Assets/Resources/Script/` (asmdef `Game.Runtime`)
- 에디터 `Assets/Editor/` (`Game.Editor`), 빌더 `Assets/Editor/Builders/`(시각: `Visual/`)
- 테스트 `Assets/Tests/EditMode`, `Assets/Tests/PlayMode` (`Game.Tests.*`)
- 프리팹 `Assets/prefab/`, 씬 `Assets/Scenes/`, 텍스처 `Assets/Resources/Textuer/`
- 인코딩 UTF-8, 커밋 메시지 한국어. Unity 로그·테스트 결과는 `.claude/tmp/`.

## 서브에이전트 작업 흐름
상세: [docs/agent-workflow.md](docs/agent-workflow.md)

1. 새 기능 순서: 조사(code-archivist) → 구현(content-programmer) → 기록 갱신(code-archivist)
   → 리뷰(code-reviewer)·테스트(qa-tester) 병렬 → 문제 시 content-programmer 수정 → 재리뷰·재테스트
   → 통과 후 unity-integrator → visual-designer → 기록 갱신(code-archivist)
2. 수정 루프 최대 2회. 2회 후에도 실패하면 멈추고 사용자에게 보고.
3. qa-tester 수정 요청서와 code-reviewer "치명적" 항목은 메인이 그대로 content-programmer에 전달.
4. 코드가 추가·변경될 때마다 code-archivist로 기록 갱신.
5. 위임 지시문은 필요한 정보만 짧게. 이전 대화를 통째로 전달하지 않는다.
6. 독립된 작은 작업(파일 1~2개 수정)은 서브에이전트 없이 메인이 직접 처리.
7. 병렬 실행:
   - 실제로 독립적인 작업이 3개 이상일 때만.
   - content-programmer 여러 개는 서로 다른 폴더를 맡을 때만.
   - 공통 베이스 코드는 병렬 작업 전에 먼저 완성·커밋.
   - unity-integrator와 visual-designer는 절대 동시 실행 금지. 같은 씬/프리팹 동시 수정 금지.
   - Unity 배치 모드 에이전트(unity-integrator, qa-tester, visual-designer)는 동시에 하나만.
8. 공유 파일(레지스트리, 매니저, 공통 베이스)은 메인이 마지막에 직접 수정.

# 모든 에이전트 작업 규칙
1. 정보 없이 코딩하지 말 것. 먼저 묻고, 같이 계획하고, 구현한다.
2. 무조건 동의하지 말 것. 문제와 대안을 먼저 검토한다.
3. 기본 테스트만으로 "완료"라고 하지 말 것. 직접 실행해서 화면을 보고, 사람이 플레이하기 전까지 끝이 아니다.
4. 같은 실수를 반복하지 말 것. 실수는 여기에 한줄씩 적는다.
   - 발사체·이펙트 렌더러를 Default 정렬 레이어로 두지 말 것 (BG 보다 먼저 그려져 GameScene 바닥에 가려짐). 무기는 `attack` 레이어.
   - 아트는 `Builders.Visual.ArtResourceBuilder.Build()` 로 적용한다. 다른 씬·프리팹 빌더를 다시 돌린 뒤에는 이것도 다시 실행 (안 하면 아트가 덮어써짐).
   - internal 헬퍼(예: `SettingsUIKit`)를 쓰는 빌더는 `script run` 이 CS0122 로 실패한다. `unity command script eval "return Builders.X.Build();"` 로 실행.
   - 무기 프리팹을 다시 만드는 빌더를 돌린 뒤에는 `Builders.WeaponTagBuilder.Build()` 도 다시 실행 (안 하면 무기 태그가 All 로 돌아감).
   - 런타임에 Instantiate 하는 연출·오브젝트는 낸 쪽의 레이어로 맞출 것 (분할 카메라 culling mask 에 Default 가 없어 안 보임). `EffectPool.Spawn` 은 레이어 인자 필수.
   - `capture game_view --save_path` 는 절대 경로를 줘도 `Assets/` 아래에 저장된다. 캡처 후 `.claude/tmp/` 로 옮기고 지울 것. Game 뷰가 Free Aspect 면 캡처가 찌그러진다.