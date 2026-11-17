/// <summary>
/// 레벨업 화면의 중앙 합성 카드 커서 상태 (순수 로직. LevelUpUI 가 키 입력·클릭을 넘겨 준다)
/// - 라운드를 열 때 Begin(합성 카드 유무) 로 시작하고, 두 커서는 각자 그룹(일반 카드)에서 시작한다
/// - 왼쪽은 D 로 카드, A 로 그룹 복귀 / 오른쪽은 ← 로 카드, → 로 그룹 복귀 (카드가 없으면 무시)
/// - 어느 한쪽이라도 카드에 있으면 재개 가능하고, 재개하면 합성만 적용한다 (다른 선택 무시)
/// </summary>
public class DimensionFusionCursor
{
    private bool hasCard;
    private bool leftOnCard;
    private bool rightOnCard;

    /// <summary>
    /// 이번 라운드에 합성 카드가 있는지
    /// </summary>
    public bool HasCard => hasCard;

    /// <summary>
    /// 어느 한쪽이라도 카드를 고른 상태인지
    /// </summary>
    public bool AnyOnCard => true == leftOnCard || true == rightOnCard;

    /// <summary>
    /// 재개할 때 합성 보상만 적용해야 하는지
    /// </summary>
    public bool ShouldApplyFusion => true == hasCard && true == AnyOnCard;

    /// <summary>
    /// 라운드 시작: 두 커서를 그룹으로 되돌린다
    /// </summary>
    public void Begin(bool _hasCard)
    {
        hasCard = _hasCard;
        leftOnCard = false;
        rightOnCard = false;
    }

    /// <summary>
    /// 라운드 끝: 카드 없음 상태로 되돌린다
    /// </summary>
    public void End()
    {
        Begin(false);
    }

    public bool IsOnCard(PlayerSide _side)
    {
        return PlayerSide.Left == _side ? leftOnCard : rightOnCard;
    }

    /// <summary>
    /// _side 커서를 카드로 옮긴다 (카드가 없거나 이미 카드면 false)
    /// </summary>
    public bool MoveToCard(PlayerSide _side)
    {
        if (false == hasCard || true == IsOnCard(_side))
        {
            return false;
        }
        SetOnCard(_side, true);
        return true;
    }

    /// <summary>
    /// _side 커서를 그룹으로 되돌린다 (이미 그룹이면 false)
    /// </summary>
    public bool MoveToGroup(PlayerSide _side)
    {
        if (false == IsOnCard(_side))
        {
            return false;
        }
        SetOnCard(_side, false);
        return true;
    }

    /// <summary>
    /// 한 프레임의 키 입력을 처리한다. 상태가 바뀌었으면 true
    /// - _towardCenter : 왼쪽 D / 오른쪽 ← (카드로)
    /// - _towardOutside : 왼쪽 A / 오른쪽 → (그룹으로). 둘 다 눌렸으면 무시
    /// </summary>
    public bool HandleInput(PlayerSide _side, bool _towardCenter, bool _towardOutside)
    {
        if (_towardCenter == _towardOutside)
        {
            return false;
        }
        return true == _towardCenter ? MoveToCard(_side) : MoveToGroup(_side);
    }

    /// <summary>
    /// 재개 가능 여부. _groupsReady = 기존 조건(보이는 그룹이 모두 선택됨)
    /// </summary>
    public bool CanResume(bool _groupsReady)
    {
        return true == ShouldApplyFusion || true == _groupsReady;
    }

    /// <summary>
    /// 카드 클릭 위치로 고른 진영 (카드 가운데보다 왼쪽이면 Left)
    /// </summary>
    public static PlayerSide SideFromClick(float _clickX, float _centerX)
    {
        return _clickX < _centerX ? PlayerSide.Left : PlayerSide.Right;
    }

    /// <summary>
    /// 합성 보상을 적용할 컨텍스트 진영 (왼쪽 우선. 합성 효과는 컨텍스트와 무관하게 양쪽을 처리한다)
    /// </summary>
    public PlayerSide FusionContextSide => true == leftOnCard || false == rightOnCard ? PlayerSide.Left : PlayerSide.Right;

    private void SetOnCard(PlayerSide _side, bool _onCard)
    {
        if (PlayerSide.Left == _side)
        {
            leftOnCard = _onCard;
        }
        else
        {
            rightOnCard = _onCard;
        }
    }
}
