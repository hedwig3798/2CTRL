using UnityEngine;

/// <summary>
/// 독 장판 연출 (판정과 무관. PoisonZoneProjectile 과 같은 오브젝트에 붙인다)
/// - 날아가는 동안 : 던진 병(thrown)만 보이고 돈다. 루트가 장판 반경에 맞춰 커져 있으므로 병은 월드 크기를 유지하도록 역보정한다
/// - 착지하면 : 병을 숨기고 구름(puffs)이 puffStagger 간격으로 하나씩 puffFrames 를 growDuration 동안 재생하며 피어오른 뒤
///   마지막 프레임에서 천천히 부풀었다 줄었다 한다
/// - 수명 진행률이 fadeStartRate 이상이면 구름이 흐려지며 사라진다 (PoisonZoneProjectile.hitEndRate 와 같은 값으로 두면 흐려지는 동안 판정 없음)
/// </summary>
public class PoisonZoneVisual
    : MonoBehaviour
{
    [Header("refs")]
    [Tooltip("비워두면 같은 오브젝트의 PoisonZoneLandMovement. 없으면 바로 착지한 것으로 본다")]
    public PoisonZoneLandMovement landMovement;
    [Tooltip("비워두면 같은 오브젝트의 Lifetime")]
    public Lifetime lifetime;

    [Header("thrown")]
    [Tooltip("날아가는 동안 보일 병")]
    public SpriteRenderer thrown;
    [Tooltip("병의 월드 크기 배율 (루트 크기와 무관)")]
    [Min(0.01f)]
    public float thrownWorldScale = 0.7f;
    [Tooltip("병이 도는 속도 (도/초)")]
    public float thrownSpin = 540f;

    [Header("puffs")]
    [Tooltip("장판 구름 조각들 (자식 SpriteRenderer)")]
    public SpriteRenderer[] puffs;
    [Tooltip("피어오르는 프레임 (마지막 프레임에서 멈춘다)")]
    public Sprite[] puffFrames;
    [Tooltip("구름 하나가 피어오르는 시간 (초)")]
    [Min(0.01f)]
    public float growDuration = 0.3f;
    [Tooltip("구름 조각 사이 등장 간격 (초)")]
    [Min(0f)]
    public float puffStagger = 0.05f;
    [Tooltip("다 피어난 뒤 부풀었다 줄었다 하는 폭 (크기 배율)")]
    [Min(0f)]
    public float wobble = 0.06f;
    [Tooltip("부풂 속도 (rad/초)")]
    public float wobbleSpeed = 3f;

    [Header("fade")]
    [Tooltip("수명 진행률이 이 값 이상이면 흐려지기 시작한다")]
    [Range(0f, 1f)]
    public float fadeStartRate = 0.85f;

    private Vector3[] puffBaseScales;
    private Color[] puffBaseColors;
    private Color thrownBaseColor = Color.white;
    private float landTime;
    private bool landed;

    private void Awake()
    {
        if (null == landMovement)
        {
            landMovement = GetComponent<PoisonZoneLandMovement>();
        }
        if (null == lifetime)
        {
            lifetime = GetComponent<Lifetime>();
        }

        // 흐려지는 시점은 판정이 끝나는 시점(hitEndRate)과 맞춘다
        PoisonZoneProjectile zone = GetComponent<PoisonZoneProjectile>();
        if (null != zone && zone.hitEndRate < 1f)
        {
            fadeStartRate = zone.hitEndRate;
        }

        int count = null != puffs ? puffs.Length : 0;
        puffBaseScales = new Vector3[count];
        puffBaseColors = new Color[count];
        for (int i = 0; i < count; ++i)
        {
            if (null == puffs[i])
            {
                continue;
            }
            puffBaseScales[i] = puffs[i].transform.localScale;
            puffBaseColors[i] = puffs[i].color;
        }
        if (null != thrown)
        {
            thrownBaseColor = thrown.color;
        }
    }

    private void OnEnable()
    {
        // 착지 여부는 발사 초기화(BlackBoard) 뒤 첫 Update 에서 판단한다 (풀에서 꺼낸 직후 landMovement 는 이전 상태)
        landed = false;
        landTime = 0f;
        ResetPuffs();
        if (null != thrown)
        {
            thrown.enabled = false;
        }
    }

    // 루트 크기(ScaleOverLifetime.Update)가 정해진 뒤 병 크기를 역보정하도록 LateUpdate
    private void LateUpdate()
    {
        if (false == landed)
        {
            if (null != landMovement && false == landMovement.IsLanded)
            {
                UpdateThrown();
                return;
            }

            landed = true;
            landTime = Time.time;
            if (null != thrown)
            {
                thrown.enabled = false;
            }
            ResetPuffs();
        }

        UpdatePuffs();
    }

    private void UpdateThrown()
    {
        if (null == thrown)
        {
            return;
        }

        thrown.enabled = true;
        thrown.color = thrownBaseColor;
        Transform t = thrown.transform;
        float rootScale = Mathf.Max(0.0001f, Mathf.Abs(transform.lossyScale.x));
        float s = thrownWorldScale / rootScale;
        t.localScale = new Vector3(s, s, 1f);
        t.localRotation = Quaternion.Euler(0f, 0f, -thrownSpin * Time.time);
    }

    private void UpdatePuffs()
    {
        if (null == puffs || 0 == puffs.Length)
        {
            return;
        }

        float alpha = 1f;
        if (null != lifetime && fadeStartRate < 1f)
        {
            float progress = 1f - lifetime.RemainRate;
            alpha = 1f - Mathf.Clamp01((progress - fadeStartRate) / (1f - fadeStartRate));
        }

        int frameCount = null != puffFrames ? puffFrames.Length : 0;
        float elapsed = Time.time - landTime;
        for (int i = 0; i < puffs.Length; ++i)
        {
            SpriteRenderer sr = puffs[i];
            if (null == sr)
            {
                continue;
            }
            float t = elapsed - i * puffStagger;
            if (t < 0f)
            {
                sr.enabled = false;
                continue;
            }

            sr.enabled = true;
            if (frameCount > 0)
            {
                int index = Mathf.Min(frameCount - 1, Mathf.FloorToInt(t / growDuration * frameCount));
                sr.sprite = puffFrames[index];
            }

            float k = 1f;
            if (t >= growDuration)
            {
                k += wobble * Mathf.Sin((t - growDuration) * wobbleSpeed + i * 1.7f);
            }
            sr.transform.localScale = puffBaseScales[i] * k;

            Color c = puffBaseColors[i];
            c.a *= alpha;
            sr.color = c;
        }
    }

    /// <summary>
    /// 구름을 숨기고 처음 상태로 (보이는 건 UpdatePuffs 가 등장 시간에 맞춰 켠다)
    /// </summary>
    private void ResetPuffs()
    {
        if (null == puffs)
        {
            return;
        }

        for (int i = 0; i < puffs.Length; ++i)
        {
            if (null == puffs[i])
            {
                continue;
            }
            puffs[i].enabled = false;
            puffs[i].transform.localScale = puffBaseScales[i];
            puffs[i].color = puffBaseColors[i];
            if (null != puffFrames && puffFrames.Length > 0)
            {
                puffs[i].sprite = puffFrames[0];
            }
        }
    }
}
