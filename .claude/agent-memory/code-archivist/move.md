# move (MoveSystem)
경로: `Assets/Resources/Script/MoveSystem/`
- 모두 MonoBehaviour + Initializable(BlackBoard에서 moveSpeedRate/moveTarget/moveTargetPoint 등 읽음). 클래스명이 파일명과 다른 경우: `StraightMovement.cs`=Straight, `FollowMovement.cs`=Follow, `ArrowMovemnt.cs`(오타)=ArrowMovemnt.
- 목록: Straight, Chasing(sealed), Chain(chainCount/chainRange), Return(신규), Slingshot, Orbit, Follow, ArrowMovemnt.
- `SPRITE_ROTATE_MODE` enum (SpriteRotateMode.cs).
- 추가 무기 이동은 Initializable 구현 후 프리팹에 부착.
