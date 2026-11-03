using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 톱날(Saw) 발사체 (무기는 WEAPON_TYPE.Wave)
/// 그 진영 카메라 시야의 왼쪽 또는 오른쪽 끝, 랜덤한 높이에서 생성되어 가로로 일직선 이동한다
/// - 카메라는 발사체 레이어를 cullingMask 에 포함한 카메라 (진영마다 다르다). 화면 경계는 매 프레임 다시 계산한다 (카메라가 플레이어를 따라 움직임)
/// - 관통. 판정 원(hitRadius * 크기) 에 닿은 적을 한 번씩만 타격한다
/// - 화면 밖(판정 반지름 + despawnPadding) 으로 나가면 소멸한다. 카메라가 톱날보다 빨라 화면을 못 벗어나는 경우를 위해 maxLifetime 안전장치가 있다
/// - 이동 / 소멸은 이 컴포넌트가 직접 처리한다 (Lifetime / 이동 컴포넌트를 붙이지 않는다)
/// - 업그레이드 : 크기 = weapon.GetParam(ProjectileScale, 1), 속도 = moveSpeed * 무기 speedRate (업그레이드되면 날아가던 톱날도 다시 읽는다)
/// - ★ FlagSawBounce : 화면 경계에 닿으면 화면 안쪽 랜덤한 방향으로 튕기고 소멸하지 않는다. 튕길 때마다 타격 기록 초기화 (날아가던 톱날도 즉시 적용)
/// </summary>
public class SawProjectile
    : Projectile
{
    [Header("saw")]
    [Tooltip("기본 이동 속도 (유닛/초). 실제 속도 = moveSpeed * 무기 speedRate (0 이하면 1)")]
    [Min(0f)]
    [SerializeField]
    private float moveSpeed = 7f;
    [Tooltip("크기 1 일 때의 판정 반지름")]
    [Min(0.01f)]
    [SerializeField]
    private float hitRadius = 0.5f;
    [Tooltip("생성 높이를 화면 위 / 아래 끝에서 이 비율만큼 안쪽으로 제한한다 (화면 높이 기준)")]
    [Range(0f, 0.5f)]
    [SerializeField]
    private float spawnHeightPadding = 0.05f;
    [Tooltip("화면 밖으로 판정 반지름 + 이 거리만큼 나가면 소멸")]
    [Min(0f)]
    [SerializeField]
    private float despawnPadding = 0.5f;
    [Tooltip("튕김이 없을 때의 최대 수명 (초). 카메라가 톱날보다 빨리 움직여 화면을 벗어나지 못하는 경우의 안전장치. 0 이면 무제한")]
    [Min(0f)]
    [SerializeField]
    private float maxLifetime = 15f;

    [Header("bounce (★)")]
    [Tooltip("튕길 때 향하는 화면 안쪽 지점의 범위. 뷰포트 기준으로 이 비율만큼 안쪽 사각형 안의 랜덤한 지점을 향한다 (0.5 면 화면 중앙)")]
    [Range(0f, 0.5f)]
    [SerializeField]
    private float bounceTargetPadding = 0.2f;

    [Header("visual")]
    [Tooltip("크기를 바꿀 대상. 비워두면 이 오브젝트")]
    [SerializeField]
    private Transform scaleTarget;
    [Tooltip("회전시킬 대상. 비워두면 이 오브젝트")]
    [SerializeField]
    private Transform spinTarget;
    [Tooltip("회전 속도 (도/초)")]
    [SerializeField]
    private float spinSpeed = 720f;
    [Tooltip("정렬 레이어를 맞출 렌더러. 비워두면 자식의 모든 렌더러")]
    [SerializeField]
    private Renderer[] renderers;
    [Tooltip("렌더러 정렬 레이어 (Default 면 BG 에 가려진다). 비워두면 바꾸지 않는다")]
    [SerializeField]
    private string sortingLayerName = "attack";

    [Header("camera")]
    [Tooltip("카메라를 찾지 못했을 때 다시 찾는 간격 (초)")]
    [Min(0.05f)]
    [SerializeField]
    private float cameraRetryInterval = 1f;

    private HashSet<Transform> hitTargets = new HashSet<Transform>();
    private List<Transform> hitBuffer = new List<Transform>();

    private Vector2 direction = Vector2.right;
    private float launchTime;
    private float currScale = 1f;
    private Vector3 baseScale = Vector3.one;
    private int cachedUpgradeVersion = -1;

    private Camera cachedCamera;
    private int cachedLayer = -1;
    private float nextCameraSearchTime;

    /// <summary>
    /// 현재 크기에 맞춘 판정 반지름
    /// </summary>
    public float CurrentRadius => hitRadius * currScale;

    /// <summary>
    /// 현재 이동 방향 (정규화)
    /// </summary>
    public Vector2 Direction => direction;

    private float CurrentSpeed
    {
        get
        {
            float rate = (null != weapon && weapon.speedRate > 0f) ? weapon.speedRate : 1f;
            return moveSpeed * rate;
        }
    }

    private void Awake()
    {
        if (null == scaleTarget)
        {
            scaleTarget = transform;
        }
        if (null == spinTarget)
        {
            spinTarget = transform;
        }
        baseScale = scaleTarget.localScale;

        ApplySortingLayer();
    }

    private void ApplySortingLayer()
    {
        if (true == string.IsNullOrEmpty(sortingLayerName))
        {
            return;
        }

        if (null == renderers || 0 == renderers.Length)
        {
            renderers = GetComponentsInChildren<Renderer>(true);
        }

        foreach (Renderer r in renderers)
        {
            if (null != r)
            {
                r.sortingLayerName = sortingLayerName;
            }
        }
    }

    /// <summary>
    /// 화면 왼쪽 / 오른쪽 끝 중 랜덤한 쪽, 랜덤한 높이에서 반대쪽을 향해 출발한다
    /// 카메라를 찾지 못하면 발사하지 않는다
    /// </summary>
    protected override bool OnLaunch()
    {
        hitTargets.Clear();
        hitBuffer.Clear();

        Camera cam = GetCamera();
        if (null == cam)
        {
            return false;
        }

        RefreshUpgrade(true);

        float z = weapon.transform.position.z;
        GetViewBounds(cam, z, out Vector2 min, out Vector2 max);

        float radius = CurrentRadius;
        float padY = (max.y - min.y) * spawnHeightPadding;
        float lowY = min.y + padY + radius;
        float highY = max.y - padY - radius;
        if (lowY > highY)
        {
            lowY = highY = (min.y + max.y) * 0.5f;
        }

        // 화면 밖에서 판정 반지름만큼 걸친 채로 시작해 안쪽으로 들어온다
        bool fromLeft = Random.value < 0.5f;
        float x = fromLeft ? min.x - radius : max.x + radius;
        transform.position = new Vector3(x, Random.Range(lowY, highY), z);
        direction = fromLeft ? Vector2.right : Vector2.left;
        launchTime = Time.time;
        return true;
    }

    private void Update()
    {
        if (null == weapon)
        {
            return;
        }

        RefreshUpgrade(false);

        float dt = Time.deltaTime;
        transform.position += (Vector3)(direction * (CurrentSpeed * dt));
        spinTarget.Rotate(0f, 0f, spinSpeed * dt);

        bool isBounce = weapon.HasFlag(WeaponParam.FlagSawBounce);

        Camera cam = GetCamera();
        if (null != cam)
        {
            GetViewBounds(cam, transform.position.z, out Vector2 min, out Vector2 max);
            if (true == isBounce)
            {
                TryBounce(min, max);
            }
            else if (true == IsOutside(min, max))
            {
                gameObject.SetActive(false);
                return;
            }
        }

        if (false == isBounce && maxLifetime > 0f && Time.time - launchTime >= maxLifetime)
        {
            gameObject.SetActive(false);
            return;
        }

        HitOverlapping();
    }

    /// <summary>
    /// 판정 원 안의 아직 타격하지 않은 적을 모두 한 번씩 타격한다
    /// </summary>
    private void HitOverlapping()
    {
        Vector2 center = transform.position;
        int count = FindAll(center, CurrentRadius, hitBuffer, hitTargets);
        if (count > 0)
        {
            // 데미지 업그레이드 / 플레이어 패시브를 살아 있는 톱날에도 반영한다
            RefreshDamage();
        }
        for (int i = 0; i < count; ++i)
        {
            Transform t = hitBuffer[i];
            hitTargets.Add(t);
            if (t.TryGetComponent(out DamagePipeline dp))
            {
                SendDamage(dp, center);
            }
        }
        hitBuffer.Clear();
    }

    /// <summary>
    /// 바깥쪽으로 움직이는 중에 화면 경계에 닿으면 화면 안쪽 랜덤한 지점을 향해 튕기고 타격 기록을 지운다
    /// 화면 밖에 있어도 바깥쪽으로 움직이고 있으면 튕겨서 화면 안으로 돌아온다
    /// </summary>
    private void TryBounce(Vector2 _min, Vector2 _max)
    {
        Vector2 pos = transform.position;
        float r = CurrentRadius;

        bool hitX = (pos.x + r >= _max.x && direction.x > 0f) || (pos.x - r <= _min.x && direction.x < 0f);
        bool hitY = (pos.y + r >= _max.y && direction.y > 0f) || (pos.y - r <= _min.y && direction.y < 0f);
        if (false == hitX && false == hitY)
        {
            return;
        }

        Vector2 point = new Vector2(
            Mathf.Lerp(_min.x, _max.x, Random.Range(bounceTargetPadding, 1f - bounceTargetPadding))
            , Mathf.Lerp(_min.y, _max.y, Random.Range(bounceTargetPadding, 1f - bounceTargetPadding))
        );

        Vector2 toPoint = point - pos;
        direction = toPoint.sqrMagnitude > 0.0001f ? toPoint.normalized : -direction;
        hitTargets.Clear();
    }

    private bool IsOutside(Vector2 _min, Vector2 _max)
    {
        Vector2 pos = transform.position;
        float margin = CurrentRadius + despawnPadding;
        return pos.x < _min.x - margin || pos.x > _max.x + margin
            || pos.y < _min.y - margin || pos.y > _max.y + margin;
    }

    /// <summary>
    /// 업그레이드가 바뀌었으면 크기를 다시 맞춘다
    /// </summary>
    private void RefreshUpgrade(bool _force)
    {
        if (false == _force && cachedUpgradeVersion == weapon.UpgradeVersion)
        {
            return;
        }

        cachedUpgradeVersion = weapon.UpgradeVersion;
        currScale = Mathf.Max(0.01f, weapon.GetParam(WeaponParam.ProjectileScale, 1f));
        scaleTarget.localScale = baseScale * currScale;
    }

    /// <summary>
    /// 카메라 시야의 월드 좌표 사각형 (분할 화면이면 그 카메라의 영역 기준)
    /// </summary>
    private static void GetViewBounds(Camera _cam, float _z, out Vector2 _min, out Vector2 _max)
    {
        float depth = Mathf.Abs(_z - _cam.transform.position.z);
        Vector3 a = _cam.ViewportToWorldPoint(new Vector3(0f, 0f, depth));
        Vector3 b = _cam.ViewportToWorldPoint(new Vector3(1f, 1f, depth));
        _min = new Vector2(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y));
        _max = new Vector2(Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
    }

    // 레이어는 생성 후 Weapon 이 바꾸므로, 레이어가 바뀌었을 때만 다시 찾는다
    // 못 찾은 것도 기억해 cameraRetryInterval 마다만 다시 찾는다 (Camera.allCameras 가 매번 배열을 할당한다)
    private Camera GetCamera()
    {
        bool isLayerChanged = cachedLayer != gameObject.layer;
        if (false == isLayerChanged)
        {
            if (null != cachedCamera)
            {
                return cachedCamera;
            }
            if (Time.time < nextCameraSearchTime)
            {
                return null;
            }
        }

        cachedLayer = gameObject.layer;
        cachedCamera = FindCameraForLayer(cachedLayer);
        if (null == cachedCamera)
        {
            nextCameraSearchTime = Time.time + cameraRetryInterval;
        }
        return cachedCamera;
    }

    // 분할 화면에서 각 진영 발사체가 자기 카메라를 기준으로 삼도록 (WFCChunkLoader / DespawnOutOfCamera 와 동일한 방식)
    private static Camera FindCameraForLayer(int _layer)
    {
        int layerBit = 1 << _layer;
        foreach (Camera cam in Camera.allCameras)
        {
            if (0 != (cam.cullingMask & layerBit))
            {
                return cam;
            }
        }
        return Camera.main;
    }

    protected override void OnDisable()
    {
        hitTargets.Clear();
        hitBuffer.Clear();
        direction = Vector2.right;
        cachedUpgradeVersion = -1;

        base.OnDisable();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, hitRadius * (Application.isPlaying ? currScale : 1f));
    }
}
