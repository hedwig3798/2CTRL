using UnityEngine;

/// <summary>
/// 특수 드랍 아이템의 종류
/// </summary>
public enum POWER_UP_TYPE
{
    Magnet = 0,     // 자기 진영 EXP 보석 전부 끌어오기

    Invincible,     // 일정 시간 무적
}

/// <summary>
/// 몬스터가 낮은 확률로 떨어뜨리는 특수 아이템 (자석 / 무적)
/// 스스로 효과를 내지 않고, ItemCollector 가 Collect 로 수집한 뒤 type 에 맞는 효과를 적용한다.
/// 프리팹은 EXP 보석과 같은 구성 (tag "item" + Spawnable + SlingshotMovement + BlackBoardHandler) 을 전제로 한다.
/// </summary>
public class PowerUpItem
    : MonoBehaviour
    , Initializable
{
    [Tooltip("수집하면 끌 오브젝트 (비워두면 이 오브젝트)")]
    [SerializeField]
    private GameObject owner;

    [Tooltip("아이템 종류")]
    [SerializeField]
    private POWER_UP_TYPE type = POWER_UP_TYPE.Magnet;

    [Tooltip("무적 지속 시간 (초). 다시 먹으면 남은 시간을 이 값으로 갱신한다")]
    [Min(0f)]
    [SerializeField]
    private float invincibleDuration = 5f;

    [Tooltip("자석으로 끌려오는 보석의 최대 속도 (보석의 SlingshotMovement.maxSpeed 보다 작으면 원래 값 사용)")]
    [Min(0f)]
    [SerializeField]
    private float magnetPullSpeed = 20f;

    [Tooltip("자석으로 끌려오는 보석의 가속도 (보석의 SlingshotMovement.acceleration 보다 작으면 원래 값 사용)")]
    [Min(0f)]
    [SerializeField]
    private float magnetPullAcceleration = 40f;

    private bool collected;

    public POWER_UP_TYPE Type => type;

    public float InvincibleDuration => invincibleDuration;

    public float MagnetPullSpeed => magnetPullSpeed;

    public float MagnetPullAcceleration => magnetPullAcceleration;

    /// <summary>
    /// 이 아이템의 레이어 (드랍한 DropManager 의 진영 레이어). 자석은 같은 레이어의 보석만 끌어온다
    /// </summary>
    public int Layer => gameObject.layer;

    private void OnEnable()
    {
        collected = false;
    }

    public void Initialize(BlackBoard _data)
    {
        collected = false;
    }

    /// <summary>
    /// 아이템을 수집하고 끈다. 처음 수집이면 true (이미 수집된 경우 false)
    /// </summary>
    public bool Collect()
    {
        if (true == collected)
        {
            return false;
        }

        collected = true;

        if (null != owner)
        {
            owner.SetActive(false);
        }
        else
        {
            gameObject.SetActive(false);
        }

        return true;
    }
}
