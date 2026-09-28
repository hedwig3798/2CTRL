using UnityEngine;

/// <summary>
/// 기준 카메라에서 일정 거리 이상 멀어지면 오브젝트를 비활성화한다.
/// 비활성화되면 Spawnable / Projectile 의 OnDisable 에서 풀로 반환된다.
/// </summary>
public class DespawnOutOfCamera
    : MonoBehaviour
{
    [Header("despawn")]
    [Tooltip("카메라 중심(x, y)에서 이 거리를 넘으면 비활성화.\n몬스터는 SpawnData.spawnRange.y 보다 커야 스폰 직후 사라지지 않는다.")]
    [Min(0f)] public float despawnDistance = 30f;

    [Tooltip("거리 검사 주기(초)")]
    [Min(0f)] public float checkInterval = 0.25f;

    [Tooltip("비워두면 이 오브젝트의 레이어를 렌더링하는 카메라 -> Camera.main 순으로 찾는다")]
    public Camera targetCamera;

    private Camera cachedCamera;
    private int cachedLayer = -1;
    private float timer;

    private void OnEnable()
    {
        timer = 0f;
    }

    private void Update()
    {
        timer += Time.deltaTime;
        if (timer < checkInterval)
        {
            return;
        }
        timer = 0f;

        Camera cam = GetCamera();
        if (null == cam)
        {
            return;
        }

        Vector2 offset = transform.position - cam.transform.position;
        if (offset.sqrMagnitude > despawnDistance * despawnDistance)
        {
            gameObject.SetActive(false);
        }
    }

    // 레이어는 생성 후 Spawner / ProjectileWeapon 이 바꾸므로, 레이어가 바뀌었을 때만 다시 찾는다
    private Camera GetCamera()
    {
        if (null != targetCamera)
        {
            return targetCamera;
        }

        if (null == cachedCamera || cachedLayer != gameObject.layer)
        {
            cachedLayer = gameObject.layer;
            cachedCamera = FindCameraForLayer(cachedLayer);
        }
        return cachedCamera;
    }

    // 분할 화면에서 각 오브젝트가 자기 카메라를 기준으로 삼도록 (WFCChunkLoader 와 동일한 방식)
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
}
