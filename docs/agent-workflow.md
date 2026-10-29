# 서브에이전트 작업 흐름 (상세)

CLAUDE.md의 요약 규칙을 보충하는 문서. 서브에이전트는 이 문서를 매번 읽을 필요 없다.

## 에이전트 역할
| 에이전트 | 모델 | effort | maxTurns | 역할 |
|---|---|---|---|---|
| code-archivist | sonnet | low | 15 | 코드 지도 유지(`.claude/agent-memory/code-archivist/`), 기존 코드 질문 답변 |
| content-programmer | inherit | high | 40 | `Assets/Resources/Script/` 런타임 C# 구현·수정 |
| code-reviewer | opus | high | 20 | 변경분(git diff) 성능·구조 리뷰, 수정 안 함 |
| qa-tester | sonnet | medium | 30 | `Assets/Tests/` EditMode/PlayMode 테스트 작성·실행 |
| unity-integrator | inherit | medium | 30 | `Assets/Editor/Builders/` 빌더로 프리팹·SO·씬 구조 구성 |
| visual-designer | sonnet | medium | 25 | `Assets/Editor/Builders/Visual/` 빌더로 시각·연출 적용 |

## 새 기능 흐름 상세
1. **조사** — code-archivist에게 관련 시스템·API를 묻는다.
2. **구현** — content-programmer. 보고: 파일 목록 / public API / 통합 정보 / 미해결.
3. **기록 갱신** — code-archivist (git diff 기반).
4. **검증** — code-reviewer와 qa-tester를 한 메시지에서 병렬 호출.
5. **수정 루프** — qa-tester의 `[수정 요청]`과 code-reviewer의 "치명적" 항목을 메인이 그대로 content-programmer에 전달.
   경고·제안은 메인이 판단. 최대 2회. 2회 후에도 실패하면 멈추고 사용자에게 보고.
6. **통합** — unity-integrator → (완료 후) visual-designer. 동시 실행 금지.
7. **기록 갱신** — code-archivist.

## 위임 지시문 작성법
- 목표, 대상 파일/폴더, 관련 API(아카이비스트 답변 요약), 완료 조건만 적는다.
- 이전 대화·전체 보고서를 통째로 붙이지 않는다. 수정 요청서는 원문 그대로 전달.

## 직접 처리 기준
- 파일 1~2개 수정의 독립된 작은 작업은 메인이 직접 처리한다(이 경우도 코드 변경 후 archivist 갱신).

## 병렬 실행
- 실제로 서로 독립적인 작업이 3개 이상일 때만 병렬.
- content-programmer 여러 개는 서로 다른 폴더를 맡을 때만.
- 공통 베이스 코드는 병렬 작업 전에 먼저 완성·커밋.
- Unity 배치 모드를 쓰는 에이전트(unity-integrator, qa-tester, visual-designer)는 동시에 하나만.
- 같은 씬/프리팹을 두 에이전트가 동시에 건드리지 않는다.

## Unity 실행 (Unity CLI)
- CLI: `C:/Users/joey3/AppData/Local/Unity/bin/unity.exe` (사용자 PATH, 1.0.0-beta.14). 프로젝트에 `com.unity.pipeline` 0.9.1-exp.1 설치.
- 모든 명령에 `--project-path "E:/2CTRL/2CTRL" --format json --no-banner`. 먼저 `unity status`로 에디터(state "ready") 확인.
- 명령 형식 `unity command <그룹> <이름>` (예: `gameobject find`, `editor save_all`, `script run`, `test run`). 목록·인자는 `unity list`.
- 빌더: `Assets/Editor/Builders/`의 static `Build()`.
  - 연결됨: `unity command script run --file <cs> --entry <Namespace.Type.Build>` (메모리 컴파일, 도메인 리로드 없음).
  - 에디터 없음: `unity run "E:/2CTRL/2CTRL" -- -executeMethod <Namespace.Type.Build> -logFile .claude/tmp/<이름>.log`.
- 테스트: 연결됨 `unity command test run --mode editor|playmode`, 에디터 없음 `unity test --mode EditMode|PlayMode --output .claude/tmp/<파일>.xml`.
- 연결 실패 시 `unity pipeline list`로 Safe Mode(컴파일 에러) 확인.
- 로그·결과는 `.claude/tmp/`(gitignore). `Logs/`, `Temp/`, `Library/`는 읽기 차단.

## 공유 파일
- 레지스트리·매니저·공통 베이스(`Projectile`, `RewardEffect`, `IDamageable`, `IPoolable`, `Initializable`,
  `EventManager`, `RewardManager`, `BlackBoard`)는 서브에이전트가 고치지 않고 보고만 하며, 메인이 마지막에 직접 수정.

## Unity MCP를 쓰지 않는 이유
- 공식 AI Assistant MCP는 deprecated(2.18 문서, Unity CLI 권장).
- CLI 내장 `unity mcp`(beta.14)는 도구 166개 중 56개가 그룹 접두사 없이 이름이 겹치고(find, create, run 등),
  도구 정의만 약 100KB라 호출마다 토큰 부담이 크다. CLI `unity command <그룹> <이름>`이 명확하고 저렴하다.
- 나중에 도입한다면 전역(.mcp.json)이 아니라 unity-integrator·visual-designer frontmatter `mcpServers`에만 인라인 등록한다.
