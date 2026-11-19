using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 플레이어가 아이템을 수집하는 컴포넌트
/// EXP 아이템을 수집하면 획득한 EXP 를 공용 ExpSystem 에 전달한다.
/// 특수 아이템(PowerUpItem) 을 수집하면 종류에 맞는 효과를 적용한다 (자석: 같은 진영 보석 끌어오기 / 무적: InvincibleBuff 켜기)
/// </summary>
public class ItemCollector 
    : MonoBehaviour
{
    [SerializeField]
    private GameObject owner;

    // 통계용 EXP 획득 귀속 (owner 우선, 없으면 부모에서 찾는다)
    private PlayerStats ownerStats;

    // 무적 아이템을 먹었을 때 켤 버프 (owner / PlayerStats 아래에서 찾는다, 없으면 무적 무시)
    private InvincibleBuff invincibleBuff;

    // 자석이 끌어올 보석 목록 버퍼 (매번 할당하지 않도록 재사용)
    private static readonly List<ExpItem> MagnetBuffer = new List<ExpItem>();

    private void Awake()
    {
        if (null != owner)
        {
            ownerStats = owner.GetComponentInParent<PlayerStats>();
        }
        if (null == ownerStats)
        {
            ownerStats = GetComponentInParent<PlayerStats>();
        }

        if (null != ownerStats)
        {
            invincibleBuff = ownerStats.GetComponentInChildren<InvincibleBuff>(true);
        }
        if (null == invincibleBuff && null != owner)
        {
            invincibleBuff = owner.GetComponentInChildren<InvincibleBuff>(true);
        }
    }

    private void OnTriggerEnter2D(Collider2D _other)
    {
        if (false == _other.CompareTag("item"))
        {
            return;
        }

        if (true == _other.TryGetComponent(out ExpItem expItem))
        {
            Absorb(expItem, ownerStats);
            return;
        }

        if (true == _other.TryGetComponent(out PowerUpItem powerUp))
        {
            ApplyPowerUp(powerUp);
        }
    }

    /// <summary>
    /// 특수 아이템을 수집하고 종류에 맞는 효과를 적용한다 (이미 수집됐으면 무시)
    /// </summary>
    private void ApplyPowerUp(PowerUpItem _item)
    {
        // 진영 레이어는 수집(비활성화) 전에 읽어 둔다
        int layer = _item.Layer;
        if (false == _item.Collect())
        {
            return;
        }

        switch (_item.Type)
        {
            case POWER_UP_TYPE.Magnet:
                Transform magnet = null != owner ? owner.transform : transform;
                PullGems(layer, magnet, _item.MagnetPullSpeed, _item.MagnetPullAcceleration);
                break;

            case POWER_UP_TYPE.Invincible:
                if (null == invincibleBuff)
                {
                    Debug.LogWarning("[ItemCollector] 플레이어에 InvincibleBuff 가 없어 무적 아이템을 적용하지 못했습니다.");
                    break;
                }
                invincibleBuff.Activate(_item.InvincibleDuration);
                break;
        }
    }

    /// <summary>
    /// 땅에 있는 EXP 보석 중 레이어가 _layer 인 것(같은 진영) 을 전부 _target 으로 끌어당긴다 (특수 아이템 "자석")
    /// 즉시 흡수하지 않고 날아오게 해, 도착하면 기존 트리거(OnTriggerEnter2D → Absorb) 로 흡수된다. 끌어당긴 개수를 돌려준다
    /// </summary>
    public static int PullGems(int _layer, Transform _target, float _maxSpeed, float _acceleration)
    {
        if (null == _target)
        {
            return 0;
        }

        ExpItem.CollectActive(MagnetBuffer);

        int count = 0;
        foreach (ExpItem gem in MagnetBuffer)
        {
            if (_layer != gem.gameObject.layer)
            {
                continue;
            }

            SlingshotMovement movement = gem.GetComponent<SlingshotMovement>();
            if (null == movement)
            {
                movement = gem.GetComponentInParent<SlingshotMovement>();
            }
            if (null == movement)
            {
                continue;
            }

            movement.Pull(_target, _maxSpeed, _acceleration);
            ++count;
        }

        MagnetBuffer.Clear();
        return count;
    }

    /// <summary>
    /// EXP 아이템을 수집해 (ExpSystem.ExpGainMultiplier 를 곱해) 공용 ExpSystem 에 넣고 _owner 진영으로 OnExpGained 를 알린다 (최종 보스 진입 시 강제 흡수에도 쓴다)
    /// 배율을 곱한 획득 EXP 를 돌려준다 (이미 수집됐으면 0)
    /// </summary>
    public static float Absorb(ExpItem _item, PlayerStats _owner)
    {
        if (null == _item)
        {
            return 0f;
        }

        float exp = _item.Collect();
        if (exp <= 0f)
        {
            return 0f;
        }

        ExpSystem expSystem = ExpSystem.Instance;
        if (null == expSystem)
        {
            Debug.LogWarning("[ItemCollector] 씬에 공용 ExpSystem 이 없습니다.");
            return 0f;
        }

        // 전체 EXP 획득 배율 적용 (통계 · 효과음 이벤트도 실제 획득량으로 알린다)
        exp *= expSystem.ExpGainMultiplier;
        expSystem.GetExp(exp);

        if (null != _owner && true == EventManager.HasInstance)
        {
            EventManager.Instance.OnExpGained?.Invoke(_owner.Side, exp);
        }
        return exp;
    }
}
