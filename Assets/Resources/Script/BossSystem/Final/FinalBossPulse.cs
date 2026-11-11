using UnityEngine;

/// <summary>
/// 약한 맥동 (표시 전용): 로컬 스케일을 사인으로 흔든다
/// </summary>
public class FinalBossPulse
    : MonoBehaviour
{
    [SerializeField] private float amplitude = 0.08f;
    [SerializeField] private float speed = 8f;
    [SerializeField] private float phase;

    private Vector3 baseScale;

    private void Awake()
    {
        baseScale = transform.localScale;
    }

    private void Update()
    {
        float k = 1f + amplitude * Mathf.Sin(Time.time * speed + phase);
        transform.localScale = new Vector3(baseScale.x * k, baseScale.y * k, baseScale.z);
    }
}
