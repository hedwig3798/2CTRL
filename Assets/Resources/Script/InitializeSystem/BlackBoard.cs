using System.Collections.Generic;
using UnityEngine;

public enum DATA_TYPE
{
    // position
    startPosition,
    startPoint,     // vector (startPosition 대신 쓰는 시작 지점. Projectile.LaunchAt 이 기록)

    // MovementInitData
    moveTarget,     // transform
    moveSpeedRate,  // float
    moveTargetPoint,// vector (moveTarget 에 랜덤 보정을 더한 실제 이동 지점)

    // HealthInitData
    HPRate,         // float

    // Attack
    damageRate,     // float

    // Item
    expRate,        // float

    // Formation
    formationIndex, // float
    formationCount, // float

    // Target
    targetLayer,    // int (LayerMask value)

    // Chain
    chainCount,     // int
    chainRange,     // float

    // Hit
    hitCount,       // int

    // Lifetime
    lifetime,       // float (0 이하는 컴포넌트 기본값)

    // Area
    areaRadius,     // float
}

public class BlackBoard
{
    #region Dictionary
    private Dictionary<DATA_TYPE, float> floatDict = new Dictionary<DATA_TYPE, float>();
    private Dictionary<DATA_TYPE, Transform> transformDict = new Dictionary<DATA_TYPE, Transform>();
    private Dictionary<DATA_TYPE, int> intDict = new Dictionary<DATA_TYPE, int>();
    private Dictionary<DATA_TYPE, Vector3> vectorDict = new Dictionary<DATA_TYPE, Vector3>();
    #endregion

    public DropManager dropManager;

    // 소환/분열처럼 런타임에 몬스터를 추가로 스폰할 때 쓴다
    public Spawner spawner;

    public void ClearBoard()
    {
        floatDict.Clear();
        transformDict.Clear();
        intDict.Clear();
        vectorDict.Clear();
    }

    #region Setter
    public void SetFloat(DATA_TYPE _type, float _val)
    {
        floatDict[_type] = _val;
    }

    public void SetTransform(DATA_TYPE _type, Transform _val)
    {
        transformDict[_type] = _val;
    }

    public void SetInt(DATA_TYPE _type, int _val)
    {
        intDict[_type] = _val;
    }

    public void SetVector(DATA_TYPE _type, Vector3 _val)
    {
        vectorDict[_type] = _val;
    }

    public void RemoveVector(DATA_TYPE _type)
    {
        vectorDict.Remove(_type);
    }
    #endregion

    #region Getter
    public float GetFloat(DATA_TYPE _type)
    {
        if (false == floatDict.ContainsKey(_type))
        {
            return 0.0f;
        }

        return floatDict[_type];
    }
    public Transform GetTransform(DATA_TYPE _type)
    {
        if (false == transformDict.ContainsKey(_type))
        {
            return null;
        }

        return transformDict[_type];
    }
    public int GetInt(DATA_TYPE _type)
    {
        if (false == intDict.ContainsKey(_type))
        {
            return 0;
        }

        return intDict[_type];
    }
    public bool TryGetVector(DATA_TYPE _type, out Vector3 _val)
    {
        return vectorDict.TryGetValue(_type, out _val);
    }

    /// <summary>
    /// 시작 지점. startPoint(vector) 가 있으면 그 값, 없으면 startPosition(transform) 의 위치
    /// 둘 다 없으면 false
    /// </summary>
    public bool TryGetStartPoint(out Vector3 _val)
    {
        if (true == vectorDict.TryGetValue(DATA_TYPE.startPoint, out _val))
        {
            return true;
        }

        Transform t = GetTransform(DATA_TYPE.startPosition);
        if (null == t)
        {
            return false;
        }

        _val = t.position;
        return true;
    }
    #endregion
}
