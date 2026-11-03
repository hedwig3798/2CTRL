using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 신규 무기 / 발사 방식 PlayMode 테스트용 조립 도우미 (프리팹 없이 코드로 구성)
/// </summary>
public static class WeaponTestKit
{
    private static readonly List<GameObject> created = new List<GameObject>();

    public static void Track(GameObject _go)
    {
        created.Add(_go);
    }

    public static Transform Marker(Vector2 _pos)
    {
        GameObject go = new GameObject("TestMarker");
        go.transform.position = _pos;
        created.Add(go);
        return go.transform;
    }

    /// <summary>
    /// 비활성 발사체 원본 (Instantiate 해도 비활성이라 Awake 가 풀에서 꺼낼 때까지 미뤄진다)
    /// </summary>
    public static T Template<T>(bool _blackBoard = false) where T : Projectile
    {
        GameObject go = new GameObject("TestProjectileTemplate_" + typeof(T).Name);
        go.SetActive(false);
        if (_blackBoard)
        {
            go.AddComponent<BlackBoardHandler>();
        }
        T p = go.AddComponent<T>();
        created.Add(go);
        return p;
    }

    public static Weapon Make(
        WEAPON_TYPE _type
        , Projectile _template
        , Transform _owner
        , float _damage = 10f
        , float _attackSpeed = 0f
        , float _range = 5f
        , int _fireCount = 1
        , int _layerMask = 1 << MonsterTestFactory.TargetLayer)
    {
        GameObject go = new GameObject("TestWeapon");
        go.SetActive(false);
        Weapon w = go.AddComponent<Weapon>();
        w.weaponType = _type;
        w.projectile = _template;
        w.owner = _owner;
        w.damage = _damage;
        w.attackSpeed = _attackSpeed;
        w.range = _range;
        w.fireCount = _fireCount;
        w.targetLayer = _layerMask;
        go.transform.position = null != _owner ? _owner.position : Vector3.zero;
        go.SetActive(true);
        created.Add(go);
        return w;
    }

    public static int CountActive<T>() where T : Object
    {
        return Object.FindObjectsByType<T>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Length;
    }

    public static T FirstActive<T>() where T : Object
    {
        T[] all = Object.FindObjectsByType<T>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        return all.Length > 0 ? all[0] : null;
    }

    public static float Hp(GameObject _go)
    {
        return _go.GetComponent<HealthSystem>().currHP;
    }

    public static void Cleanup()
    {
        // 풀 반환 콜백이 파괴 중에 돌지 않게 먼저 표시한다
        foreach (Projectile p in Object.FindObjectsByType<Projectile>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            p.SetPool(null);
        }
        foreach (Weapon w in Object.FindObjectsByType<Weapon>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            Object.DestroyImmediate(w.gameObject);
        }
        foreach (Projectile p in Object.FindObjectsByType<Projectile>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            Object.DestroyImmediate(p.gameObject);
        }
        foreach (GameObject go in created)
        {
            if (null != go)
            {
                Object.DestroyImmediate(go);
            }
        }
        created.Clear();
        MonsterTestFactory.Cleanup();
    }
}

/// <summary>
/// Weapon 발사 방식 검증용 발사체. 발사 횟수와 프레임을 기록하고, 발사 허용 여부를 바꿀 수 있다
/// </summary>
public class FireModeTestProjectile : Projectile
{
    public static bool Allow = true;
    public static int Attempts;
    public static readonly List<int> LaunchFrames = new List<int>();

    public static void ResetStatics()
    {
        Allow = true;
        Attempts = 0;
        LaunchFrames.Clear();
    }

    protected override bool OnLaunch()
    {
        ++Attempts;
        if (false == Allow)
        {
            return false;
        }
        LaunchFrames.Add(Time.frameCount);
        return true;
    }
}
