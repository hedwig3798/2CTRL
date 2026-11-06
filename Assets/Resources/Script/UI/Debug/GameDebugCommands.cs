#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 디버그 전용 : 게임 진행 단축키
/// - skipWaveKey(기본 F2) : 모든 Spawner 의 현재 웨이브를 건너뛴다 (마지막 웨이브면 웨이브 종료 → 승리)
/// - clearKey(기본 F3) : 즉시 클리어 (GameOverManager.TriggerVictory)
/// - 에디터 / 개발 빌드(Development Build)에서만 컴파일된다. 릴리스 빌드에는 포함되지 않는다
/// - 씬 로드 시 자동으로 생성되므로 씬에 배치할 필요가 없다
/// - 일시정지 중(보상 선택, 설정 창, 게임 오버)에는 무시한다
/// </summary>
public class GameDebugCommands
    : MonoBehaviour
{
    [Tooltip("현재 웨이브를 건너뛰는 키")]
    [SerializeField]
    private Key skipWaveKey = Key.F2;

    [Tooltip("즉시 클리어하는 키")]
    [SerializeField]
    private Key clearKey = Key.F3;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateInstance()
    {
        if (null != FindAnyObjectByType<GameDebugCommands>())
        {
            return;
        }

        GameObject go = new GameObject(nameof(GameDebugCommands));
        DontDestroyOnLoad(go);
        go.AddComponent<GameDebugCommands>();
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (null == keyboard || true == GamePause.IsPaused)
        {
            return;
        }

        if (Key.None != skipWaveKey && true == keyboard[skipWaveKey].wasPressedThisFrame)
        {
            SkipWave();
        }

        if (Key.None != clearKey && true == keyboard[clearKey].wasPressedThisFrame)
        {
            Clear();
        }
    }

    /// <summary>
    /// 모든 Spawner 의 현재 웨이브를 건너뛴다
    /// </summary>
    public static void SkipWave()
    {
        Spawner[] spawners = FindObjectsByType<Spawner>();
        int count = 0;
        foreach (Spawner spawner in spawners)
        {
            if (true == spawner.DebugSkipWave())
            {
                count++;
                Debug.Log($"[Debug] 웨이브 스킵 : {spawner.name} (웨이브 {spawner.CurrentWaveIndex + 1})", spawner);
            }
        }

        if (0 == count)
        {
            Debug.Log("[Debug] 웨이브 스킵 : 진행 중인 Spawner 가 없습니다.");
        }
    }

    /// <summary>
    /// 즉시 클리어한다
    /// </summary>
    public static void Clear()
    {
        GameOverManager manager = FindAnyObjectByType<GameOverManager>();
        if (null == manager)
        {
            Debug.Log("[Debug] 클리어 : 씬에 GameOverManager 가 없습니다.");
            return;
        }

        Debug.Log("[Debug] 클리어", manager);
        manager.TriggerVictory();
    }
}
#endif
