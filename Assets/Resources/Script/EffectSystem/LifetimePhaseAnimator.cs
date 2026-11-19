using UnityEngine;

/// <summary>
/// Lifetime 에 맞춰 등장 -> 반복 -> 퇴장 세 구간의 프레임을 재생한다 (덩굴 조각처럼 수명이 정해진 장판 연출용)
/// - 등장 : 켜진 뒤 introDuration 초 동안 introFrames 를 한 번
/// - 반복 : loopFrames 를 loopFps 로 (켜질 때 시작 프레임을 무작위로 해 여러 개가 같이 흔들리지 않게)
/// - 퇴장 : 남은 수명이 outroDuration 초 이하이면 outroFrames 를 한 번
/// 수명이 짧아 등장과 퇴장이 겹치면 퇴장이 우선한다
/// </summary>
public class LifetimePhaseAnimator
    : MonoBehaviour
{
    [Tooltip("비워두면 같은 오브젝트의 SpriteRenderer")]
    public SpriteRenderer target;
    [Tooltip("비워두면 같은 오브젝트(또는 부모)의 Lifetime")]
    public Lifetime lifetime;

    public Sprite[] introFrames;
    public Sprite[] loopFrames;
    public Sprite[] outroFrames;

    [Min(0f)]
    public float introDuration = 0.35f;
    [Min(0.01f)]
    public float loopFps = 4f;
    [Min(0f)]
    public float outroDuration = 0.3f;
    [Tooltip("켜질 때 좌우를 무작위로 뒤집는다")]
    public bool randomFlip = true;

    private float startTime;
    private int loopOffset;

    private void Awake()
    {
        if (null == target)
        {
            target = GetComponent<SpriteRenderer>();
        }
        if (null == lifetime)
        {
            lifetime = GetComponentInParent<Lifetime>();
        }
    }

    private void OnEnable()
    {
        startTime = Time.time;
        loopOffset = null != loopFrames && loopFrames.Length > 0 ? Random.Range(0, loopFrames.Length) : 0;
        if (null != target)
        {
            if (true == randomFlip)
            {
                target.flipX = Random.value < 0.5f;
            }
            if (null != introFrames && introFrames.Length > 0)
            {
                target.sprite = introFrames[0];
            }
        }
    }

    private void Update()
    {
        if (null == target)
        {
            return;
        }

        float elapsed = Time.time - startTime;
        float remain = null != lifetime ? lifetime.RemainRate * lifetime.lifetime : float.PositiveInfinity;

        Sprite[] set;
        int index;
        if (remain <= outroDuration && null != outroFrames && outroFrames.Length > 0)
        {
            set = outroFrames;
            float t = outroDuration > 0f ? 1f - remain / outroDuration : 1f;
            index = Mathf.FloorToInt(t * set.Length);
        }
        else if (elapsed < introDuration && null != introFrames && introFrames.Length > 0)
        {
            set = introFrames;
            index = Mathf.FloorToInt(elapsed / introDuration * set.Length);
        }
        else if (null != loopFrames && loopFrames.Length > 0)
        {
            set = loopFrames;
            index = Mathf.FloorToInt((elapsed - introDuration) * loopFps) + loopOffset;
            index %= set.Length;
        }
        else
        {
            return;
        }

        target.sprite = set[Mathf.Clamp(index, 0, set.Length - 1)];
    }
}
