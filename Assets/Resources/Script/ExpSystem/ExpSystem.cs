using System;
using UnityEngine;

/// <summary>
/// 모든 플레이어가 공유하는 EXP 시스템 (씬에 하나 배치)
/// - 플레이어는 ItemCollector 로 EXP 아이템을 수집해 ExpSystem.Instance.GetExp 로 전달한다.
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
                instance = FindFirstObjectByType<ExpSystem>();
            }
            return instance;
        }
    }

    public static bool HasInstance => null != instance;

    public int currLevel;
    public float currExp;
    public float maxExp;
    public LevelTable levelTable;

    public Action<GameObject, int> levelUpAction;
    public Action<float, float> OnExpChanged;

    public void GetExp(float _exp)
    {
        if (null == levelTable || null == levelTable.expTable || 0 == levelTable.expTable.Length)
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

    private float GetNeedExp(int level)
    {
        if (level < 0)
        {
            level = 0;
        }

        if (level >= levelTable.expTable.Length)
        {
            return levelTable.expTable[levelTable.expTable.Length - 1];
        }

        return levelTable.expTable[level];
    }
}
