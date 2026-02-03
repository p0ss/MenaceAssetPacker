using System;
using System.Collections.Generic;
using System.Linq;
using Menace.ModpackLoader.Tests.Helpers;
using Menace.SDK.Repl;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Menace.ModpackLoader.Tests.SDK.Repl;

public class RuntimeCompilerTests
{
    private readonly RuntimeCompiler _compiler;

    public RuntimeCompilerTests()
    {
        _compiler = new RuntimeCompiler(TestReferences.GetAll());
    }

    [Fact]
    public void Compile_SimpleExpression_Succeeds()
    {
        var result = _compiler.Compile("1 + 2");

        Assert.True(result.Success, string.Join("; ", result.Errors));
        Assert.NotNull(result.LoadedAssembly);
    }

    [Fact]
    public void Compile_StringExpression_Succeeds()
    {
        var result = _compiler.Compile("\"hello\".Length");

        Assert.True(result.Success, string.Join("; ", result.Errors));
        Assert.NotNull(result.LoadedAssembly);
    }

    [Fact]
    public void Compile_Statements_Succeeds()
    {
        var result = _compiler.Compile("var x = 1; var y = 2;");

        Assert.True(result.Success, string.Join("; ", result.Errors));
        Assert.NotNull(result.LoadedAssembly);
    }

    [Fact]
    public void Compile_InvalidSyntax_ReturnsErrors()
    {
        var result = _compiler.Compile("{{{");

        Assert.False(result.Success);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void Compile_UndefinedType_ReturnsErrors()
    {
        var result = _compiler.Compile("TotallyFakeNonexistentType12345.DoStuff()");

        Assert.False(result.Success);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void Compile_NullInput_ReturnsError()
    {
        var result = _compiler.Compile(null);

        Assert.False(result.Success);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void Compile_EmptyInput_ReturnsError()
    {
        var result = _compiler.Compile("");

        Assert.False(result.Success);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void Compile_ReturnsLoadedAssembly()
    {
        var result = _compiler.Compile("42");

        Assert.True(result.Success, string.Join("; ", result.Errors));
        Assert.NotNull(result.LoadedAssembly);

        // The assembly should contain the generated class
        var types = result.LoadedAssembly.GetTypes();
        Assert.NotEmpty(types);
    }
}
