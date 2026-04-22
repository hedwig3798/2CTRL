using System.Collections.Generic;
using UnityEngine;

public enum DATA_TYPE
{
    // MovementInitData
    moveTarget,
    moveSpeedRate,

    // HealthInitData
    HPRate,
}

public class BlackBoard
{
    #region Dictionary
    private Dictionary<DATA_TYPE, float> floatDict = new Dictionary<DATA_TYPE, float>();
    private Dictionary<DATA_TYPE, Transform> transformDict = new Dictionary<DATA_TYPE, Transform>();
    #endregion

    public void ClearBoard()
    {
        floatDict.Clear();
        transformDict.Clear();
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
    #endregion

    #region Getter
    public float GetFloat(DATA_TYPE _type)
    {
        return floatDict[_type];
    }
    public Transform GetTransform(DATA_TYPE _type)
    {
        if (transformDict.ContainsKey(_type))
        {
            return transformDict[_type];
        }
        return null;
    }
    #endregion
}
