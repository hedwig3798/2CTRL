using System;
using System.Reflection;
using UnityEngine;


public class DamagePipeline : MonoBehaviour
{
    private IDamageable[] pipeline
        = new IDamageable[(int)DAMAGE_PIPELINE.END];

    public void ProcessDamage(ref DamageMassage _msg)
    {
        foreach (IDamageable damageable in pipeline)
        {
            if (null == damageable)
            {
                continue;
            }
            damageable.ProcessDamage(ref _msg);
        }
    }

    private void Awake()
    {
        // 파이프라인 초기화
        for (int i = 0; i < (int)DAMAGE_PIPELINE.END; ++i)
        {
            pipeline[i] = null;
        }

        // 파이프라인 enum 값 가져오기
        DAMAGE_PIPELINE[] enumValues
            = (DAMAGE_PIPELINE[])Enum.GetValues(typeof(DAMAGE_PIPELINE));

        // enum 값에 따른 속성 가져오기
        for (int i = 0; i < enumValues.Length - 1; ++i)
        {
            // 현재 enum 값
            DAMAGE_PIPELINE currentType = enumValues[i];

            // Field 정보가 있는지 확인
            FieldInfo fieldInfo = currentType.GetType().GetField(currentType.ToString());
            if (null == fieldInfo)
            {
                continue;
            }

            // PipelineComponentAttribute 가 있는지 확인
            PipelineAttribute attribute
                = (PipelineAttribute)Attribute.GetCustomAttribute(
                        fieldInfo
                        , typeof(PipelineAttribute)
                    );
            if (null == attribute)
            {
                continue;
            }

            // 타입이 정해져 있는지 확인
            Type targetType = attribute.targetType;
            if (null == targetType)
            {
                continue;
            }

            // 그 타입의 컴포넌트 가져오기
            // null 이여도 어차피 null 저장하면 된다.
            Component component = GetComponent(targetType);

            // IDamageable 인터페이스로 캐스팅 해서 넣기
            pipeline[(int)currentType] = component as IDamageable;
        }
    }
}
