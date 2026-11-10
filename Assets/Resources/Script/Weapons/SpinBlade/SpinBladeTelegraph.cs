using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 회전 칼날의 예고 연출 (무기 오브젝트 자식에 붙인다. 발사체는 발동 순간에야 생기므로 무기 쪽에서 쿨타임을 읽는다)
/// 다음 발동까지 telegraphDuration 초 이하로 남으면 칼날이 플레이어 몸에서 양쪽으로 천천히 나오고, 그 전에는 숨긴다
/// - 칼날 거리 / 각도는 무기가 발사할 SpinBladeProjectile 프리팹 값을 읽어 회전 시작 위치와 맞춘다
/// - 발동하면 쿨타임이 다시 차서 같은 프레임에 숨겨지고 발사체의 칼날로 이어진다
/// - 숨길 때는 렌더러만 끈다 (무기가 꺼지는 도중에도 안전하게)
/// </summary>
public class SpinBladeTelegraph
    : MonoBehaviour
{
    [Header("reference")]
    [Tooltip("쿨타임을 읽을 무기. 비워두면 부모에서 찾는다")]
    [SerializeField]
    private Weapon weapon;
    [Tooltip("예고용 칼날 Transform (이 오브젝트의 자식, 스프라이트의 +X 가 바깥쪽)")]
    [SerializeField]
    private Transform[] blades;

    [Header("telegraph")]
    [Tooltip("발동 몇 초 전부터 칼날이 나오기 시작하는지")]
    [Min(0.01f)]
    [SerializeField]
    private float telegraphDuration = 1.5f;
    [Tooltip("칼날이 나오기 시작하는 거리 (플레이어 중심 기준). 끝나는 거리는 발사체의 bladeRadius")]
    [Min(0f)]
    [SerializeField]
    private float startRadius = 0f;

    [Header("sound")]
    [Tooltip("예고 칼날이 나오기 시작할 때 (숨김 → 표시)")]
    [SerializeField]
    private SoundId tellSound = SoundId.WPN_SPINBLADE_TELL;

    [Header("visual")]
    [Tooltip("칼날 렌더러 정렬 레이어 (Default 면 BG 에 가려진다). 비워두면 바꾸지 않는다")]
    [SerializeField]
    private string sortingLayerName = "attack";

    private readonly List<Renderer> bladeRenderers = new List<Renderer>();
    private SpinBladeProjectile spinProjectile;
    private bool isVisible;

    /// <summary>
    /// 예고 칼날이 보이는 중인지
    /// </summary>
    public bool IsVisible => isVisible;

    private void Awake()
    {
        if (null == weapon)
        {
            weapon = GetComponentInParent<Weapon>(true);
        }
        if (null != weapon)
        {
            spinProjectile = weapon.projectile as SpinBladeProjectile;
        }

        CacheRenderers();
        ApplyVisible(false);
    }

    private void CacheRenderers()
    {
        if (null == blades)
        {
            return;
        }

        foreach (Transform blade in blades)
        {
            if (null != blade)
            {
                bladeRenderers.AddRange(blade.GetComponentsInChildren<Renderer>(true));
            }
        }

        if (true == string.IsNullOrEmpty(sortingLayerName))
        {
            return;
        }

        foreach (Renderer r in bladeRenderers)
        {
            r.sortingLayerName = sortingLayerName;
        }
    }

    private void LateUpdate()
    {
        if (false == TryGetProgress(out float progress))
        {
            SetVisible(false);
            return;
        }

        SetVisible(true);

        Transform origin = null != weapon.owner ? weapon.owner : weapon.transform;
        float radius = Mathf.Lerp(startRadius, spinProjectile.BladeRadius, progress);
        SpinBladeProjectile.PlaceBlades(blades, origin.position, spinProjectile.StartAngle, radius);
    }

    /// <summary>
    /// 예고 구간이면 진행도(0 ~ 1)를 돌려준다. 공격하지 않는 무기 / 쿨타임이 충분히 남은 때는 false
    /// </summary>
    private bool TryGetProgress(out float _progress)
    {
        _progress = 0f;

        if (null == weapon || null == spinProjectile || false == weapon.isActiveAndEnabled || WEAPON_TYPE.Repeat != weapon.weaponType)
        {
            return false;
        }

        float coolTime = weapon.FinalCoolTime;
        if (coolTime <= 0f)
        {
            return false;
        }

        float duration = Mathf.Min(telegraphDuration, coolTime);
        float remaining = weapon.RemainingCoolTime;
        if (remaining > duration)
        {
            return false;
        }

        _progress = 1f - remaining / duration;
        return true;
    }

    private void SetVisible(bool _visible)
    {
        if (_visible == isVisible)
        {
            return;
        }

        ApplyVisible(_visible);
        if (true == _visible && null != weapon)
        {
            PlayerStats stats = weapon.OwnerStats;
            SoundManager.Play(tellSound, null != stats ? stats.Side : SoundLayerSide.GetSide(weapon.gameObject.layer));
        }
    }

    private void ApplyVisible(bool _visible)
    {
        isVisible = _visible;
        foreach (Renderer r in bladeRenderers)
        {
            if (null != r)
            {
                r.enabled = _visible;
            }
        }
    }

    private void OnDisable()
    {
        SetVisible(false);
    }
}
