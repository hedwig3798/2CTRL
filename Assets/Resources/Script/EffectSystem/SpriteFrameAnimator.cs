using UnityEngine;

/// <summary>
/// SpriteRenderer 의 스프라이트를 프레임 배열로 바꿔 끼우는 가벼운 프레임 재생기 (풀링되는 연출용. Animator 없이 동작)
/// - LifetimeSync : Lifetime 진행률(0 -> 1)에 맞춰 첫 프레임부터 마지막 프레임까지 한 번 (폭발 등)
/// - OnceOverDuration : 켜질 때부터 duration 초 동안 한 번 재생하고 마지막 프레임에 멈춘다 (예고 표식 등)
/// - Loop : fps 로 반복
/// 켜질 때(OnEnable)마다 처음부터 다시 재생한다
/// </summary>
public class SpriteFrameAnimator
    : MonoBehaviour
{
    public enum PlayMode
    {
        LifetimeSync,
        OnceOverDuration,
        Loop,
    }

    [Header("frames")]
    [Tooltip("바꿀 렌더러. 비워두면 같은 오브젝트의 SpriteRenderer")]
    public SpriteRenderer target;
    [Tooltip("재생 순서대로 (같은 스프라이트를 여러 번 넣어 순서를 바꿀 수 있다)")]
    public Sprite[] frames;
    public PlayMode mode = PlayMode.LifetimeSync;

    [Header("timing")]
    [Tooltip("LifetimeSync 에서 쓸 Lifetime. 비워두면 같은 오브젝트(또는 부모)의 Lifetime")]
    public Lifetime lifetime;
    [Tooltip("OnceOverDuration 재생 시간 (초)")]
    [Min(0.01f)]
    public float duration = 0.5f;
    [Tooltip("Loop 초당 프레임 수")]
    [Min(0.01f)]
    public float fps = 10f;

    private float startTime;
    private int currentIndex = -1;

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
        Restart();
    }

    private void Update()
    {
        Apply();
    }

    /// <summary>
    /// 처음 프레임부터 다시 재생한다
    /// </summary>
    public void Restart()
    {
        startTime = Time.time;
        currentIndex = 0;
        // Lifetime.OnEnable 보다 먼저 불리면 이전 수명이 남아 있으므로 진행률을 읽지 않고 첫 프레임부터
        if (null != target && null != frames && frames.Length > 0)
        {
            target.sprite = frames[0];
        }
    }

    private void Apply()
    {
        if (null == target || null == frames || 0 == frames.Length)
        {
            return;
        }

        int count = frames.Length;
        int index;
        switch (mode)
        {
            case PlayMode.LifetimeSync:
                float progress = null != lifetime ? 1f - lifetime.RemainRate : 0f;
                index = Mathf.FloorToInt(progress * count);
                break;
            case PlayMode.OnceOverDuration:
                index = Mathf.FloorToInt((Time.time - startTime) / duration * count);
                break;
            default:
                index = Mathf.FloorToInt((Time.time - startTime) * fps) % count;
                break;
        }
        index = Mathf.Clamp(index, 0, count - 1);

        if (index != currentIndex)
        {
            currentIndex = index;
            target.sprite = frames[index];
        }
    }
}
