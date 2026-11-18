using UnityEngine;

/// <summary>
/// 판 종료 시 차원력 획득 계수 (MetaReward.Calculate 가 사용)
/// - 차원력 = floor((클리어 웨이브 × waveCoef + 양쪽 처치 합 × killCoef + 레벨 × levelCoef) × 결과 배율)
/// - GameOverManager 에 연결하지 않으면 DEFAULT_* 값을 쓴다
/// </summary>
[CreateAssetMenu(fileName = "MetaRewardConfig", menuName = "2CTRL/Meta/MetaRewardConfig")]
public class MetaRewardConfig
    : ScriptableObject
{
    public const float DEFAULT_WAVE_COEF = 15f;
    public const float DEFAULT_KILL_COEF = 0.005f;
    public const float DEFAULT_LEVEL_COEF = 2f;
    public const float DEFAULT_VICTORY_RATE = 1f;
    public const float DEFAULT_DEFEAT_RATE = 0.5f;

    [Tooltip("클리어한 웨이브 1개당")]
    [Min(0f)]
    [SerializeField]
    private float waveCoef = DEFAULT_WAVE_COEF;

    [Tooltip("처치 1마리당 (양쪽 합)")]
    [Min(0f)]
    [SerializeField]
    private float killCoef = DEFAULT_KILL_COEF;

    [Tooltip("공유 레벨 1당")]
    [Min(0f)]
    [SerializeField]
    private float levelCoef = DEFAULT_LEVEL_COEF;

    [Tooltip("승리 시 결과 배율")]
    [Min(0f)]
    [SerializeField]
    private float victoryRate = DEFAULT_VICTORY_RATE;

    [Tooltip("패배 시 결과 배율")]
    [Min(0f)]
    [SerializeField]
    private float defeatRate = DEFAULT_DEFEAT_RATE;

    public float WaveCoef => waveCoef;
    public float KillCoef => killCoef;
    public float LevelCoef => levelCoef;
    public float VictoryRate => victoryRate;
    public float DefeatRate => defeatRate;

    /// <summary>
    /// 값을 한 번에 넣는다 (에디터 빌더·테스트용)
    /// </summary>
    public void Configure(float _waveCoef, float _killCoef, float _levelCoef, float _victoryRate, float _defeatRate)
    {
        waveCoef = Mathf.Max(0f, _waveCoef);
        killCoef = Mathf.Max(0f, _killCoef);
        levelCoef = Mathf.Max(0f, _levelCoef);
        victoryRate = Mathf.Max(0f, _victoryRate);
        defeatRate = Mathf.Max(0f, _defeatRate);
    }
}
