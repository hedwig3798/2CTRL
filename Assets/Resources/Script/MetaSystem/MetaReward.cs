using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 한 판의 차원력 획득 결과 (항목별 기여, 배율, 합계)
/// </summary>
public readonly struct MetaRewardResult
{
    public readonly int ClearedWaves;
    public readonly int Kills;
    public readonly int Level;
    public readonly bool Victory;

    /// <summary>
    /// 클리어 웨이브 × waveCoef (배율 적용 전)
    /// </summary>
    public readonly float WaveValue;

    /// <summary>
    /// 처치 합 × killCoef (배율 적용 전)
    /// </summary>
    public readonly float KillValue;

    /// <summary>
    /// 레벨 × levelCoef (배율 적용 전)
    /// </summary>
    public readonly float LevelValue;

    /// <summary>
    /// 결과 배율 (승리 / 패배)
    /// </summary>
    public readonly float Rate;

    /// <summary>
    /// 지급량 = floor((WaveValue + KillValue + LevelValue) × Rate)
    /// </summary>
    public readonly int Total;

    public MetaRewardResult(int _clearedWaves, int _kills, int _level, bool _victory, float _waveValue, float _killValue, float _levelValue, float _rate, int _total)
    {
        ClearedWaves = _clearedWaves;
        Kills = _kills;
        Level = _level;
        Victory = _victory;
        WaveValue = _waveValue;
        KillValue = _killValue;
        LevelValue = _levelValue;
        Rate = _rate;
        Total = _total;
    }
}

/// <summary>
/// 차원력 획득 계산 (순수 함수) 과 씬에서 입력을 모으는 헬퍼
/// - 차원력 = floor((클리어 웨이브 × A + 양쪽 처치 합 × B + 레벨 × C) × 결과 배율)
/// - float 계수(0.005 등) 오차로 floor 가 1 작아지지 않도록 decimal 로 계산한다
/// </summary>
public static class MetaReward
{
    // 계수·배율 상한 (decimal 변환 오버플로 방지)
    private const float MaxCoef = 1e9f;

    /// <summary>
    /// _config 계수로 계산 (_config 가 null 이면 MetaRewardConfig.DEFAULT_* 사용)
    /// </summary>
    public static MetaRewardResult Calculate(int _clearedWaves, int _kills, int _level, bool _victory, MetaRewardConfig _config)
    {
        if (null == _config)
        {
            return Calculate(_clearedWaves, _kills, _level, _victory,
                MetaRewardConfig.DEFAULT_WAVE_COEF,
                MetaRewardConfig.DEFAULT_KILL_COEF,
                MetaRewardConfig.DEFAULT_LEVEL_COEF,
                MetaRewardConfig.DEFAULT_VICTORY_RATE,
                MetaRewardConfig.DEFAULT_DEFEAT_RATE);
        }

        return Calculate(_clearedWaves, _kills, _level, _victory,
            _config.WaveCoef, _config.KillCoef, _config.LevelCoef, _config.VictoryRate, _config.DefeatRate);
    }

    /// <summary>
    /// 계수를 직접 받아 계산 (음수 입력·계수는 0 으로 자른다)
    /// </summary>
    public static MetaRewardResult Calculate(int _clearedWaves, int _kills, int _level, bool _victory,
        float _waveCoef, float _killCoef, float _levelCoef, float _victoryRate, float _defeatRate)
    {
        int waves = Mathf.Max(0, _clearedWaves);
        int kills = Mathf.Max(0, _kills);
        int level = Mathf.Max(0, _level);
        float rate = Mathf.Max(0f, true == _victory ? _victoryRate : _defeatRate);

        // (decimal)float 는 유효 숫자 7 자리로 반올림되므로 0.005f 가 정확히 0.005 가 된다
        decimal waveValue = waves * ToDecimal(_waveCoef);
        decimal killValue = kills * ToDecimal(_killCoef);
        decimal levelValue = level * ToDecimal(_levelCoef);
        decimal total = Math.Floor((waveValue + killValue + levelValue) * ToDecimal(rate));

        int totalInt = total >= int.MaxValue ? int.MaxValue : (int)total;
        return new MetaRewardResult(waves, kills, level, _victory,
            (float)waveValue, (float)killValue, (float)levelValue, rate, totalInt);
    }

    /// <summary>
    /// 스포너 하나의 클리어 웨이브 수 = 끝났으면 웨이브 수, 아니면 진행 중 웨이브 번호 (시작 전 -1 은 0)
    /// </summary>
    public static int ClearedWaves(bool _isFinished, int _waveCount, int _currentWaveIndex)
    {
        return true == _isFinished ? Mathf.Max(0, _waveCount) : Mathf.Max(0, _currentWaveIndex);
    }

    /// <summary>
    /// 스포너들의 클리어 웨이브 수 중 최솟값 (없으면 0)
    /// </summary>
    public static int CollectClearedWaves(IEnumerable<Spawner> _spawners)
    {
        if (null == _spawners)
        {
            return 0;
        }

        int min = int.MaxValue;
        foreach (Spawner spawner in _spawners)
        {
            if (null == spawner)
            {
                continue;
            }
            min = Mathf.Min(min, ClearedWaves(spawner.IsFinished, spawner.WaveCount, spawner.CurrentWaveIndex));
        }
        return int.MaxValue == min ? 0 : min;
    }

    /// <summary>
    /// 양쪽 처치 합 (_stats 가 null 이면 0)
    /// </summary>
    public static int CollectKills(StatisticsManager _stats)
    {
        if (null == _stats)
        {
            return 0;
        }

        PlayerCombatStats left = _stats.Get(PlayerSide.Left);
        PlayerCombatStats right = _stats.Get(PlayerSide.Right);
        return (null != left ? left.Kills : 0) + (null != right ? right.Kills : 0);
    }

    /// <summary>
    /// 공유 레벨 (ExpSystem 이 없으면 0)
    /// </summary>
    public static int CollectLevel()
    {
        ExpSystem exp = ExpSystem.Instance;
        return null != exp ? Mathf.Max(0, exp.currLevel) : 0;
    }

    private static decimal ToDecimal(float _value)
    {
        if (false == (_value > 0f))
        {
            return 0m;
        }
        // decimal 범위를 넘는 값(무한대 포함) 은 OverflowException 이므로 자른다
        return (decimal)Mathf.Min(_value, MaxCoef);
    }
}
