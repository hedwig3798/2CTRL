using System;
using UnityEngine;

public class ExpSystem
    : MonoBehaviour
{
    public int currLevel;
    public float currExp;
    public float maxExp;
    public LevelTable levelTable;

    public Action<GameObject, int> levelUpAction;

    public void GetExp(float _exp)
    {
        if (null == levelTable || null == levelTable.expTable || 0 == levelTable.expTable.Length)
        {
            return;
        }

        if (currLevel >= levelTable.maxLevel)
        {
            return;
        }

        currExp += _exp;

        if (maxExp <= 0f)
        {
            maxExp = GetNeedExp(currLevel);
            if (maxExp <= 0f)
            {
                return;
            }
        }

        int levelUpAmount = 0;

        while (currExp >= maxExp && maxExp > 0f)
        {
            currExp -= maxExp;
            levelUpAmount++;
            currLevel++;

            if (currLevel >= levelTable.maxLevel)
            {
                currLevel = levelTable.maxLevel;
                break;
            }

            maxExp = GetNeedExp(currLevel);
        }

        if (0 < levelUpAmount)
        {
            levelUpAction?.Invoke(gameObject, levelUpAmount);
        }
    }

    private float GetNeedExp(int level)
    {
        if (level < 0)
        {
            level = 0;
        }

        if (level >= levelTable.expTable.Length)
        {
            return levelTable.expTable[levelTable.expTable.Length - 1];
        }

        return levelTable.expTable[level];
    }
}
