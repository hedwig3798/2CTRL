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

    /// <summary>
    /// 플레이어가 데미지를 받았을 때 (받은 플레이어, 실제로 깎인 양)
    /// </summary>
    public Action<PlayerSide, float> OnPlayerDamaged;

    /// <summary>
    /// 플레이어 무기가 적에게 데미지를 줬을 때 (무기 주인, 무기, 실제로 깎인 양)
    /// </summary>
    public Action<PlayerSide, Weapon, float> OnWeaponDamage;

    /// <summary>
    /// 플레이어가 EXP 아이템을 수집했을 때 (수집한 플레이어, 획득 EXP)
    /// </summary>
    public Action<PlayerSide, float> OnExpGained;

    /// <summary>
    /// 플레이어 사망 시 (사망한 플레이어)
    /// </summary>
    public Action<PlayerSide> OnPlayerDeath;

    /// <summary>
    /// 중간 보스 출현 예고 (보스가 나올 진영, 보스 프리팹의 MidBoss. 씬 인스턴스가 아니므로 표시 정보만 읽는다)
    /// </summary>
    public Action<PlayerSide, MidBoss> OnBossWarning;

    /// <summary>
    /// 중간 보스 출현 (보스가 나온 진영, 스폰된 보스)
    /// </summary>
    public Action<PlayerSide, MidBoss> OnBossSpawned;

    /// <summary>
    /// 중간 보스 처치 (보스가 나온 진영 = 처치한 진영, 처치된 보스). LevelUpUI 가 보스 보상을 연다
    /// </summary>
    public Action<PlayerSide, MidBoss> OnBossDefeated;

    /// <summary>
    /// 경고만 하고 아직 나오지 않은 중간 보스 출현 취소 (진영). BossHudUI 가 경고 배너를 숨긴다
    /// </summary>
    public Action<PlayerSide> OnBossWarningCancelled;

    /// <summary>
    /// 최종 보스전 시작 (모든 웨이브 종료 직후, 인트로 연출 시작). HP 바 표시
    /// </summary>
    public Action OnFinalBossStart;

    /// <summary>
    /// 최종 보스 HP 변경 (현재치, 최대치)
    /// </summary>
    public Action<float, float> OnFinalBossHPChanged;

    /// <summary>
    /// 최종 보스 처치. FinalBossDirector 가 GameOverManager.TriggerVictory 를 부른다
    /// </summary>
    public Action OnFinalBossDefeated;

    #endregion

    public static EventManager Instance
    {
        get
        {
            if (null == instance)
            {
                instance = FindAnyObjectByType<EventManager>();
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
