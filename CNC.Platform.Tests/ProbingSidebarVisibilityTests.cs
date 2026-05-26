using CNC.Controls.Probing;

namespace CNC.Platform.Tests;

public class ProbingSidebarVisibilityTests
{
    [Fact]
    public void ToolLengthWithoutFixtureShowsTouchPlateOnly()
    {
        var model = new ProbingViewModel
        {
            ProbingType = ProbingType.ToolLength,
            ProbeFixture = false
        };

        AssertVisibility(
            model,
            probeDiameter: false,
            touchPlateHeight: true,
            fixtureHeight: false,
            xyOffset: false,
            offset: false,
            showTouchPlateSection: true,
            showClearancesSection: false,
            touchPlateXY: true);
    }

    [Fact]
    public void ToolLengthWithFixtureShowsFixtureOnly()
    {
        var model = new ProbingViewModel
        {
            ProbingType = ProbingType.ToolLength,
            ProbeFixture = true
        };

        AssertVisibility(
            model,
            probeDiameter: false,
            touchPlateHeight: false,
            fixtureHeight: true,
            xyOffset: false,
            offset: false,
            showTouchPlateSection: true,
            showClearancesSection: false,
            touchPlateXY: true);
    }

    [Theory]
    [InlineData(ProbingType.EdgeFinderExternal)]
    [InlineData(ProbingType.EdgeFinderInternal)]
    public void EdgeFinderWithNoEdgeShowsProbeDiameterOnly(ProbingType probingType)
    {
        var model = new ProbingViewModel
        {
            ProbingType = probingType,
            ProbeEdge = Edge.None
        };

        AssertVisibility(
            model,
            probeDiameter: true,
            touchPlateHeight: false,
            fixtureHeight: false,
            xyOffset: false,
            offset: false,
            showTouchPlateSection: false,
            showClearancesSection: false,
            touchPlateXY: true);
    }

    [Theory]
    [InlineData(ProbingType.EdgeFinderExternal)]
    [InlineData(ProbingType.EdgeFinderInternal)]
    public void EdgeFinderWithZEdgeShowsTouchPlateWhenProbeZIsSelected(ProbingType probingType)
    {
        var model = new ProbingViewModel
        {
            ProbingType = probingType,
            ProbeEdge = Edge.Z
        };

        AssertVisibility(
            model,
            probeDiameter: false,
            touchPlateHeight: true,
            fixtureHeight: false,
            xyOffset: false,
            offset: false,
            showTouchPlateSection: true,
            showClearancesSection: false,
            touchPlateXY: true);
    }

    [Theory]
    [InlineData(ProbingType.EdgeFinderExternal)]
    [InlineData(ProbingType.EdgeFinderInternal)]
    public void EdgeFinderWithSideEdgeShowsDiameterAndClearances(ProbingType probingType)
    {
        var model = new ProbingViewModel
        {
            ProbingType = probingType,
            ProbeEdge = Edge.AB
        };

        AssertVisibility(
            model,
            probeDiameter: true,
            touchPlateHeight: false,
            fixtureHeight: false,
            xyOffset: true,
            offset: false,
            showTouchPlateSection: false,
            showClearancesSection: true,
            touchPlateXY: true);
    }

    [Theory]
    [InlineData(ProbingType.EdgeFinderExternal)]
    [InlineData(ProbingType.EdgeFinderInternal)]
    public void EdgeFinderWithCornerShowsOffsetAndClearances(ProbingType probingType)
    {
        var model = new ProbingViewModel
        {
            ProbingType = probingType,
            ProbeEdge = Edge.A
        };

        AssertVisibility(
            model,
            probeDiameter: true,
            touchPlateHeight: false,
            fixtureHeight: false,
            xyOffset: true,
            offset: true,
            showTouchPlateSection: false,
            showClearancesSection: true,
            touchPlateXY: true);
    }

    [Fact]
    public void RotationShowsClearanceOffsetAndXyOffsets()
    {
        var model = new ProbingViewModel { ProbingType = ProbingType.Rotation };

        AssertVisibility(
            model,
            probeDiameter: false,
            touchPlateHeight: false,
            fixtureHeight: false,
            xyOffset: true,
            offset: true,
            showTouchPlateSection: false,
            showClearancesSection: true,
            touchPlateXY: true);
    }

    [Fact]
    public void CenterFinderShowsDiameterAndXyOffsets()
    {
        var model = new ProbingViewModel { ProbingType = ProbingType.CenterFinder };

        AssertVisibility(
            model,
            probeDiameter: true,
            touchPlateHeight: false,
            fixtureHeight: false,
            xyOffset: true,
            offset: false,
            showTouchPlateSection: false,
            showClearancesSection: true,
            touchPlateXY: true);
    }

    [Fact]
    public void HeightMapShowsTouchPlateAndXyOffsetsButHidesTouchPlateXyToggle()
    {
        var model = new ProbingViewModel { ProbingType = ProbingType.HeightMap };

        AssertVisibility(
            model,
            probeDiameter: false,
            touchPlateHeight: true,
            fixtureHeight: false,
            xyOffset: true,
            offset: false,
            showTouchPlateSection: true,
            showClearancesSection: true,
            touchPlateXY: false);
    }

    [Fact]
    public void SidebarVisibilityChangesRaiseNotifications()
    {
        var model = new ProbingViewModel();
        var changed = new List<string>();
        model.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != null)
                changed.Add(e.PropertyName);
        };

        model.ProbingType = ProbingType.EdgeFinderExternal;
        model.ProbeEdge = Edge.Z;
        model.ProbeZ = false;
        model.ProbeFixture = true;

        Assert.Contains(nameof(ProbingViewModel.ProbeDiameterEnable), changed);
        Assert.Contains(nameof(ProbingViewModel.TouchPlateHeightEnable), changed);
        Assert.Contains(nameof(ProbingViewModel.FixtureHeightEnable), changed);
        Assert.Contains(nameof(ProbingViewModel.XYOffsetEnable), changed);
        Assert.Contains(nameof(ProbingViewModel.OffsetEnable), changed);
        Assert.Contains(nameof(ProbingViewModel.ShowTouchPlateSection), changed);
        Assert.Contains(nameof(ProbingViewModel.ShowClearancesSection), changed);
        Assert.Contains(nameof(ProbingViewModel.TouchPlateXYEnabled), changed);
    }

    static void AssertVisibility(
        ProbingViewModel model,
        bool probeDiameter,
        bool touchPlateHeight,
        bool fixtureHeight,
        bool xyOffset,
        bool offset,
        bool showTouchPlateSection,
        bool showClearancesSection,
        bool touchPlateXY)
    {
        Assert.Equal(probeDiameter, model.ProbeDiameterEnable);
        Assert.Equal(touchPlateHeight, model.TouchPlateHeightEnable);
        Assert.Equal(fixtureHeight, model.FixtureHeightEnable);
        Assert.Equal(xyOffset, model.XYOffsetEnable);
        Assert.Equal(offset, model.OffsetEnable);
        Assert.Equal(showTouchPlateSection, model.ShowTouchPlateSection);
        Assert.Equal(showClearancesSection, model.ShowClearancesSection);
        Assert.Equal(touchPlateXY, model.TouchPlateXYEnabled);
    }
}
