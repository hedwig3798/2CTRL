using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// 데미지 텍스트 출력을 담당하는 전역 매니져
/// - 출력 여부(On/Off), 색, 포맷, 연출을 여기서 일괄 결정한다.
/// - 씬에 미리 배치하면 그것을 사용하고, 없으면 처음 접근할 때 자동으로 생성한다. (씬 전환 시 유지)
///
/// 타 스크립트 사용 예)
///   DamageTextManager.Instance.Show(damage, transform, UI_SCREEN.Auto);
///   DamageTextManager.Instance.IsEnabled = false;   // 설정 창 등에서 일괄 Off
///   DamageTextManager.Instance.TextColor = Color.yellow;
/// </summary>
[DisallowMultipleComponent]
public class DamageTextManager
    : MonoBehaviour
{
    private static DamageTextManager instance;
    private static bool isQuitting = false;

    [Header("출력")]
    [Tooltip("데미지 텍스트 출력 여부 (모든 오브젝트에 일괄 적용)")]
    [SerializeField] private bool isEnabled = true;
    [Tooltip("텍스트 색")]
    [SerializeField] private Color textColor = Color.white;

    [Header("텍스트")]
    [Tooltip("폰트 (비어있으면 TMP 기본 폰트)")]
    public TMP_FontAsset font;
    public float fontSize = 36f;
    public FontStyles fontStyle = FontStyles.Bold;
    [Tooltip("데미지 숫자 포맷 (예 : \"0\" 정수, \"0.#\" 소수 첫째 자리)")]
    public string numberFormat = "0";

    [Header("연출")]
    [Tooltip("표시 시간 (초)")]
    public float duration = 0.8f;
    [Tooltip("대상 위치에 더할 월드 오프셋 (예 : 머리 위)")]
    public Vector3 worldOffset = new Vector3(0f, 1f, 0f);
    [Tooltip("표시되는 동안 위로 떠오르는 월드 거리")]
    public float worldRise = 1f;
    [Tooltip("겹침 방지를 위한 가로 랜덤 오프셋 (월드, ±)")]
    public float randomOffsetX = 0.3f;
    [Tooltip("시간(0~1)에 따른 떠오르는 비율(0~1)")]
    public AnimationCurve riseCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [Tooltip("시간(0~1)에 따른 알파(0~1)")]
    public AnimationCurve fadeCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.6f, 1f), new Keyframe(1f, 0f));

    /// <summary>
    /// 출력 여부가 바뀔 때 호출 (설정 창 토글 동기화용)
    /// </summary>
    public event Action<bool> OnEnabledChanged;

    private readonly Stack<DamageTextUI> pool = new Stack<DamageTextUI>();
    private readonly List<DamageTextUI> actives = new List<DamageTextUI>();

    // 인스펙터에서 isEnabled 를 바꿨을 때 변경을 감지하기 위한 값
    private bool appliedEnabled = true;

    public static DamageTextManager Instance
    {
        get
        {
            if (null == instance)
            {
                instance = FindFirstObjectByType<DamageTextManager>();
            }
            // 게임 종료 중에는 새로 만들지 않음
            if (null == instance && false == isQuitting)
            {
                new GameObject(nameof(DamageTextManager)).AddComponent<DamageTextManager>();
            }
            return instance;
        }
    }

    #region Public API

    /// <summary>
    /// 데미지 텍스트 출력 여부 (모든 오브젝트에 일괄 적용)
    /// </summary>
    public bool IsEnabled
    {
        get => isEnabled;
        set
        {
            isEnabled = value;
            ApplyEnabled();
        }
    }

    public Color TextColor
    {
        get => textColor;
        set => textColor = value;
    }

    /// <summary>
    /// 데미지 텍스트 출력
    /// </summary>
    /// <param name="_amount">실제로 받은 데미지 양</param>
    /// <param name="_target">텍스트가 뜰 위치 (출력 시점의 위치에 고정)</param>
    /// <param name="_screen">그려질 화면 (Auto 면 대상의 레이어로 결정)</param>
    public void Show(float _amount, Transform _target, UI_SCREEN _screen)
    {
        if (false == isEnabled || _amount <= 0f || null == _target)
        {
            return;
        }

        int targetLayer = _target.gameObject.layer;
        if (UI_SCREEN.Auto == _screen)
        {
            _screen = UICanvasProvider.ResolveScreen(targetLayer);
        }

        Vector3 worldPos = _target.position + worldOffset;
        worldPos.x += UnityEngine.Random.Range(-randomOffsetX, randomOffsetX);

        Camera worldCamera = UICanvasProvider.GetCameraForLayer(targetLayer);

        DamageTextUI ui = Get();
        actives.Add(ui);
        ui.Play(_screen, worldCamera, worldPos, _amount.ToString(numberFormat), textColor);
    }

    /// <summary>
    /// 떠 있는 모든 데미지 텍스트를 즉시 제거
    /// </summary>
    public void ClearAll()
    {
        // Release 에서 actives 가 바뀌므로 복사본 사용
        DamageTextUI[] targets = actives.ToArray();
        foreach (DamageTextUI ui in targets)
        {
            if (null != ui)
            {
                Release(ui);
            }
        }
        actives.Clear();
    }

    #endregion

    /// <summary>
    /// 표시가 끝난 텍스트를 풀로 반환 (DamageTextUI 가 호출)
    /// </summary>
    public void Release(DamageTextUI _ui)
    {
        actives.Remove(_ui);
        if (null == _ui)
        {
            return;
        }

        _ui.Stop();
        // 씬 전환 때 Canvas 와 함께 파괴되지 않도록 매니져 아래에 보관
        _ui.transform.SetParent(transform, false);
        pool.Push(_ui);
    }

    private DamageTextUI Get()
    {
        while (0 < pool.Count)
        {
            DamageTextUI pooled = pool.Pop();
            if (null != pooled)
            {
                return pooled;
            }
        }
        return DamageTextUI.Create(this);
    }

    private void ApplyEnabled()
    {
        if (appliedEnabled == isEnabled)
        {
            return;
        }
        appliedEnabled = isEnabled;

        if (false == isEnabled)
        {
            ClearAll();
        }
        OnEnabledChanged?.Invoke(isEnabled);
    }

    private void Awake()
    {
        if (null != instance && this != instance)
        {
            Debug.LogWarning($"[DamageTextManager] 매니져가 여러 개입니다. ({instance.name}, {name}) 먼저 등록된 것을 사용합니다.");
            Destroy(this);
            return;
        }

        instance = this;
        appliedEnabled = isEnabled;

        // DontDestroyOnLoad 는 루트 오브젝트만 가능
        transform.SetParent(null, false);
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        // 씬 전환으로 Canvas 와 함께 파괴된 텍스트 정리
        actives.RemoveAll(ui => null == ui);
    }

    private void OnDestroy()
    {
        if (this == instance)
        {
            instance = null;
        }
    }

    private void OnApplicationQuit()
    {
        isQuitting = true;
    }

    // Enter Play Mode 옵션으로 도메인 리로드를 끈 경우를 위해 static 초기화
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
        isQuitting = false;
    }

#if UNITY_EDITOR
    // 플레이 중 인스펙터에서 isEnabled 를 바꿔도 적용
    private void OnValidate()
    {
        duration = Mathf.Max(0.01f, duration);
        randomOffsetX = Mathf.Max(0f, randomOffsetX);

        if (true == Application.isPlaying && this == instance)
        {
            ApplyEnabled();
        }
    }
#endif
}
