using System.Drawing;
using WechatStyleScreenshot.Core;

namespace WechatStyleScreenshot.Tests;

public class SelectionMathTests
{
    [Theory]
    [InlineData(10, 20, 110, 220, 10, 20, 100, 200)]
    [InlineData(110, 220, 10, 20, 10, 20, 100, 200)]
    [InlineData(110, 20, 10, 220, 10, 20, 100, 200)]
    [InlineData(10, 220, 110, 20, 10, 20, 100, 200)]
    public void FromPointsNormalizesDragDirection(
        int startX,
        int startY,
        int endX,
        int endY,
        int expectedX,
        int expectedY,
        int expectedWidth,
        int expectedHeight)
    {
        Rectangle rectangle = SelectionMath.FromPoints(new Point(startX, startY), new Point(endX, endY));

        Assert.Equal(new Rectangle(expectedX, expectedY, expectedWidth, expectedHeight), rectangle);
    }

    [Theory]
    [InlineData(1, 20, false)]
    [InlineData(20, 1, false)]
    [InlineData(2, 2, true)]
    [InlineData(200, 100, true)]
    public void IsCapturableRejectsTinySelections(int width, int height, bool expected)
    {
        bool actual = SelectionMath.IsCapturable(new Rectangle(0, 0, width, height));

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void GetHandleRectanglesReturnsEightResizeHandles()
    {
        Rectangle selection = new(10, 20, 100, 80);

        IReadOnlyList<Rectangle> handles = SelectionMath.GetHandleRectangles(selection, 8);

        Assert.Equal(8, handles.Count);
        Assert.Contains(new Rectangle(6, 16, 8, 8), handles);
        Assert.Contains(new Rectangle(106, 96, 8, 8), handles);
        Assert.Contains(new Rectangle(56, 16, 8, 8), handles);
        Assert.Contains(new Rectangle(6, 56, 8, 8), handles);
    }

    [Fact]
    public void HitTestReturnsHandleBeforeMove()
    {
        Rectangle selection = new(10, 20, 100, 80);

        SelectionHitTarget target = SelectionMath.HitTest(selection, new Point(10, 20), 8);

        Assert.Equal(SelectionHitTarget.TopLeft, target);
    }

    [Fact]
    public void HitTestReturnsMoveInsideSelection()
    {
        Rectangle selection = new(10, 20, 100, 80);

        SelectionHitTarget target = SelectionMath.HitTest(selection, new Point(50, 60), 8);

        Assert.Equal(SelectionHitTarget.Move, target);
    }

    [Fact]
    public void ApplyDragMovesSelectionWithinBounds()
    {
        Rectangle selection = new(10, 20, 100, 80);
        Rectangle bounds = new(0, 0, 200, 200);

        Rectangle actual = SelectionMath.ApplyDrag(selection, SelectionHitTarget.Move, new Size(15, -10), bounds);

        Assert.Equal(new Rectangle(25, 10, 100, 80), actual);
    }

    [Fact]
    public void ApplyDragResizesFromBottomRight()
    {
        Rectangle selection = new(10, 20, 100, 80);
        Rectangle bounds = new(0, 0, 200, 200);

        Rectangle actual = SelectionMath.ApplyDrag(selection, SelectionHitTarget.BottomRight, new Size(15, 20), bounds);

        Assert.Equal(new Rectangle(10, 20, 115, 100), actual);
    }

    [Fact]
    public void GetMouseActionConfirmsSelectedAreaOnLeftDoubleClick()
    {
        SelectionMouseAction action = SelectionMath.GetMouseAction(MouseButtons.Left, clicks: 2, hasSelection: true);

        Assert.Equal(SelectionMouseAction.Confirm, action);
    }

    [Fact]
    public void GetMouseActionCancelsOnRightClick()
    {
        SelectionMouseAction action = SelectionMath.GetMouseAction(MouseButtons.Right, clicks: 1, hasSelection: true);

        Assert.Equal(SelectionMouseAction.Cancel, action);
    }

    [Fact]
    public void HitTestToolbarButtonsDetectsConfirmAndCancel()
    {
        Rectangle cancel = new(10, 10, 30, 30);
        Rectangle confirm = new(60, 10, 30, 30);

        Assert.Equal(ToolbarButtonHit.Cancel, SelectionMath.HitTestToolbarButtons(cancel, confirm, new Point(15, 20)));
        Assert.Equal(ToolbarButtonHit.Confirm, SelectionMath.HitTestToolbarButtons(cancel, confirm, new Point(70, 20)));
        Assert.Equal(ToolbarButtonHit.None, SelectionMath.HitTestToolbarButtons(cancel, confirm, new Point(45, 20)));
    }

    [Fact]
    public void HitTestToolbarButtonsDetectsCancelOcrAndConfirmWithoutOverlap()
    {
        Rectangle cancel = new(10, 10, 30, 30);
        Rectangle ocr = new(50, 10, 30, 30);
        Rectangle confirm = new(90, 10, 30, 30);

        Assert.Equal(ToolbarButtonHit.Cancel, SelectionMath.HitTestToolbarButtons(cancel, ocr, confirm, new Point(15, 20)));
        Assert.Equal(ToolbarButtonHit.Ocr, SelectionMath.HitTestToolbarButtons(cancel, ocr, confirm, new Point(55, 20)));
        Assert.Equal(ToolbarButtonHit.Confirm, SelectionMath.HitTestToolbarButtons(cancel, ocr, confirm, new Point(95, 20)));
        Assert.Equal(ToolbarButtonHit.None, SelectionMath.HitTestToolbarButtons(cancel, ocr, confirm, new Point(45, 20)));
    }

    [Fact]
    public void HitTestToolbarButtonsDetectsOutfitPreview()
    {
        Rectangle cancel = new(10, 10, 30, 30);
        Rectangle ocr = new(50, 10, 30, 30);
        Rectangle outfit = new(90, 10, 30, 30);
        Rectangle confirm = new(130, 10, 30, 30);

        Assert.Equal(ToolbarButtonHit.OutfitPreview, SelectionMath.HitTestToolbarButtons(cancel, ocr, outfit, confirm, new Point(95, 20)));
    }
}
