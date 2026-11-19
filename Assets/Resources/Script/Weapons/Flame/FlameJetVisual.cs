using UnityEngine;

/// <summary>
/// 화염 방사 분사 연출 (판정과 무관. FlameProjectile 과 같은 오브젝트에 붙인다)
/// 분사하는 동안 container(FlameProjectile 의 visualRoot. +x = 분사 방향, 크기 = 범위 배율) 안에서
/// 불덩이(puffs)를 spawnRate 로 뿜는다. 불덩이는 노즐에서 부채꼴 안의 무작위 방향으로 날아가며 커지고 frames 를 수명 동안 한 번 재생한 뒤 흐려진다
/// - 불덩이 그림은 분사 방향과 상관없이 늘 위가 위 (불꽃이 거꾸로 서지 않게)
/// - 부채꼴 각도는 FlameProjectile.CurrentFanAngle 을 따른다 (maxSpreadAngle 로 제한)
/// - 렌더러는 미리 만든 자식(puffs)을 돌려 쓴다. 모자라면 가장 오래된 불덩이를 다시 쓴다
/// </summary>
[DefaultExecutionOrder(100)]
public class FlameJetVisual
    : MonoBehaviour
{
    [Header("refs")]
    [Tooltip("비워두면 같은 오브젝트의 FlameProjectile")]
    public FlameProjectile flame;
    [Tooltip("불덩이 렌더러 (container 의 자식)")]
    public SpriteRenderer[] puffs;
    [Tooltip("불덩이 하나가 수명 동안 한 번 재생할 프레임")]
    public Sprite[] frames;

    [Header("emit")]
    [Tooltip("초당 뿜는 불덩이 수")]
    [Min(1f)]
    public float spawnRate = 45f;
    [Tooltip("불덩이 수명 (초)")]
    [Min(0.05f)]
    public float puffLifetime = 0.4f;

    [Header("path (container 로컬 단위)")]
    [Tooltip("시작 거리 (노즐)")]
    public float startDistance = 0.3f;
    [Tooltip("끝 거리 (FlameProjectile.visualBaseRange 와 같게)")]
    public float endDistance = 3f;
    [Tooltip("끝 거리 무작위 감소 비율 (0.2 = 최대 20% 짧게)")]
    [Range(0f, 1f)]
    public float endJitter = 0.2f;
    [Tooltip("부채꼴 각도 중 실제로 퍼지는 비율")]
    [Range(0f, 1f)]
    public float spreadRate = 0.85f;
    [Tooltip("퍼지는 최대 각도 (도)")]
    [Range(0f, 360f)]
    public float maxSpreadAngle = 140f;

    [Header("size / fade")]
    public float startScale = 0.45f;
    public float endScale = 1.5f;
    [Tooltip("수명 진행률이 이 값 이상이면 흐려진다")]
    [Range(0f, 1f)]
    public float fadeStartRate = 0.65f;

    private float[] ages;
    private float[] angles;
    private float[] ends;
    private Color[] baseColors;
    private float spawnAccum;

    private void Awake()
    {
        if (null == flame)
        {
            flame = GetComponent<FlameProjectile>();
        }

        int count = null != puffs ? puffs.Length : 0;
        ages = new float[count];
        angles = new float[count];
        ends = new float[count];
        baseColors = new Color[count];
        for (int i = 0; i < count; ++i)
        {
            if (null != puffs[i])
            {
                baseColors[i] = puffs[i].color;
            }
        }
    }

    private void OnEnable()
    {
        for (int i = 0; i < ages.Length; ++i)
        {
            ages[i] = -1f;
            if (null != puffs[i])
            {
                puffs[i].enabled = false;
            }
        }
        // 켜지자마자 한 개는 바로 나오게
        spawnAccum = 1f;
    }

    private void LateUpdate()
    {
        if (null == puffs || 0 == puffs.Length)
        {
            return;
        }

        float dt = Time.deltaTime;
        spawnAccum += dt * spawnRate;
        while (spawnAccum >= 1f)
        {
            spawnAccum -= 1f;
            Spawn();
        }

        int frameCount = null != frames ? frames.Length : 0;
        for (int i = 0; i < puffs.Length; ++i)
        {
            SpriteRenderer sr = puffs[i];
            if (null == sr || ages[i] < 0f)
            {
                continue;
            }

            ages[i] += dt;
            float t = ages[i] / puffLifetime;
            if (t >= 1f)
            {
                ages[i] = -1f;
                sr.enabled = false;
                continue;
            }

            // 빠르게 나가 감속 (ease out)
            float eased = 1f - (1f - t) * (1f - t);
            float distance = Mathf.Lerp(startDistance, ends[i], eased);
            float rad = angles[i] * Mathf.Deg2Rad;
            Transform pt = sr.transform;
            pt.localPosition = new Vector3(Mathf.Cos(rad) * distance, Mathf.Sin(rad) * distance, 0f);
            pt.rotation = Quaternion.identity;
            float s = Mathf.Lerp(startScale, endScale, eased);
            pt.localScale = new Vector3(s, s, 1f);

            if (frameCount > 0)
            {
                sr.sprite = frames[Mathf.Min(frameCount - 1, Mathf.FloorToInt(t * frameCount))];
            }

            Color c = baseColors[i];
            if (t > fadeStartRate && fadeStartRate < 1f)
            {
                c.a *= 1f - (t - fadeStartRate) / (1f - fadeStartRate);
            }
            sr.color = c;
        }
    }

    private void Spawn()
    {
        // 쉬는 불덩이, 없으면 가장 오래된 불덩이
        int pick = -1;
        float oldest = -1f;
        for (int i = 0; i < puffs.Length; ++i)
        {
            if (null == puffs[i])
            {
                continue;
            }
            if (ages[i] < 0f)
            {
                pick = i;
                break;
            }
            if (ages[i] > oldest)
            {
                oldest = ages[i];
                pick = i;
            }
        }
        if (pick < 0)
        {
            return;
        }

        float fan = null != flame ? flame.CurrentFanAngle : 60f;
        float half = Mathf.Min(fan, maxSpreadAngle) * spreadRate * 0.5f;
        angles[pick] = Random.Range(-half, half);
        ends[pick] = endDistance * (1f - Random.Range(0f, endJitter));
        ages[pick] = 0f;

        SpriteRenderer sr = puffs[pick];
        sr.enabled = true;
        sr.flipX = Random.value < 0.5f;
        // 겹치는 순서를 섞어 같은 모양이 줄지어 보이지 않게
        sr.sortingOrder = 2 + (Time.frameCount + pick) % 8;
        if (null != frames && frames.Length > 0)
        {
            sr.sprite = frames[0];
        }
        sr.transform.localPosition = new Vector3(startDistance, 0f, 0f);
        sr.transform.localScale = new Vector3(startScale, startScale, 1f);
    }
}
