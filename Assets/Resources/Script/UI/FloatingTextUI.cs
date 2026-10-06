using System;
using TMPro;
using UnityEngine;

/// <summary>
/// 월드 위치에 떠올랐다가 사라지는 텍스트 하나 (데미지, 회복량 등)
/// Canvas 없이 3D TextMeshPro 로 그리며, 대상과 같은 레이어로 설정해 해당 카메라에만 보이게 한다.
/// 끝나면 비활성화되고 onFinished 로 풀에 반환된다.
///
/// 타 스크립트 사용 예)
///   FloatingTextUI ui = FloatingTextUI.Create(container);
///   ui.Play("12", Color.white, worldPos, layer, 4f, 0.8f, 1f, Release);
/// </summary>
public class FloatingTextUI
    : MonoBehaviour
{
    // 숫자가 잘리지 않을 정도의 넉넉한 크기 (Overflow 로 표시)
    private static readonly Vector2 TEXT_SIZE = new Vector2(3f, 1f);
    // 스프라이트 위에 그려지도록
    private const int SORTING_ORDER = 1000;

    [Tooltip("시간(0~1)에 따른 떠오르는 비율(0~1)")]
    public AnimationCurve riseCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [Tooltip("시간(0~1)에 따른 알파(0~1)")]
    public AnimationCurve fadeCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.6f, 1f), new Keyframe(1f, 0f));

    private TextMeshPro text;

    private Vector3 startWorldPos;
    private Color baseColor;
    private float duration;
    private float rise;
    private float elapsed;
    private Action<FloatingTextUI> onFinished;

    /// <summary>
    /// 코드로 텍스트 오브젝트 생성 (비활성 상태)
    /// </summary>
    public static FloatingTextUI Create(Transform _parent)
    {
        GameObject go = new GameObject(nameof(FloatingTextUI), typeof(RectTransform), typeof(TextMeshPro), typeof(FloatingTextUI));
        go.SetActive(false);
        go.transform.SetParent(_parent, false);

        FloatingTextUI ui = go.GetComponent<FloatingTextUI>();
        ui.text = go.GetComponent<TextMeshPro>();
        ui.text.rectTransform.sizeDelta = TEXT_SIZE;
        ui.text.alignment = TextAlignmentOptions.Center;
        ui.text.textWrappingMode = TextWrappingModes.NoWrap;
        ui.text.overflowMode = TextOverflowModes.Overflow;
        ui.text.fontStyle = FontStyles.Bold;
        ui.text.sortingOrder = SORTING_ORDER;

        return ui;
    }

    /// <summary>
    /// 출력 시작
    /// </summary>
    /// <param name="_layer">그려질 레이어 (대상의 레이어를 넘기면 해당 카메라에만 보임)</param>
    /// <param name="_duration">표시 시간 (초)</param>
    /// <param name="_rise">표시되는 동안 위로 떠오르는 월드 거리</param>
    /// <param name="_onFinished">표시가 끝났을 때 호출 (풀 반환용)</param>
    public void Play(string _text, Color _color, Vector3 _worldPos, int _layer, float _fontSize, float _duration, float _rise, Action<FloatingTextUI> _onFinished)
    {
        LayerUtils.SetLayer(gameObject, _layer);

        text.text = _text;
        text.fontSize = _fontSize;

        startWorldPos = _worldPos;
        baseColor = _color;
        duration = Mathf.Max(0.01f, _duration);
        rise = _rise;
        elapsed = 0f;
        onFinished = _onFinished;

        gameObject.SetActive(true);
        UpdateVisual(0f);
    }

    /// <summary>
    /// 출력 중지 (onFinished 는 호출하지 않음)
    /// </summary>
    public void Stop()
    {
        onFinished = null;
        gameObject.SetActive(false);
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / duration);
        UpdateVisual(t);

        if (1f <= t)
        {
            Action<FloatingTextUI> finished = onFinished;
            onFinished = null;
            gameObject.SetActive(false);
            finished?.Invoke(this);
        }
    }

    private void UpdateVisual(float _t)
    {
        Color color = baseColor;
        color.a *= Mathf.Clamp01(fadeCurve.Evaluate(_t));
        text.color = color;

        transform.position = startWorldPos + Vector3.up * (rise * riseCurve.Evaluate(_t));
    }
}
