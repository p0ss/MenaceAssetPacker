using System;
using Menace.Modkit.App.Models;

namespace Menace.Modkit.Tests.Integration;

/// <summary>
/// Phase 0/1 boundary: verifies ModpackDependency.Parse(), TryParse(), IsSatisfiedBy().
/// </summary>
public class DependencyParsingTests
{
    [Fact]
    public void Parse_NameOnly_SetsNameAndEmptyOperator()
    {
        var dep = ModpackDependency.Parse("MenaceFramework");

        Assert.Equal("MenaceFramework", dep.Name);
        Assert.Equal(string.Empty, dep.Operator);
        Assert.Equal(string.Empty, dep.VersionConstraint);
    }

    [Fact]
    public void Parse_GreaterThanOrEqual_ParsesCorrectly()
    {
        var dep = ModpackDependency.Parse("Foo >= 1.0");

        Assert.Equal("Foo", dep.Name);
        Assert.Equal(">=", dep.Operator);
        Assert.Equal("1.0", dep.VersionConstraint);
    }

    [Theory]
    [InlineData("A >= 1.0", "A", ">=", "1.0")]
    [InlineData("B <= 2.5.1", "B", "<=", "2.5.1")]
    [InlineData("C == 3.0.0", "C", "==", "3.0.0")]
    [InlineData("D > 0.9", "D", ">", "0.9")]
    [InlineData("E < 4.0", "E", "<", "4.0")]
    public void Parse_AllOperators(string input, string name, string op, string ver)
    {
        var dep = ModpackDependency.Parse(input);

        Assert.Equal(name, dep.Name);
        Assert.Equal(op, dep.Operator);
        Assert.Equal(ver, dep.VersionConstraint);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(">=1.0")]
    [InlineData("@invalid!")]
    public void Parse_InvalidFormat_ThrowsFormatException(string input)
    {
        Assert.Throws<FormatException>(() => ModpackDependency.Parse(input));
    }

    [Fact]
    public void TryParse_ValidInput_ReturnsTrue()
    {
        var success = ModpackDependency.TryParse("Framework >= 2.0", out var dep);

        Assert.True(success);
        Assert.NotNull(dep);
        Assert.Equal("Framework", dep!.Name);
        Assert.Equal(">=", dep.Operator);
    }

    [Fact]
    public void TryParse_InvalidInput_ReturnsFalse()
    {
        var success = ModpackDependency.TryParse("@bad!", out var dep);

        Assert.False(success);
        Assert.Null(dep);
    }

    [Fact]
    public void IsSatisfiedBy_NoConstraint_AlwaysTrue()
    {
        var dep = ModpackDependency.Parse("SomeMod");

        Assert.True(dep.IsSatisfiedBy("1.0.0"));
        Assert.True(dep.IsSatisfiedBy("999.0.0"));
        Assert.True(dep.IsSatisfiedBy("0.0.1"));
    }

    [Fact]
    public void IsSatisfiedBy_GreaterOrEqual_Above_True()
    {
        var dep = ModpackDependency.Parse("Mod >= 2.0.0");

        Assert.True(dep.IsSatisfiedBy("3.0.0"));
        Assert.True(dep.IsSatisfiedBy("2.1.0"));
    }

    [Fact]
    public void IsSatisfiedBy_GreaterOrEqual_Below_False()
    {
        var dep = ModpackDependency.Parse("Mod >= 2.0.0");

        Assert.False(dep.IsSatisfiedBy("1.9.9"));
        Assert.False(dep.IsSatisfiedBy("0.1.0"));
    }

    [Fact]
    public void IsSatisfiedBy_ExactMatch_True()
    {
        var dep = ModpackDependency.Parse("Mod >= 2.0.0");

        Assert.True(dep.IsSatisfiedBy("2.0.0"));
    }

    [Fact]
    public void IsSatisfiedBy_TwoPartVersion_Normalizes()
    {
        // "1.0" should normalize to "1.0.0" for comparison
        var dep = ModpackDependency.Parse("Mod >= 1.0");

        Assert.True(dep.IsSatisfiedBy("1.0"));
        Assert.True(dep.IsSatisfiedBy("1.0.0"));
        Assert.True(dep.IsSatisfiedBy("1.1"));
        Assert.False(dep.IsSatisfiedBy("0.9"));
    }
}
