using NUnit.Framework;

/// <summary>
/// 레벨업 중앙 합성 카드 커서 상태 (DimensionFusionCursor) 테스트
/// </summary>
public class DimensionFusionCursorTests
{
    [Test]
    public void Begin_WithoutCard_IgnoresMoveAndKeepsOldResumeRule()
    {
        DimensionFusionCursor cursor = new DimensionFusionCursor();
        cursor.Begin(false);

        Assert.IsFalse(cursor.MoveToCard(PlayerSide.Left));
        Assert.IsFalse(cursor.HandleInput(PlayerSide.Right, true, false));
        Assert.IsFalse(cursor.AnyOnCard);
        Assert.IsFalse(cursor.ShouldApplyFusion);
        Assert.IsFalse(cursor.CanResume(false));
        Assert.IsTrue(cursor.CanResume(true));
    }

    [Test]
    public void Begin_WithCard_BothStartOnGroup()
    {
        DimensionFusionCursor cursor = new DimensionFusionCursor();
        cursor.Begin(true);

        Assert.IsTrue(cursor.HasCard);
        Assert.IsFalse(cursor.IsOnCard(PlayerSide.Left));
        Assert.IsFalse(cursor.IsOnCard(PlayerSide.Right));
        Assert.IsFalse(cursor.ShouldApplyFusion);
        Assert.IsFalse(cursor.CanResume(false));
    }

    [Test]
    public void HandleInput_TowardCenterThenOutside_MovesAndReturns()
    {
        DimensionFusionCursor cursor = new DimensionFusionCursor();
        cursor.Begin(true);

        Assert.IsTrue(cursor.HandleInput(PlayerSide.Left, true, false));
        Assert.IsTrue(cursor.IsOnCard(PlayerSide.Left));
        Assert.IsFalse(cursor.IsOnCard(PlayerSide.Right));

        // 이미 카드면 변화 없음
        Assert.IsFalse(cursor.HandleInput(PlayerSide.Left, true, false));

        Assert.IsTrue(cursor.HandleInput(PlayerSide.Left, false, true));
        Assert.IsFalse(cursor.IsOnCard(PlayerSide.Left));

        // 이미 그룹이면 변화 없음
        Assert.IsFalse(cursor.HandleInput(PlayerSide.Left, false, true));
    }

    [Test]
    public void HandleInput_BothKeysOrNone_Ignored()
    {
        DimensionFusionCursor cursor = new DimensionFusionCursor();
        cursor.Begin(true);

        Assert.IsFalse(cursor.HandleInput(PlayerSide.Right, true, true));
        Assert.IsFalse(cursor.HandleInput(PlayerSide.Right, false, false));
        Assert.IsFalse(cursor.IsOnCard(PlayerSide.Right));
    }

    [Test]
    public void OneSideOnCard_CanResumeRegardlessOfGroups_AndAppliesFusion()
    {
        DimensionFusionCursor cursor = new DimensionFusionCursor();
        cursor.Begin(true);
        cursor.MoveToCard(PlayerSide.Right);

        Assert.IsTrue(cursor.CanResume(false));
        Assert.IsTrue(cursor.ShouldApplyFusion);
        Assert.AreEqual(PlayerSide.Right, cursor.FusionContextSide);

        cursor.MoveToCard(PlayerSide.Left);
        Assert.AreEqual(PlayerSide.Left, cursor.FusionContextSide);
    }

    [Test]
    public void BothOnGroup_WithCard_UsesGroupResumeRule()
    {
        DimensionFusionCursor cursor = new DimensionFusionCursor();
        cursor.Begin(true);
        cursor.MoveToCard(PlayerSide.Left);
        cursor.MoveToGroup(PlayerSide.Left);

        Assert.IsFalse(cursor.ShouldApplyFusion);
        Assert.IsFalse(cursor.CanResume(false));
        Assert.IsTrue(cursor.CanResume(true));
    }

    [Test]
    public void BeginAndEnd_ResetCursors()
    {
        DimensionFusionCursor cursor = new DimensionFusionCursor();
        cursor.Begin(true);
        cursor.MoveToCard(PlayerSide.Left);
        cursor.MoveToCard(PlayerSide.Right);

        cursor.Begin(true);
        Assert.IsFalse(cursor.AnyOnCard);

        cursor.MoveToCard(PlayerSide.Left);
        cursor.End();
        Assert.IsFalse(cursor.HasCard);
        Assert.IsFalse(cursor.AnyOnCard);
        Assert.IsFalse(cursor.ShouldApplyFusion);
    }

    [Test]
    public void SideFromClick_LeftOfCenterIsLeft()
    {
        Assert.AreEqual(PlayerSide.Left, DimensionFusionCursor.SideFromClick(-10f, 0f));
        Assert.AreEqual(PlayerSide.Right, DimensionFusionCursor.SideFromClick(10f, 0f));
        Assert.AreEqual(PlayerSide.Right, DimensionFusionCursor.SideFromClick(0f, 0f));
    }
}
