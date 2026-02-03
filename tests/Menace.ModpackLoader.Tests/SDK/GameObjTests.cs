using System;
using Menace.SDK;
using Xunit;

namespace Menace.ModpackLoader.Tests.SDK;

public class GameObjTests
{
    [Fact]
    public void Default_IsNull()
    {
        var obj = default(GameObj);

        Assert.True(obj.IsNull);
        Assert.Equal(IntPtr.Zero, obj.Pointer);
    }

    [Fact]
    public void ZeroPointer_IsNull()
    {
        var obj = new GameObj(IntPtr.Zero);

        Assert.True(obj.IsNull);
    }

    [Fact]
    public void NonZero_IsNotNull()
    {
        var obj = new GameObj(new IntPtr(1));

        Assert.False(obj.IsNull);
    }

    [Fact]
    public void Equals_SamePointer_True()
    {
        var a = new GameObj(new IntPtr(42));
        var b = new GameObj(new IntPtr(42));

        Assert.True(a.Equals(b));
        Assert.True(a == b);
    }

    [Fact]
    public void Equals_DifferentPointer_False()
    {
        var a = new GameObj(new IntPtr(1));
        var b = new GameObj(new IntPtr(2));

        Assert.False(a.Equals(b));
        Assert.True(a != b);
    }

    [Fact]
    public void GetHashCode_ConsistentWithPointer()
    {
        var ptr = new IntPtr(12345);
        var obj = new GameObj(ptr);

        Assert.Equal(ptr.GetHashCode(), obj.GetHashCode());
    }
}
