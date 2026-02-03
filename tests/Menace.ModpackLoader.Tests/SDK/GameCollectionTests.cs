using System;
using Menace.SDK;
using Xunit;

namespace Menace.ModpackLoader.Tests.SDK;

public class GameCollectionTests
{
    [Fact]
    public void GameList_ZeroPointer_NotValid()
    {
        var list = new GameList(IntPtr.Zero);

        Assert.False(list.IsValid);
    }

    [Fact]
    public void GameList_ZeroPointer_CountIsZero()
    {
        var list = new GameList(IntPtr.Zero);

        Assert.Equal(0, list.Count);
    }

    [Fact]
    public void GameDict_ZeroPointer_NotValid()
    {
        var dict = new GameDict(IntPtr.Zero);

        Assert.False(dict.IsValid);
    }

    [Fact]
    public void GameDict_ZeroPointer_CountIsZero()
    {
        var dict = new GameDict(IntPtr.Zero);

        Assert.Equal(0, dict.Count);
    }

    [Fact]
    public void GameArray_ZeroPointer_NotValid()
    {
        var arr = new GameArray(IntPtr.Zero);

        Assert.False(arr.IsValid);
    }

    [Fact]
    public void GameArray_ZeroPointer_LengthIsZero()
    {
        var arr = new GameArray(IntPtr.Zero);

        Assert.Equal(0, arr.Length);
    }
}
