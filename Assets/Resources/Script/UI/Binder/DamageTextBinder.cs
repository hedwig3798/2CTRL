using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// HealthSystem 의 피격 데미지를 FloatingTextUI 로 띄우는 연결 컴포넌트
/// FloatingTextUI 는 데미지를 모르고, HealthSystem 은 UI 를 모른다. 둘 사이의 연결만 담당한다.
/// - target 이 비어있으면 부모에서 HealthSystem 을 찾는다.
/// - spawnPoint 가 비어있으면 target 의 위치에 띄운다.
/// - 텍스트는 씬 루트의 컨테이너 아래에 만들어지므로, 대상이 죽어 비활성화돼도 끝까지 출력된다.
///
/// 런타임에 대상이 정해지는 경우
///   binder.SetTarget(monster.GetComponent<HealthSystem>());
/// </summary>
public class DamageTextBinder
    : MonoBehaviour
{
    [Tooltip("데미지를 표시할 대상 (비어있으면 부모에서 탐색)")]
    [SerializeField]
    private HealthSystem target;

    [Tooltip("데미지 텍스트가 뜰 위치 (비어있으면 대상의 위치)")]
    [SerializeField]
    private Transform spawnPoint;

    [Header("텍스트")]
    public Color textColor = Color.white;
    [Tooltip("월드 기준 글자 크기")]
    public float fontSize = 4f;
    [Tooltip("데미지 숫자 포맷 (예 : \"0\" 정수, \"0.#\" 소수 첫째 자리)")]
    public string numberFormat = "0";

    [Header("연출")]
    [Tooltip("표시 시간 (초)")]
    public float duration = 0.8f;
    [Tooltip("위치에 더할 월드 오프셋 (예 : 머리 위)")]
    public Vector3 worldOffset = new Vector3(0f, 1f, 0f);
    [Tooltip("표시되는 동안 위로 떠오르는 월드 거리")]
    public float worldRise = 1f;
    [Tooltip("겹침 방지를 위한 가로 랜덤 오프셋 (월드, ±)")]
    public float randomOffsetX = 0.3f;

    private readonly Stack<FloatingTextUI> pool = new Stack<FloatingTextUI>();
    private Transform container;

    public HealthSystem Target => target;

    /// <summary>
    /// 표시 대상 변경 (null 이면 연결 해제)
    /// </summary>
    public void SetTarget(HealthSystem _target)
    {
        if (target == _target)
        {
            return;
        }

        Unbind();
        target = _target;

        if (true == isActiveAndEnabled)
        {
            Bind();
        }
    }

    private void Awake()
    {
        if (null == target)
        {
            target = GetComponentInParent<HealthSystem>();
        }
    }

    private void OnEnable()
    {
        Bind();
    }

    private void OnDisable()
    {
        Unbind();
    }

    private void OnDestroy()
    {
        // 씬 루트에 둔 컨테이너는 직접 정리 (고아 오브젝트 방지)
        if (null != container)
        {
            Destroy(container.gameObject);
        }
    }

    private void Bind()
    {
        if (null == target)
        {
            return;
        }

        target.OnDamaged -= HandleDamaged;
        target.OnDamaged += HandleDamaged;
    }

    private void Unbind()
    {
        if (null != target)
        {
            target.OnDamaged -= HandleDamaged;
        }
    }

    private void HandleDamaged(float _amount)
    {
        Transform anchor = (null != spawnPoint) ? spawnPoint : target.transform;

        Vector3 worldPos = anchor.position + worldOffset;
        worldPos.x += Random.Range(-randomOffsetX, randomOffsetX);

        FloatingTextUI ui = Get();
        ui.Play(_amount.ToString(numberFormat), textColor, worldPos, anchor.gameObject.layer, fontSize, duration, worldRise, Release);
    }

    private FloatingTextUI Get()
    {
        if (null == container)
        {
            // 대상이 비활성화돼도 텍스트가 남도록 씬 루트에 생성
            container = new GameObject($"{nameof(DamageTextBinder)}_{name}").transform;
        }

        if (0 < pool.Count)
        {
            return pool.Pop();
        }
        return FloatingTextUI.Create(container);
    }

    private void Release(FloatingTextUI _ui)
    {
        pool.Push(_ui);
    }
}
