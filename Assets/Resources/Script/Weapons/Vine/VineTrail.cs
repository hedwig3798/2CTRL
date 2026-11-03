using UnityEngine;

/// <summary>
/// 덩굴 무기가 마지막으로 남긴 조각을 기억한다 (무기마다 하나)
/// 무기 오브젝트에 붙는다. 프리팹에 없으면 VineProjectile 이 처음 발사될 때 무기에 붙인다
/// </summary>
public class VineTrail
    : MonoBehaviour
{
    private VineProjectile lastSegment;

    /// <summary>
    /// 살아 있는 마지막 조각과 _position 의 거리가 _spacing 미만인지
    /// 마지막 조각이 사라졌거나 _self 자신(풀에서 다시 꺼낸 같은 조각)이면 false
    /// </summary>
    public bool IsTooClose(Vector2 _position, float _spacing, VineProjectile _self)
    {
        if (null == lastSegment || lastSegment == _self || false == lastSegment.gameObject.activeSelf)
        {
            return false;
        }

        return ((Vector2)lastSegment.transform.position - _position).sqrMagnitude < _spacing * _spacing;
    }

    /// <summary>
    /// 새로 생성된 조각을 마지막 조각으로 기록한다
    /// </summary>
    public void SetLast(VineProjectile _segment)
    {
        lastSegment = _segment;
    }

    private void OnDisable()
    {
        lastSegment = null;
    }
}
