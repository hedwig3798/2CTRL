using UnityEngine;

/// <summary>
/// 최종 보스 수치 (기획 docs/boss-design.md 4장). 거리 단위는 보스 월드 (진영 월드와 세로 배율 1:1 기준)
/// 페이즈별 배열은 [P1, P2, P3] 순서
/// </summary>
[CreateAssetMenu(fileName = "FinalBossData", menuName = "Boss/Final Boss Data")]
public class FinalBossData
    : ScriptableObject
{
    [Header("체력·몸체")]
    public float maxHP = 160000f;
    public float bodyRadius = 1.8f;
    [Tooltip("평소 몸체 접촉 데미지")]
    public float contactDamage = 20f;
    [Tooltip("같은 플레이어에게 접촉 데미지를 다시 줄 때까지 (초)")]
    public float contactCooldown = 0.5f;
    [Tooltip("판정 시 플레이어를 이 반경의 원으로 본다")]
    public float playerHitRadius = 0.3f;

    [Header("진입")]
    [Tooltip("모든 적 제거 후 보스 등장까지 경고 연출 (초)")]
    public float introDuration = 3f;
    [Tooltip("플레이어 화면 고정 여백")]
    public float playerClampMargin = 0.5f;
    [Tooltip("화면 위쪽 밖에서 시작하는 높이 (몸체 반경 위로 더)")]
    public float entryStartAbove = 1f;
    [Tooltip("내려와 멈추는 위치 (화면 위 경계에서 아래로)")]
    public float entryStopFromTop = 4f;
    public float entryDuration = 2f;
    [Tooltip("보스전 BGM (None 이면 바꾸지 않음)")]
    public SoundId bossBgm = SoundId.None;

    [Header("페이즈")]
    public float phase2Ratio = 0.66f;
    public float phase3Ratio = 0.33f;
    [Tooltip("패턴 사이 간격 (초) [P1, P2, P3]")]
    public float[] patternInterval = { 2.5f, 2.0f, 1.6f };
    [Tooltip("예고 시간 배율 [P1, P2, P3]")]
    public float[] telegraphScale = { 1f, 0.85f, 0.85f };
    [Tooltip("모든 예고의 최소 시간 (초)")]
    public float minTelegraph = 1f;
    [Tooltip("P1 에서 왼쪽 위협 뒤 오른쪽 위협까지 (초)")]
    public float p1SideStagger = 0.5f;
    [Tooltip("P3 에서 조합 패턴을 고를 확률")]
    [Range(0f, 1f)]
    public float p3ComboChance = 0.5f;
    [Tooltip("이 시간(초)이 지나면 격노 (패턴 간격 enrageInterval)")]
    public float enrageTime = 300f;
    public float enrageInterval = 1f;

    [Header("대기 이동")]
    public float idleMoveSpeed = 3f;
    [Tooltip("대기 이동 목표를 고를 때 진영 사각형 안쪽 여백")]
    public float idleInset = 2.5f;

    [Header("운석")]
    public float meteorWindup = 0.6f;
    [Tooltip("진영당 발 수 [P1, P2, P3] (1발은 플레이어 위치)")]
    public int[] meteorCountPerSide = { 3, 5, 5 };
    [Tooltip("비행 시간 = 경고 시간 (페이즈 배율 적용)")]
    public float meteorFlightTime = 1.8f;
    public float meteorRadius = 1.6f;
    public float meteorDamage = 30f;
    [Tooltip("포물선 최고 높이 (시각)")]
    public float meteorArcHeight = 3f;
    [Tooltip("무작위 착탄 지점을 고를 때 진영 사각형 안쪽 여백")]
    public float meteorSideInset = 1f;
    [Tooltip("P3 착탄 불장판")]
    public float fireDuration = 3f;
    public float fireRadius = 1.6f;
    public float fireDamage = 8f;
    public float fireTickInterval = 0.5f;

    [Header("돌진")]
    public float dashTelegraph = 1f;
    public float dashSpeed = 16f;
    public float dashMaxDuration = 3f;
    public int dashMaxBounces = 4;
    public float dashRest = 1.5f;
    public float dashDamage = 35f;
    public float dashArrowLength = 4f;
    public float dashArrowWidth = 0.5f;

    [Header("레이저")]
    [Tooltip("줄 수 [P1, P2, P3]")]
    public int[] laserCount = { 3, 4, 5 };
    public float laserWarn = 1.2f;
    public float laserBlink = 0.2f;
    public float laserBlinkInterval = 0.05f;
    public float laserFire = 0.4f;
    public float laserWidth = 0.8f;
    public float laserWarnWidth = 0.08f;
    public float laserDamage = 40f;
    [Tooltip("각 진영 절반에 보장할 안전 틈 폭")]
    public float laserSafeGap = 2f;
    [Tooltip("줄 수마다 배치 재시도 상한 (실패하면 줄 수를 하나 줄인다)")]
    public int laserMaxAttempts = 12;
    [Tooltip("안전 틈 검사 격자 간격")]
    public float laserGapSampleStep = 0.4f;
    [Tooltip("안전 틈 1차(거친) 검사 격자 간격. 통과한 후보만 정밀 검사")]
    public float laserCoarseStep = 1.2f;

    [Header("예고 표시")]
    public BossTelegraphStyle telegraphStyle;
    public Color warnColor = new Color(1f, 0.3f, 0.2f, 0.9f);
    public Color fillColor = new Color(1f, 0.3f, 0.2f, 0.35f);
    public Color fireColor = new Color(1f, 0.5f, 0.1f, 0.5f);
    public Color arrowColor = new Color(1f, 0.8f, 0.2f, 0.8f);
    public Color laserColor = new Color(1f, 0.2f, 0.4f, 1f);

    /// <summary>
    /// 페이즈 배열 값 (없으면 마지막 값, 배열이 비면 _fallback)
    /// </summary>
    public static float PhaseValue(float[] _values, int _phase, float _fallback)
    {
        if (null == _values || 0 == _values.Length)
        {
            return _fallback;
        }
        return _values[Mathf.Clamp(_phase, 0, _values.Length - 1)];
    }

    public static int PhaseValue(int[] _values, int _phase, int _fallback)
    {
        if (null == _values || 0 == _values.Length)
        {
            return _fallback;
        }
        return _values[Mathf.Clamp(_phase, 0, _values.Length - 1)];
    }

    /// <summary>
    /// 페이즈 예고 시간 (배율 적용, 최소 minTelegraph)
    /// </summary>
    public float Telegraph(float _base, int _phase)
    {
        return FinalBossPatterns.TelegraphTime(_base, PhaseValue(telegraphScale, _phase, 1f), minTelegraph);
    }
}
