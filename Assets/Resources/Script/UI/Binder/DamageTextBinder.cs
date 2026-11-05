using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// HealthSystem 의 피격 데미지를 FloatingTextUI 로 띄우는 연결 컴포넌트
/// FloatingTextUI 는 데미지를 모르고, HealthSystem 은 UI 를 모른다. 둘 사이의 연결만 담당한다.
/// - target 이 비어있으면 부모에서 HealthSystem 을 찾는다.
/// - spawnPoint 가 비어있으면 target 의 위치에 띄운다.
/// - 텍스트는 씬 루트의 컨테이너 아래에 만들어지므로, 대상이 죽어 비활성화돼도 끝까지 출력된다.
/// - GameSettings.ShowDamageText 가 꺼져 있으면 띄우지 않고, 꺼지는 순간 떠 있는 텍스트도 지운다.
/// - font 는 PixelFontBuilder.AssignDamageTextFont 가 프리팹에 연결한다. (비어있으면 TMP 기본 폰트)
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

    [Tooltip("텍스트 폰트 (비어있으면 TMP 기본 폰트)")]
    [SerializeField]
    private TMP_FontAsset font;

    [Tooltip("글자 스타일 (픽셀 폰트는 Bold 에셋을 쓰고 Normal 권장. 가짜 굵게는 픽셀이 뭉개짐)")]
    [SerializeField]
    private FontStyles fontStyle = FontStyles.Normal;

    [Header("정렬")]
    [Tooltip("정렬 레이어 이름 (Default 금지 : BG 에 가려짐. 없는 이름이면 Front UI → attack 순으로 대체)")]
    [SerializeField]
    private string sortingLayerName = "Front UI";

    [Tooltip("같은 정렬 레이어 안의 순서")]
    [SerializeField]
    private int sortingOrder = 1000;

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
    private readonly List<FloatingTextUI> active = new List<FloatingTextUI>();
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

        GameSettings.OnShowDamageTextChanged -= HandleShowDamageTextChanged;
        GameSettings.OnShowDamageTextChanged += HandleShowDamageTextChanged;
    }

    private void OnDisable()
    {
        Unbind();
        GameSettings.OnShowDamageTextChanged -= HandleShowDamageTextChanged;
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

    private void HandleShowDamageTextChanged(bool _show)
    {
        if (true == _show)
        {
            return;
        }

        // 떠 있는 텍스트도 바로 지운다 (Stop 은 onFinished 를 부르지 않으므로 직접 풀에 반환)
        for (int i = active.Count - 1; 0 <= i; --i)
        {
            FloatingTextUI ui = active[i];
            if (null != ui)
            {
                ui.Stop();
                pool.Push(ui);
            }
        }
        active.Clear();
    }

    private void HandleDamaged(float _amount)
    {
        if (false == GameSettings.ShowDamageText)
        {
            return;
        }

        Transform anchor = (null != spawnPoint) ? spawnPoint : target.transform;

        Vector3 worldPos = anchor.position + worldOffset;
        worldPos.x += Random.Range(-randomOffsetX, randomOffsetX);

        FloatingTextUI ui = Get();
        active.Add(ui);
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

        FloatingTextUI ui = FloatingTextUI.Create(container);
        int layerID = SortingLayer.NameToID(sortingLayerName);
        if (0 == layerID || false == SortingLayer.IsValid(layerID))
        {
            // Default(0) 나 없는 이름은 BG 에 가려지므로 기본 레이어로 대체
            layerID = FloatingTextUI.DefaultSortingLayerID();
        }
        ui.Setup(font, fontStyle, layerID, sortingOrder);
        return ui;
    }

    private void Release(FloatingTextUI _ui)
    {
        active.Remove(_ui);
        pool.Push(_ui);
    }
}
