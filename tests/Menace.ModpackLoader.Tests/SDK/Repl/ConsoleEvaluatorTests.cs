using System;
using Menace.ModpackLoader.Tests.Helpers;
using Menace.SDK.Repl;
using Xunit;

namespace Menace.ModpackLoader.Tests.SDK.Repl;

public class ConsoleEvaluatorTests
{
    private readonly ConsoleEvaluator _evaluator;

    public ConsoleEvaluatorTests()
    {
        var compiler = new RuntimeCompiler(TestReferences.GetAll());
        _evaluator = new ConsoleEvaluator(compiler);
    }

    [Fact]
    public void Evaluate_SimpleExpression_ReturnsResult()
    {
        var result = _evaluator.Evaluate("1 + 2");

        Assert.True(result.Success, result.Error);
        Assert.Equal(3, result.Value);
        Assert.Equal("3", result.DisplayText);
    }

    [Fact]
    public void Evaluate_StringExpression_ReturnsString()
    {
        var result = _evaluator.Evaluate("\"hello\"");

        Assert.True(result.Success, result.Error);
        Assert.Equal("hello", result.Value);
        Assert.Equal("\"hello\"", result.DisplayText);
    }

    [Fact]
    public void Evaluate_NullResult_DisplaysNull()
    {
        // Statements without an explicit return get 'return null;' appended
        var result = _evaluator.Evaluate("var x = 1;");

        Assert.True(result.Success, result.Error);
        Assert.Null(result.Value);
        Assert.Equal("null", result.DisplayText);
    }

    [Fact]
    public void Evaluate_CompileError_ReturnsError()
    {
        var result = _evaluator.Evaluate("{{{");

        Assert.False(result.Success);
        Assert.NotNull(result.Error);
        Assert.NotEmpty(result.Error);
    }

    [Fact]
    public void History_TracksEvaluations()
    {
        _evaluator.Evaluate("1 + 1");
        _evaluator.Evaluate("2 + 2");

        Assert.Equal(2, _evaluator.History.Count);
        Assert.Equal("1 + 1", _evaluator.History[0].Input);
        Assert.Equal("2 + 2", _evaluator.History[1].Input);
    }

    [Fact]
    public void History_IncludesInputAndResult()
    {
        _evaluator.Evaluate("42");

        Assert.Single(_evaluator.History);

        var (input, result) = _evaluator.History[0];
        Assert.Equal("42", input);
        Assert.True(result.Success);
        Assert.Equal(42, result.Value);
    }
}
