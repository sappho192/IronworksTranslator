using IronworksTranslator.Utils.UI;
using System.Windows;

namespace IronworksTranslator.Tests.Utils;

public class GameWindowFollowTests
{
    [Theory]
    [InlineData(true, true, false, 10, "Restore")]
    [InlineData(true, true, false, 20, "Restore")]
    [InlineData(true, true, false, 30, "Minimize")]
    [InlineData(true, true, false, 0, "Keep")]
    [InlineData(true, true, true, 0, "Minimize")]
    [InlineData(true, true, true, 30, "Minimize")]
    [InlineData(true, true, true, 20, "Restore")]
    [InlineData(false, true, false, 30, "Restore")]
    [InlineData(true, false, false, 30, "Restore")]
    public void FocusTransitions_ChooseExpectedAction(
        bool enabled, bool gameAvailable, bool minimized, int foreground, string expected)
    {
        Assert.Equal(expected, GameWindowFollowPolicy.Decide(
            enabled, gameAvailable, minimized, foreground, 10, 20).ToString());
    }

    [Fact]
    public void NoAttachedGame_DoesNotMinimizeWindows()
    {
        Assert.Equal(GameWindowFollowAction.Restore,
            GameWindowFollowPolicy.Decide(true, true, false, 30, 0, 20));
    }

    [Theory]
    [InlineData(WindowState.Normal)]
    [InlineData(WindowState.Maximized)]
    public void AutomaticMinimize_RestoresPreviousStateExactlyOnce(WindowState original)
    {
        var state = new AutomaticWindowState();
        Assert.Equal(WindowState.Minimized, state.Update(true, original, true));
        Assert.Null(state.Update(true, WindowState.Minimized, true));
        Assert.Equal(original, state.Update(true, WindowState.Minimized, false));
        Assert.Null(state.Update(true, original, false));
    }

    [Fact]
    public void ManualMinimize_IsNeverRestored()
    {
        var state = new AutomaticWindowState();
        Assert.Null(state.Update(true, WindowState.Minimized, true));
        Assert.Null(state.Update(true, WindowState.Minimized, false));
    }

    [Fact]
    public void HiddenWindow_IsNotMinimizedAndClearsAutomaticState()
    {
        var state = new AutomaticWindowState();
        Assert.Null(state.Update(false, WindowState.Normal, true));
        Assert.Equal(WindowState.Minimized, state.Update(true, WindowState.Normal, true));
        Assert.Equal(WindowState.Normal, state.Update(false, WindowState.Minimized, true));
        Assert.Null(state.Update(false, WindowState.Normal, false));
    }

    [Fact]
    public void ManualRestore_DiscardsThePreviousAutomaticState()
    {
        var state = new AutomaticWindowState();
        state.Update(true, WindowState.Maximized, true);
        Assert.Null(state.Update(true, WindowState.Normal, false));
        Assert.Null(state.Update(true, WindowState.Minimized, false));
    }

    [Fact]
    public void AltTabStyles_PreserveTransparencyTopmostAndOtherUnrelatedFlags()
    {
        const int unrelated = 0x00080000 | 0x00000008 | 0x08000000;
        var excluded = NativeWindowMethods.GetAltTabStyle(unrelated | NativeWindowMethods.WsExAppWindow, true);
        Assert.Equal(unrelated | NativeWindowMethods.WsExToolWindow, excluded);
        Assert.Equal(unrelated | NativeWindowMethods.WsExAppWindow,
            NativeWindowMethods.GetAltTabStyle(excluded, false));
    }
}
