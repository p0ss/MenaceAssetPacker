using System;
using System.Collections.Generic;
using Menace.SDK;
using Xunit;

namespace Menace.ModpackLoader.Tests.SDK;

public class GameStateTests : IDisposable
{
    public GameStateTests()
    {
        CleanupGameState();
    }

    public void Dispose()
    {
        CleanupGameState();
    }

    [Fact]
    public void NotifySceneLoaded_UpdatesCurrentScene()
    {
        GameState.NotifySceneLoaded("MainMenu");

        Assert.Equal("MainMenu", GameState.CurrentScene);
    }

    [Fact]
    public void NotifySceneLoaded_FiresSceneLoadedEvent()
    {
        string received = null;
        void handler(string s) { received = s; }

        GameState.SceneLoaded += handler;
        try
        {
            GameState.NotifySceneLoaded("Tactical");
            Assert.Equal("Tactical", received);
        }
        finally
        {
            GameState.SceneLoaded -= handler;
        }
    }

    [Fact]
    public void IsScene_MatchesCurrentScene()
    {
        GameState.NotifySceneLoaded("Tactical");

        Assert.True(GameState.IsScene("Tactical"));
        Assert.True(GameState.IsScene("tactical")); // case-insensitive
        Assert.False(GameState.IsScene("MainMenu"));
    }

    [Fact]
    public void RunDelayed_ExecutesAfterFrames()
    {
        bool fired = false;
        GameState.RunDelayed(3, () => fired = true);

        GameState.ProcessUpdate(); // frame 1
        GameState.ProcessUpdate(); // frame 2
        Assert.False(fired);

        GameState.ProcessUpdate(); // frame 3 — should fire
        Assert.True(fired);
    }

    [Fact]
    public void RunDelayed_DoesNotExecuteEarly()
    {
        bool fired = false;
        GameState.RunDelayed(5, () => fired = true);

        for (int i = 0; i < 4; i++)
            GameState.ProcessUpdate();

        Assert.False(fired);

        GameState.ProcessUpdate(); // frame 5
        Assert.True(fired);
    }

    [Fact]
    public void RunWhen_ExecutesWhenConditionTrue()
    {
        int callCount = 0;
        bool fired = false;

        GameState.RunWhen(
            () => ++callCount >= 3,
            () => fired = true,
            maxAttempts: 10);

        GameState.ProcessUpdate(); // attempt 1, condition false
        GameState.ProcessUpdate(); // attempt 2, condition false
        Assert.False(fired);

        GameState.ProcessUpdate(); // attempt 3, condition true
        Assert.True(fired);
    }

    [Fact]
    public void RunWhen_StopsAfterMaxAttempts()
    {
        bool fired = false;

        GameState.RunWhen(
            () => false, // never true
            () => fired = true,
            maxAttempts: 3);

        for (int i = 0; i < 5; i++)
            GameState.ProcessUpdate();

        Assert.False(fired);
    }

    [Fact]
    public void ProcessUpdate_HandlesMultipleDelayedActions()
    {
        var results = new List<int>();

        GameState.RunDelayed(1, () => results.Add(1));
        GameState.RunDelayed(2, () => results.Add(2));
        GameState.RunDelayed(3, () => results.Add(3));

        GameState.ProcessUpdate();
        Assert.Single(results);
        Assert.Contains(1, results);

        GameState.ProcessUpdate();
        Assert.Equal(2, results.Count);
        Assert.Contains(2, results);

        GameState.ProcessUpdate();
        Assert.Equal(3, results.Count);
        Assert.Contains(3, results);
    }

    private static void CleanupGameState()
    {
        // Reset scene and _tacticalFired
        GameState.NotifySceneLoaded("_cleanup_");
        // Drain any remaining delayed/conditional actions
        for (int i = 0; i < 100; i++)
            GameState.ProcessUpdate();
        // Reset to empty scene
        GameState.NotifySceneLoaded("");
    }
}
