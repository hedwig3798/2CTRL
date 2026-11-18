using System;
using UnityEngine;

/// <summary>
/// 모든 플레이어가 공유하는 EXP 시스템 (씬에 하나 배치)
/// - 플레이어는 ItemCollector 로 EXP 아이템을 수집해 ExpSystem.Instance.GetExp 로 전달한다.
/// - 아이템 EXP 에는 expGainMultiplier 가 곱해진다 (ItemCollector.Absorb). GetExp 자체는 받은 값을 그대로 더한다
/// - EXP 변동은 OnExpChanged 로 알리고, UI 는 ExpSliderBinder 가 연결한다.
/// - 씬에 배치된 것만 사용하며 자동 생성 / 씬 전환 유지는 하지 않는다.
/// </summary>
[DisallowMultipleComponent]
public class ExpSystem
    : MonoBehaviour
{
    private static ExpSystem instance;

    public static ExpSystem Instance
    {
        get
        {
            if (null == instance)
            {
                instance = FindAnyObjectByType<ExpSystem>();
            }
            return instance;
        }
    }

    public static bool HasInstance => null != instance;

    public int currLevel;
    public float currExp;
    public float maxExp;
    public LevelTable levelTable;

    [Tooltip("EXP 아이템을 먹었을 때 아이템 EXP 에 곱하는 전체 획득 배율 (ItemCollector.Absorb 가 적용)")]
    [Min(0f)]
    [SerializeField]
    private float expGainMultiplier = 1.5f;

    /// <summary>
    /// EXP 아이템 획득 배율 (아이템 EXP × 이 값 = 실제 획득 EXP)
    /// </summary>
    public float ExpGainMultiplier => expGainMultiplier;

    public Action<GameObject, int> levelUpAction;
    public Action<float, float> OnExpChanged;

    public void GetExp(float _exp)
    {
        if (null == levelTable || false == levelTable.HasData)
        {
            return;
        }

        if (currLevel >= levelTable.maxLevel)
        {
            return;
        }

        currExp += _exp;

        if (maxExp <= 0f)
        {
            maxExp = GetNeedExp(currLevel);
            if (maxExp <= 0f)
            {
                return;
            }
        }

        int levelUpAmount = 0;

        while (currExp >= maxExp && maxExp > 0f)
        {
            currExp -= maxExp;
            levelUpAmount++;
            currLevel++;

            if (currLevel >= levelTable.maxLevel)
            {
                currLevel = levelTable.maxLevel;
                break;
            }

            maxExp = GetNeedExp(currLevel);
        }

        OnExpChanged?.Invoke(currExp, maxExp);

        if (0 < levelUpAmount)
        {
            levelUpAction?.Invoke(gameObject, levelUpAmount);
            EventManager.Instance.OnLevelUp?.Invoke(gameObject, levelUpAmount);
        }
    }

    private void Awake()
    {
        if (null != instance && this != instance)
        {
            Debug.LogWarning($"[ExpSystem] 공용 ExpSystem 이 여러 개입니다. ({instance.name}, {name}) 먼저 등록된 것을 사용합니다.");
            Destroy(this);
            return;
        }

        instance = this;
    }

    private void OnDestroy()
    {
        if (this == instance)
        {
            instance = null;
        }
    }

    // Enter Play Mode 옵션으로 도메인 리로드를 끈 경우를 위해 static 초기화
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
    }

    /// <summary>
    /// level 에서 다음 레벨로 오르는 데 필요한 EXP (LevelTable 의 공식 또는 표)
    /// </summary>
    private float GetNeedExp(int level)
    {
        return levelTable.GetNeedExp(level);
    }
}
