# move (MoveSystem)
경로: `Assets/Resources/Script/MoveSystem/`
- 모두 MonoBehaviour + Initializable(BlackBoard에서 moveSpeedRate/moveTarget/moveTargetPoint 등 읽음). 클래스명이 파일명과 다른 경우: `StraightMovement.cs`=Straight, `FollowMovement.cs`=Follow, `ArrowMovemnt.cs`(오타)=ArrowMovemnt.
- 목록: Straight, Chasing(sealed), Chain(chainCount/chainRange), Return(신규), Slingshot, Orbit, Follow, ArrowMovemnt.
- `SPRITE_ROTATE_MODE` enum (SpriteRotateMode.cs).
- 추가 무기 이동은 Initializable 구현 후 프리팹에 부착.
- MoveAnimator (2026-11-02, 신규): target(기본 root) 위치 변화로 Animator bool `IsMoving` 설정. 컨트롤러에 파라미터 없으면 무시. 플레이어 visual 에 부착.
- ArrowMovemnt.LastMoveDirection (2026-11-03): 마지막 이동 방향(정규화, 정지 시 유지, 초기 right). FlameProjectile 이 부채꼴 방향으로 사용.
- Follow.offset(Vector2): 대상 위치 + offset 으로 따라감. GameScene 분할 카메라는 ArtResourceBuilder 가 좌 -1.2 / 우 +1.2 로 설정(플레이어가 화면 가운데 경계 쪽에 보이게). 값은 `CAMERA_CENTER_BIAS`, 스폰 반경 11~13 때문에 크게 올리면 바깥 모서리에서 스폰이 보임.
- 아트 visual 스케일(판정 무관)은 ArtResourceBuilder 상수: PLAYER 1.3 / MONSTER 1.15 / WEAPON 1.15(개체형 탄만) / EXP_GEM 0.4.
