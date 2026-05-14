using System.Collections.Generic;
using UnityEngine;

public enum DATA_TYPE
{
    // MovementInitData
    moveTarget,
    moveSpeedRate,

    // HealthInitData
    HPRate,

    // Attack
    damageRate,

}

public class BlackBoard
{
    #region Dictionary
    private Dictionary<DATA_TYPE, float> floatDict = new Dictionary<DATA_TYPE, float>();
    private Dictionary<DATA_TYPE, Transform> transformDict = new Dictionary<DATA_TYPE, Transform>();
    #endregion

    public DropManager dropManager;

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
        if (false == floatDict.ContainsKey(_type))
        {
            Debug.LogWarning($"{ToString()} has no {_type.ToString()}");
            return 0.0f;
        }

        return floatDict[_type];
    }
    public Transform GetTransform(DATA_TYPE _type)
    {
        if (false == transformDict.ContainsKey(_type))
        {
            Debug.LogWarning($"{ToString()} has no {_type.ToString()}");
            return null;
        }

        return transformDict[_type];
    }
    #endregion
}
