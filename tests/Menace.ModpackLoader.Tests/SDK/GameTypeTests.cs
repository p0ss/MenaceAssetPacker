using System;
using Menace.SDK;
using Xunit;

namespace Menace.ModpackLoader.Tests.SDK;

public class GameTypeTests
{
    [Fact]
    public void Find_EmptyName_ReturnsInvalid()
    {
        var result = GameType.Find("");

        Assert.False(result.IsValid);
        Assert.Equal(IntPtr.Zero, result.ClassPointer);
    }

    [Fact]
    public void Find_NullName_ReturnsInvalid()
    {
        var result = GameType.Find(null);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Find_UnresolvableName_ReturnsInvalid()
    {
        // All IL2CPP stubs return IntPtr.Zero, so no type can be resolved
        var result = GameType.Find("NonExistent.Type.That.Does.Not.Exist");

        Assert.False(result.IsValid);
    }

    [Fact]
    public void FromPointer_Zero_ReturnsInvalid()
    {
        var result = GameType.FromPointer(IntPtr.Zero);

        Assert.False(result.IsValid);
        Assert.Same(GameType.Invalid, result);
    }

    [Fact]
    public void Invalid_IsNotValid()
    {
        Assert.False(GameType.Invalid.IsValid);
        Assert.Equal(IntPtr.Zero, GameType.Invalid.ClassPointer);
        Assert.Equal("", GameType.Invalid.FullName);
    }

    [Fact]
    public void ToString_Invalid_ShowsMessage()
    {
        Assert.Equal("<invalid GameType>", GameType.Invalid.ToString());
    }
}
