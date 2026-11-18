using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 영구 강화 진행도 (차원력, 강화별 레벨) 를 PlayerPrefs 에 저장 / 로드하는 static 클래스
/// - GameSettings 와 같은 패턴: 처음 접근할 때 읽고, 값이 바뀌면 PlayerPrefs 에 쓰고 바로 Save 한다
/// - 키: KEY_CURRENCY ("Meta.Currency"), KEY_LEVEL_PREFIX + id ("Meta.Level.{id}")
/// - 값이 바뀌면 OnChanged 호출 (OnEnable 에서 구독, OnDisable 에서 해제)
/// - DEMO_INVINCIBLE 빌드는 키 접두사를 "MetaDemo." 로 분리하고, 로드 시와 구매 후 차원력을 DEMO_CURRENCY 로 채운다
///   (같은 PC 에서 PlayerPrefs 를 공유하므로 일반 빌드 세이브를 오염시키지 않게)
///
/// 사용 예)
///   if (MetaProgress.TryPurchase(data)) { ... }
///   MetaProgress.AddCurrency(result.Total);
/// </summary>
public static class MetaProgress
{
#if DEMO_INVINCIBLE
    public const string KEY_PREFIX = "MetaDemo.";
#else
    public const string KEY_PREFIX = "Meta.";
#endif
    public const string KEY_CURRENCY = KEY_PREFIX + "Currency";
    public const string KEY_LEVEL_PREFIX = KEY_PREFIX + "Level.";

    /// <summary>
    /// DEMO_INVINCIBLE 빌드에서 채워 두는 차원력
    /// </summary>
    public const int DEMO_CURRENCY = 9999999;

    private static bool isLoaded;
    private static int currency;
    // id → 레벨 (처음 물어볼 때 PlayerPrefs 에서 읽는다)
    private static readonly Dictionary<string, int> levels = new Dictionary<string, int>();

    /// <summary>
    /// 차원력 또는 강화 레벨이 바뀔 때
    /// </summary>
    public static event Action OnChanged;

    #region Public API

    /// <summary>
    /// 보유 차원력
    /// </summary>
    public static int Currency
    {
        get
        {
            EnsureLoaded();
            return currency;
        }
    }

    /// <summary>
    /// 강화 레벨 (0 ~ MaxLevel, id 가 비었으면 0)
    /// </summary>
    public static int GetLevel(PermanentUpgradeData _data)
    {
        if (null == _data || true == string.IsNullOrEmpty(_data.Id))
        {
            return 0;
        }

        EnsureLoaded();
        if (false == levels.TryGetValue(_data.Id, out int level))
        {
            level = Mathf.Max(0, PlayerPrefs.GetInt(KEY_LEVEL_PREFIX + _data.Id, 0));
            levels[_data.Id] = level;
        }
        return Mathf.Min(level, _data.MaxLevel);
    }

    public static bool IsMaxLevel(PermanentUpgradeData _data)
    {
        return null != _data && GetLevel(_data) >= _data.MaxLevel;
    }

    /// <summary>
    /// 다음 레벨 가격 (최대 레벨이거나 _data 가 null 이면 -1)
    /// </summary>
    public static int GetCost(PermanentUpgradeData _data)
    {
        if (null == _data || true == IsMaxLevel(_data))
        {
            return -1;
        }
        return _data.GetCost(GetLevel(_data));
    }

    /// <summary>
    /// 최대 레벨이 아니고 차원력이 가격 이상인지
    /// </summary>
    public static bool CanPurchase(PermanentUpgradeData _data)
    {
        if (null == _data || true == string.IsNullOrEmpty(_data.Id))
        {
            return false;
        }

        int cost = GetCost(_data);
        return cost >= 0 && Currency >= cost;
    }

    /// <summary>
    /// 차원력을 내고 레벨을 1 올린다 (저장 + OnChanged)
    /// </summary>
    /// <returns>샀으면 true (최대 레벨 / 잔액 부족 / id 없음이면 false)</returns>
    public static bool TryPurchase(PermanentUpgradeData _data)
    {
        if (null != _data && true == string.IsNullOrEmpty(_data.Id))
        {
            Debug.LogWarning($"[MetaProgress] {_data.name} 의 id 가 비어 있어 구매할 수 없습니다.", _data);
        }

        if (false == CanPurchase(_data))
        {
            return false;
        }

        int level = GetLevel(_data);
        currency -= GetCost(_data);
        levels[_data.Id] = level + 1;

#if DEMO_INVINCIBLE
        currency = Mathf.Max(currency, DEMO_CURRENCY);
#endif

        PlayerPrefs.SetInt(KEY_CURRENCY, currency);
        PlayerPrefs.SetInt(KEY_LEVEL_PREFIX + _data.Id, level + 1);
        PlayerPrefs.Save();
        OnChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// 차원력을 더한다 (0 이하는 무시, int 범위로 자름. 저장 + OnChanged)
    /// </summary>
    public static void AddCurrency(int _amount)
    {
        if (_amount <= 0)
        {
            return;
        }

        EnsureLoaded();
        long sum = (long)currency + _amount;
        currency = sum > int.MaxValue ? int.MaxValue : (int)sum;
        PlayerPrefs.SetInt(KEY_CURRENCY, currency);
        PlayerPrefs.Save();
        OnChanged?.Invoke();
    }

    /// <summary>
    /// 디버그용 : 차원력과 강화 레벨을 지운다
    /// - PlayerPrefs 는 키를 나열할 수 없으므로 _upgrades 의 id 와 지금까지 읽은 id 의 레벨만 지운다
    /// </summary>
    public static void ResetAll(IEnumerable<PermanentUpgradeData> _upgrades = null)
    {
        EnsureLoaded();

        PlayerPrefs.DeleteKey(KEY_CURRENCY);
        if (null != _upgrades)
        {
            foreach (PermanentUpgradeData data in _upgrades)
            {
                if (null != data && false == string.IsNullOrEmpty(data.Id))
                {
                    PlayerPrefs.DeleteKey(KEY_LEVEL_PREFIX + data.Id);
                }
            }
        }
        foreach (string id in levels.Keys)
        {
            PlayerPrefs.DeleteKey(KEY_LEVEL_PREFIX + id);
        }
        PlayerPrefs.Save();

        levels.Clear();
        isLoaded = false;
        Load();
        OnChanged?.Invoke();
    }

    /// <summary>
    /// 저장값을 다시 읽는다 (테스트에서 PlayerPrefs 를 직접 바꾼 뒤. OnChanged 호출)
    /// </summary>
    public static void Reload()
    {
        levels.Clear();
        isLoaded = false;
        Load();
        OnChanged?.Invoke();
    }

    #endregion

    private static void EnsureLoaded()
    {
        if (false == isLoaded)
        {
            Load();
        }
    }

    private static void Load()
    {
        isLoaded = true;
        currency = Mathf.Max(0, PlayerPrefs.GetInt(KEY_CURRENCY, 0));

#if DEMO_INVINCIBLE
        if (currency < DEMO_CURRENCY)
        {
            currency = DEMO_CURRENCY;
            PlayerPrefs.SetInt(KEY_CURRENCY, currency);
            PlayerPrefs.Save();
        }
#endif
    }

    // Enter Play Mode 옵션으로 도메인 리로드를 끈 경우를 위해 static 초기화
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        isLoaded = false;
        levels.Clear();
        OnChanged = null;
    }
}
