using UnityEngine;

/// <summary>
/// 폭풍 소용돌이(무기 id Tornado) 연출 (판정과 무관. TornadoProjectile 과 같은 오브젝트에 붙인다)
/// - floor : 바닥 소용돌이 (프레임 반복은 SpriteFrameAnimator). 지름 = 끌어당김 반경 * 2. 생길 때 커지며 나타나고 사라지기 직전 흐려진다
/// - debris : 끌어당김 반경 가장자리에서 나선을 그리며 가운데로 빨려드는 잎. 수명이 끝나면 다시 가장자리에서 나온다
/// - zap : 타격 판정(TornadoProjectile.LastTickTime) 때마다 타격 반경 안에서 번개 프레임을 한 번 재생한다
/// TornadoProjectile.Update 뒤에 돌도록 LateUpdate + 실행 순서를 늦춘다
/// </summary>
[DefaultExecutionOrder(100)]
public class TornadoVortexVisual
    : MonoBehaviour
{
    [Header("refs")]
    [Tooltip("비워두면 같은 오브젝트의 TornadoProjectile")]
    public TornadoProjectile tornado;

    [Header("floor")]
    public SpriteRenderer floor;
    [Tooltip("floor 그림 크기 1 일 때 지름 (유닛)")]
    [Min(0.01f)]
    public float floorUnitDiameter = 1f;
    [Tooltip("생길 때 커지는 시간 / 사라지기 전 흐려지는 시간 (초)")]
    [Min(0.01f)]
    public float fadeTime = 0.3f;

    [Header("debris")]
    public SpriteRenderer[] debris;
    public Sprite[] debrisFrames;
    [Min(0.01f)]
    public float debrisFps = 12f;
    [Tooltip("잎 하나가 가장자리에서 가운데까지 빨려드는 시간 (초)")]
    [Min(0.05f)]
    public float debrisLifetime = 0.9f;
    [Tooltip("빨려드는 동안 도는 각도 (도). 양수 = 반시계 (바닥 나선이 도는 방향과 맞춘다)")]
    public float debrisSpin = 220f;
    [Tooltip("잎 크기 배율")]
    public float debrisScale = 1.4f;

    [Header("zap")]
    public SpriteRenderer zap;
    public Sprite[] zapFrames;
    [Tooltip("번개 한 번 재생 시간 (초)")]
    [Min(0.01f)]
    public float zapDuration = 0.22f;
    [Tooltip("번개 크기 배율")]
    public float zapScale = 1.5f;

    private float[] ages;
    private float[] startAngles;
    private float[] startRadii;
    private Color[] debrisColors;
    private Color floorColor = Color.white;
    private float spawnTime;
    private float shownTick = -1f;

    private void Awake()
    {
        if (null == tornado)
        {
            tornado = GetComponent<TornadoProjectile>();
        }
        if (null != floor)
        {
            floorColor = floor.color;
        }

        int count = null != debris ? debris.Length : 0;
        ages = new float[count];
        startAngles = new float[count];
        startRadii = new float[count];
        debrisColors = new Color[count];
        for (int i = 0; i < count; ++i)
        {
            if (null != debris[i])
            {
                debrisColors[i] = debris[i].color;
            }
        }
    }

    private void OnEnable()
    {
        spawnTime = Time.time;
        shownTick = -1f;
        if (null != zap)
        {
            zap.enabled = false;
        }
        // 잎은 처음부터 고르게 퍼져 있도록 나이를 흩어 둔다
        for (int i = 0; i < ages.Length; ++i)
        {
            Respawn(i);
            ages[i] = debrisLifetime * i / Mathf.Max(1, ages.Length);
        }
    }

    private void LateUpdate()
    {
        if (null == tornado)
        {
            return;
        }

        float pullRadius = tornado.CurrentPullRadius;

        // 등장 / 퇴장
        float appear = Mathf.Clamp01((Time.time - spawnTime) / fadeTime);
        float vanish = Mathf.Clamp01(tornado.RemainTime / fadeTime);
        float presence = Mathf.Min(appear, vanish);

        UpdateFloor(pullRadius, appear, presence);
        UpdateDebris(pullRadius, presence);
        UpdateZap();
    }

    private void UpdateFloor(float _pullRadius, float _appear, float _presence)
    {
        if (null == floor)
        {
            return;
        }

        // 나타날 때 0.6 -> 1 로 커진다 (ease out)
        float grow = 0.6f + 0.4f * (1f - (1f - _appear) * (1f - _appear));
        float s = _pullRadius * 2f / floorUnitDiameter * grow;
        floor.transform.localScale = new Vector3(s, s, 1f);
        Color c = floorColor;
        c.a *= _presence;
        floor.color = c;
    }

    private void UpdateDebris(float _pullRadius, float _presence)
    {
        if (null == debris)
        {
            return;
        }

        float dt = Time.deltaTime;
        int frameCount = null != debrisFrames ? debrisFrames.Length : 0;
        for (int i = 0; i < debris.Length; ++i)
        {
            SpriteRenderer sr = debris[i];
            if (null == sr)
            {
                continue;
            }

            ages[i] += dt;
            if (ages[i] >= debrisLifetime)
            {
                Respawn(i);
            }

            float t = ages[i] / debrisLifetime;
            // 처음엔 천천히, 가운데에 가까울수록 빨리 빨려든다 (ease in)
            float radius = _pullRadius * startRadii[i] * (1f - t * t);
            float angle = (startAngles[i] + debrisSpin * t) * Mathf.Deg2Rad;
            Transform tr = sr.transform;
            tr.localPosition = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f);
            float s = debrisScale * (0.6f + 0.4f * (1f - t));
            tr.localScale = new Vector3(s, s, 1f);

            if (frameCount > 0)
            {
                sr.sprite = debrisFrames[(Mathf.FloorToInt(ages[i] * debrisFps) + i) % frameCount];
            }

            // 가장자리에서 나타나고 가운데에서 사라진다
            float alpha = Mathf.Min(Mathf.Clamp01(t / 0.15f), Mathf.Clamp01((1f - t) / 0.2f)) * _presence;
            Color c = debrisColors[i];
            c.a *= alpha;
            sr.color = c;
        }
    }

    private void Respawn(int _index)
    {
        ages[_index] = 0f;
        startAngles[_index] = Random.Range(0f, 360f);
        startRadii[_index] = Random.Range(0.75f, 1f);
        if (null != debris[_index])
        {
            debris[_index].flipX = Random.value < 0.5f;
        }
    }

    private void UpdateZap()
    {
        if (null == zap)
        {
            return;
        }

        float tick = tornado.LastTickTime;
        if (tick >= 0f && tick != shownTick)
        {
            shownTick = tick;
            // 타격 반경 안 무작위 지점, 무작위 좌우
            Vector2 offset = Random.insideUnitCircle * tornado.CurrentHitRadius * 0.5f;
            zap.transform.localPosition = new Vector3(offset.x, offset.y + 0.2f, 0f);
            zap.transform.localScale = new Vector3(zapScale, zapScale, 1f);
            zap.flipX = Random.value < 0.5f;
        }

        float elapsed = Time.time - shownTick;
        int frameCount = null != zapFrames ? zapFrames.Length : 0;
        if (shownTick < 0f || frameCount == 0 || elapsed >= zapDuration)
        {
            zap.enabled = false;
            return;
        }

        zap.enabled = true;
        zap.sprite = zapFrames[Mathf.Min(frameCount - 1, Mathf.FloorToInt(elapsed / zapDuration * frameCount))];
    }
}
