using System;
using UnityEngine;

/// <summary>
/// 전역 이벤트를 보관하는 매니져
/// - 이벤트는 여기서 선언하고, 발행하는 쪽은 직접 Invoke, 구독하는 쪽은 += / -= 로 듣는다.
/// - 씬에 미리 배치하면 그것을 사용하고, 없으면 처음 접근할 때 자동으로 생성한다. (씬 전환 시 유지)
///
/// 발행 예)
///   EventManager.Instance.OnEnemyDeath?.Invoke(gameObject);
///
/// 구독 예)
///   private void OnEnable()
///   {
///       EventManager.Instance.OnEnemyDeath += HandleEnemyDeath;
///   }
///   private void OnDisable()
///   {
///       // 종료 중 매니져가 새로 생성되지 않도록 HasInstance 로 확인
///       if (true == EventManager.HasInstance)
///       {
///           EventManager.Instance.OnEnemyDeath -= HandleEnemyDeath;
///       }
///   }
///
/// 주의) = 로 대입하면 다른 구독자가 모두 지워지므로 반드시 += / -= 를 사용한다.
/// </summary>
[DisallowMultipleComponent]
public class EventManager
    : MonoBehaviour
{
    private static EventManager instance;
    private static bool isQuitting = false;

    #region Events

    /// <summary>
    /// 적 사망 시 (죽은 오브젝트)
    /// </summary>
    public Action<GameObject> OnEnemyDeath;

    /// <summary>
    /// 레벨업 시 (레벨업한 오브젝트, 오른 레벨 수)
    /// </summary>
    public Action<GameObject, int> OnLevelUp;

    #endregion

    public static EventManager Instance
    {
        get
        {
            if (null == instance)
            {
                instance = FindFirstObjectByType<EventManager>();
            }
            // 게임 종료 중에는 새로 만들지 않음
            if (null == instance && false == isQuitting)
            {
                new GameObject(nameof(EventManager)).AddComponent<EventManager>();
            }
            return instance;
        }
    }

    /// <summary>
    /// 매니져가 존재하는지 (구독 해제 시 자동 생성을 막기 위해 사용)
    /// </summary>
    public static bool HasInstance => null != instance;

    private void Awake()
    {
        if (null != instance && this != instance)
        {
            Debug.LogWarning($"[EventManager] 매니져가 여러 개입니다. ({instance.name}, {name}) 먼저 등록된 것을 사용합니다.");
            Destroy(this);
            return;
        }

        instance = this;

        // DontDestroyOnLoad 는 루트 오브젝트만 가능
        transform.SetParent(null, false);
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (this == instance)
        {
            instance = null;
        }
    }

    private void OnApplicationQuit()
    {
        isQuitting = true;
    }

    // Enter Play Mode 옵션으로 도메인 리로드를 끈 경우를 위해 static 초기화
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
        isQuitting = false;
    }
}
