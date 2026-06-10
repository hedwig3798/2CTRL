using System;
using System.Linq;
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
        // 이미 최대 레벨인 경우 무시
        if (currLevel == levelTable.maxLevel)
        {
            return;
        }

        // 경험치 증가
        currExp += _exp;

        // 레벨업
        int levelUpAmount = 0;

        // 현재 경험치가 최대 경험치를 넘긴 경우
        while (currExp >= maxExp)
        {
            // 레벨업 계산 처리
            currExp -= maxExp;
            levelUpAmount++;
            currLevel++;

            // 레벨업 후 최대 레벨에 달성한 경우 탈출
            if (currLevel == levelTable.maxLevel)
            {
                break;
            }

            // 현재 레벨 데이터가 테이블에 없는 경우 가장 마지막 수치로
            if (currLevel >= levelTable.expTable.Length)
            {
                maxExp = levelTable.expTable.Last();
            }
            else
            {
                maxExp = levelTable.expTable[currLevel];
            }
        }

        // 레벨업 처리
        if (0 < levelUpAmount)
        {
            levelUpAction?.Invoke(gameObject, levelUpAmount);
        }
    }
}
