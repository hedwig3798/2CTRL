# 디버그 기능 (Assets/Resources/Script/UI/Debug/)
- 모든 디버그 코드는 `#if UNITY_EDITOR || DEVELOPMENT_BUILD` 로 감싼다 (릴리스 빌드 제외). 새 디버그 코드도 같은 기호.
- `PlayerStatDebugUI` : F1, IMGUI 스텟 패널. AfterSceneLoad 자동 생성.
- `GameDebugCommands` : F2 웨이브 스킵(모든 Spawner.DebugSkipWave), F3 즉시 클리어(GameOverManager.TriggerVictory). GamePause.IsPaused 중엔 키 무시. static SkipWave()/Clear() 는 eval 로 직접 호출 가능. AfterSceneLoad 자동 생성.
- `WeaponTest/*` : WeaponTest 씬 전용(빌드 씬 아님) → [weapon-test.md](weapon-test.md).
