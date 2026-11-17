using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 레벨업 화면 중앙의 차원 무기 합성 카드 (결과 아이콘·이름·설명 + 재료 아이콘 2개)
/// - 레이아웃(RectTransform) 은 빌더가 만들고, 여기서는 직렬화 참조에 내용만 채운다
/// - 선택 상태는 왼쪽 커서 / 오른쪽 커서를 따로 표시한다 (LevelUpUI 가 SetCursor 로 지정)
/// - 클릭되면 OnClicked 로 클릭 위치 쪽 진영(카드 가운데 기준 왼쪽 / 오른쪽)만 알리고, 선택 여부는 LevelUpUI 가 결정한다
/// - 재료 아이콘은 씬의 DimensionApplier 차원 무기 보상 → fallbackRewards 순서로 재료 프리팹을 주는 보상의 이미지를 쓴다
/// </summary>
public class DimensionFusionCard
    : MonoBehaviour
    , IPointerClickHandler
{
    [Header("result")]
    [Tooltip("결과 차원 무기 아이콘")]
    [SerializeField]
    private Image resultIcon;

    [Tooltip("결과 차원 무기 이름")]
    [SerializeField]
    private TMP_Text nameText;

    [Tooltip("결과 차원 무기 설명")]
    [SerializeField]
    private TMP_Text explainText;

    [Header("materials")]
    [Tooltip("재료 A 아이콘")]
    [SerializeField]
    private Image materialIconA;

    [Tooltip("재료 B 아이콘")]
    [SerializeField]
    private Image materialIconB;

    [Tooltip("씬 차원에서 재료 아이콘을 찾지 못했을 때 찾아볼 무기 보상 목록")]
    [SerializeField]
    private RewardData[] fallbackRewards;

    [Header("selection")]
    [Tooltip("어느 한쪽이라도 카드를 고르면 켜는 강조 오브젝트 (없어도 됨)")]
    [SerializeField]
    private GameObject selectBackground;

    [Tooltip("왼쪽 플레이어가 카드를 고르면 켜는 커서 오브젝트")]
    [SerializeField]
    private GameObject leftCursor;

    [Tooltip("오른쪽 플레이어가 카드를 고르면 켜는 커서 오브젝트")]
    [SerializeField]
    private GameObject rightCursor;

    /// <summary>
    /// 카드가 클릭되었을 때 (클릭 위치 쪽 진영)
    /// </summary>
    public event Action<PlayerSide> OnClicked;

    /// <summary>
    /// 지금 표시 중인 합성법 (숨겼으면 null)
    /// </summary>
    public DimensionWeaponData Data { get; private set; }

    public bool IsLeftSelected { get; private set; }
    public bool IsRightSelected { get; private set; }

    private void OnEnable()
    {
        StringTable.OnLanguageChanged += HandleLanguageChanged;
        ApplyTexts();
        ApplyCursor();
    }

    private void OnDisable()
    {
        StringTable.OnLanguageChanged -= HandleLanguageChanged;
    }

    private void HandleLanguageChanged(Language _language)
    {
        ApplyTexts();
    }

    /// <summary>
    /// _data 합성법을 표시하고 카드를 켠다 (커서는 모두 해제)
    /// </summary>
    public void Show(DimensionWeaponData _data)
    {
        Data = _data;
        IsLeftSelected = false;
        IsRightSelected = false;

        gameObject.SetActive(true);

        ApplyTexts();
        SetIcon(resultIcon, null != _data ? _data.Icon : null);
        SetIcon(materialIconA, null != _data ? FindWeaponIcon(_data.MaterialA) : null);
        SetIcon(materialIconB, null != _data ? FindWeaponIcon(_data.MaterialB) : null);
        ApplyCursor();
    }

    /// <summary>
    /// 카드를 끄고 표시 중인 합성법을 비운다
    /// </summary>
    public void Hide()
    {
        Data = null;
        IsLeftSelected = false;
        IsRightSelected = false;
        ApplyCursor();
        gameObject.SetActive(false);
    }

    /// <summary>
    /// 왼쪽 / 오른쪽 커서 표시
    /// </summary>
    public void SetCursor(bool _left, bool _right)
    {
        IsLeftSelected = _left;
        IsRightSelected = _right;
        ApplyCursor();
    }

    public void OnPointerClick(PointerEventData _eventData)
    {
        if (null == Data)
        {
            return;
        }

        RectTransform rect = transform as RectTransform;
        PlayerSide side = PlayerSide.Left;
        if (null != rect && true == RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, _eventData.position, _eventData.pressEventCamera, out Vector2 local))
        {
            side = DimensionFusionCursor.SideFromClick(local.x, rect.rect.center.x);
        }
        OnClicked?.Invoke(side);
    }

    private void ApplyTexts()
    {
        if (null != nameText)
        {
            nameText.text = null != Data ? StringTable.Resolve(Data.DisplayNameKey) : string.Empty;
        }
        if (null != explainText)
        {
            explainText.text = null != Data ? StringTable.Resolve(Data.ExplainKey) : string.Empty;
        }
    }

    private void ApplyCursor()
    {
        if (null != leftCursor)
        {
            leftCursor.SetActive(IsLeftSelected);
        }
        if (null != rightCursor)
        {
            rightCursor.SetActive(IsRightSelected);
        }
        if (null != selectBackground)
        {
            selectBackground.SetActive(true == IsLeftSelected || true == IsRightSelected);
        }
    }

    private static void SetIcon(Image _image, Sprite _sprite)
    {
        if (null == _image)
        {
            return;
        }
        _image.sprite = _sprite;
        _image.enabled = null != _sprite;
    }

    /// <summary>
    /// 씬 차원(양쪽 진영)의 무기 보상 → fallbackRewards 순서로 _prefab 을 주는 보상의 이미지 (없으면 null)
    /// </summary>
    private Sprite FindWeaponIcon(Weapon _prefab)
    {
        if (null == _prefab)
        {
            return null;
        }

        foreach (DimensionApplier applier in FindObjectsByType<DimensionApplier>())
        {
            if (null == applier || null == applier.Dimension)
            {
                continue;
            }

            Sprite found = FindIcon(applier.Dimension.WeaponRewards, _prefab);
            if (null != found)
            {
                return found;
            }
        }
        return FindIcon(fallbackRewards, _prefab);
    }

    private static Sprite FindIcon(RewardData[] _rewards, Weapon _prefab)
    {
        if (null == _rewards)
        {
            return null;
        }

        foreach (RewardData reward in _rewards)
        {
            if (null != reward && true == reward.HasImage && reward.GetEffect() is WeaponRewardEffect effect && _prefab == effect.WeaponPrefab)
            {
                return reward.GetImage();
            }
        }
        return null;
    }
}
