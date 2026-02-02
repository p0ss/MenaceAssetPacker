using System.Collections.Generic;
using System.Linq;
using Menace.Modkit.App.Models;
using Menace.Modkit.App.Services;
using Menace.Modkit.Tests.Helpers;

namespace Menace.Modkit.Tests.Integration;

/// <summary>
/// Phase 4: verifies SecurityScanner.ScanSources() pattern detection.
/// </summary>
public class SecurityScannerTests
{
    private readonly SecurityScanner _scanner = new();

    [Fact]
    public void ScanSources_CleanFile_NoWarnings()
    {
        using var tmp = new TemporaryDirectory();
        var file = tmp.WriteFile("Clean.cs", """
            using System;
            public class Clean
            {
                public int Add(int a, int b) => a + b;
            }
            """);

        var warnings = _scanner.ScanSources(new List<string> { file });

        Assert.Empty(warnings);
    }

    [Fact]
    public void ScanSources_HttpClient_ReturnsDanger()
    {
        using var tmp = new TemporaryDirectory();
        var file = tmp.WriteFile("Net.cs", """
            using System.Net.Http;
            public class Net
            {
                private HttpClient _client = new HttpClient();
            }
            """);

        var warnings = _scanner.ScanSources(new List<string> { file });

        Assert.Contains(warnings, w => w.Severity == SecuritySeverity.Danger && w.Category == "Networking");
    }

    [Fact]
    public void ScanSources_ProcessStart_ReturnsDanger()
    {
        using var tmp = new TemporaryDirectory();
        var file = tmp.WriteFile("Proc.cs", """
            using System.Diagnostics;
            public class Proc
            {
                public void Run() { Process.Start("notepad"); }
            }
            """);

        var warnings = _scanner.ScanSources(new List<string> { file });

        Assert.Contains(warnings, w => w.Severity == SecuritySeverity.Danger && w.Category == "Process");
    }

    [Fact]
    public void ScanSources_DllImport_ReturnsWarning()
    {
        using var tmp = new TemporaryDirectory();
        var file = tmp.WriteFile("Native.cs", """
            using System.Runtime.InteropServices;
            public class Native
            {
                [DllImport("kernel32.dll")]
                static extern void Beep();
            }
            """);

        var warnings = _scanner.ScanSources(new List<string> { file });

        Assert.Contains(warnings, w => w.Severity == SecuritySeverity.Warning && w.Category == "P/Invoke");
    }

    [Fact]
    public void ScanSources_CommentedOut_NoWarning()
    {
        using var tmp = new TemporaryDirectory();
        var file = tmp.WriteFile("Commented.cs", """
            public class Commented
            {
                // HttpClient is not used here
                // Process.Start is also commented out
                public void DoNothing() { }
            }
            """);

        var warnings = _scanner.ScanSources(new List<string> { file });

        Assert.Empty(warnings);
    }

    [Fact]
    public void ScanSources_UrlLiteral_ReturnsDanger()
    {
        using var tmp = new TemporaryDirectory();
        var file = tmp.WriteFile("Url.cs", """
            public class Url
            {
                private string _endpoint = "https://example.com/api";
            }
            """);

        var warnings = _scanner.ScanSources(new List<string> { file });

        Assert.Contains(warnings, w => w.Severity == SecuritySeverity.Danger && w.Category == "URLs");
    }

    [Fact]
    public void ScanSources_Base64PlusAssemblyLoad_ReturnsDanger()
    {
        using var tmp = new TemporaryDirectory();
        var file = tmp.WriteFile("Obfuscated.cs", """
            using System;
            using System.Reflection;
            public class Obfuscated
            {
                public void Load() { Assembly.Load(Convert.FromBase64String("AAAA")); }
            }
            """);

        var warnings = _scanner.ScanSources(new List<string> { file });

        Assert.Contains(warnings, w => w.Severity == SecuritySeverity.Danger && w.Category == "Obfuscation");
    }

    [Fact]
    public void ScanSources_MultiplePatterns_ReturnsMultiple()
    {
        using var tmp = new TemporaryDirectory();
        var file = tmp.WriteFile("Multi.cs", """
            using System.Net.Http;
            using System.Diagnostics;
            public class Multi
            {
                private HttpClient _c = new HttpClient();
                public void Run() { Process.Start("cmd"); }
            }
            """);

        var warnings = _scanner.ScanSources(new List<string> { file });

        // Should have at least networking + process warnings
        Assert.True(warnings.Count >= 2);
        Assert.Contains(warnings, w => w.Category == "Networking");
        Assert.Contains(warnings, w => w.Category == "Process");
    }

    [Fact]
    public void ScanSources_NonexistentFile_Skipped()
    {
        var warnings = _scanner.ScanSources(new List<string> { "/nonexistent/path/Fake.cs" });

        Assert.Empty(warnings);
    }

    [Fact]
    public void ScanSources_CorrectLineNumber()
    {
        using var tmp = new TemporaryDirectory();
        var file = tmp.WriteFile("Lines.cs", """
            using System;
            using System.Net.Http;
            public class Lines
            {
                private HttpClient _c = new HttpClient();
            }
            """);

        var warnings = _scanner.ScanSources(new List<string> { file });

        var httpWarning = warnings.First(w => w.Category == "Networking");
        // HttpClient appears on line 5 (1-indexed)
        Assert.Equal(5, httpWarning.Line);
    }
}
