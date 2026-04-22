using UnityEngine;

/// <summary>
/// 외부 클래스에 의해 초기화 될 수 있는 컴포넌트
/// </summary>
public interface Initializable
{
    void Initialize(BlackBoard _data);
}
