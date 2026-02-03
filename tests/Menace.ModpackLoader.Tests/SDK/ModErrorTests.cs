using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Menace.SDK;
using Xunit;

namespace Menace.ModpackLoader.Tests.SDK;

public class ModErrorTests : IDisposable
{
    public ModErrorTests()
    {
        ResetModError();
    }

    public void Dispose()
    {
        ResetModError();
    }

    [Fact]
    public void Report_AddsEntryToRecentErrors()
    {
        ModError.Report("test-mod", "test message");

        var errors = ModError.RecentErrors;
        Assert.Single(errors);
        Assert.Equal("test-mod", errors[0].ModId);
        Assert.Equal("test message", errors[0].Message);
        Assert.Equal(ErrorSeverity.Error, errors[0].Severity);
    }

    [Fact]
    public void Report_NullModId_UsesUnknown()
    {
        ModError.Report(null, "msg");

        var errors = ModError.RecentErrors;
        Assert.Single(errors);
        Assert.Equal("unknown", errors[0].ModId);
    }

    [Fact]
    public void Warn_AddsWarningEntry()
    {
        ModError.Warn("test-mod", "warning message");

        var errors = ModError.RecentErrors;
        Assert.Single(errors);
        Assert.Equal(ErrorSeverity.Warning, errors[0].Severity);
        Assert.Equal("warning message", errors[0].Message);
    }

    [Fact]
    public void Info_AddsInfoEntry()
    {
        ModError.Info("test-mod", "info message");

        var errors = ModError.RecentErrors;
        Assert.Single(errors);
        Assert.Equal(ErrorSeverity.Info, errors[0].Severity);
    }

    [Fact]
    public void Clear_RemovesAllEntries()
    {
        ModError.Report("mod-a", "error 1");
        ModError.Report("mod-b", "error 2");
        Assert.Equal(2, ModError.RecentErrors.Count);

        ModError.Clear();

        Assert.Empty(ModError.RecentErrors);
    }

    [Fact]
    public void GetErrors_FiltersByModId()
    {
        ModError.Report("mod-a", "error A");
        ModError.Report("mod-b", "error B");
        ModError.Report("mod-a", "error A2");

        var modAErrors = ModError.GetErrors("mod-a");
        Assert.Equal(2, modAErrors.Count);
        Assert.All(modAErrors, e => Assert.Equal("mod-a", e.ModId));

        var allErrors = ModError.GetErrors();
        Assert.Equal(3, allErrors.Count);
    }

    [Fact]
    public void RingBuffer_EnforcesPerModLimit()
    {
        var modId = "ringbuf-mod";

        // Directly inject 200 entries via reflection to bypass rate limiting
        DirectAddEntries(modId, 200);
        Assert.Equal(200, ModError.GetErrors(modId).Count);

        // Adding one more through the public API should trim the oldest
        ModError.Report(modId, "entry-201");

        var errors = ModError.GetErrors(modId);
        Assert.Equal(200, errors.Count);
        // Oldest entry ("Error 0") should have been removed
        Assert.DoesNotContain(errors, e => e.Message == "Error 0");
        Assert.Contains(errors, e => e.Message == "entry-201");
    }

    [Fact]
    public void RingBuffer_EnforcesGlobalLimit()
    {
        // Inject 999 entries across many mods
        for (int m = 0; m < 10; m++)
        {
            DirectAddEntries($"global-mod-{m}", 100);
        }
        Assert.Equal(1000, ModError.RecentErrors.Count);

        // Adding one more should trim to 1000
        ModError.Report("overflow-mod", "overflow entry");

        Assert.True(ModError.RecentErrors.Count <= 1000);
    }

    [Fact]
    public void RateLimiting_DropsExcessiveErrors()
    {
        var modId = "ratelimit-" + Guid.NewGuid().ToString("N")[..8];

        // Consume all 10 tokens rapidly
        for (int i = 0; i < 10; i++)
            ModError.Report(modId, $"Error {i}");

        // 11th should be rate-limited
        ModError.Report(modId, "Dropped error");

        var errors = ModError.GetErrors(modId);
        Assert.Equal(10, errors.Count);
        Assert.DoesNotContain(errors, e => e.Message == "Dropped error");
    }

    [Fact]
    public void Deduplication_IncrementsSameMessage()
    {
        var modId = "dedup-" + Guid.NewGuid().ToString("N")[..8];

        ModError.Report(modId, "same message");
        ModError.Report(modId, "same message");
        ModError.Report(modId, "same message");

        var errors = ModError.GetErrors(modId);
        Assert.Single(errors);
        Assert.True(errors[0].OccurrenceCount >= 3);
    }

    [Fact]
    public void Deduplication_DifferentMessages_NotDeduped()
    {
        var modId = "nodedup-" + Guid.NewGuid().ToString("N")[..8];

        ModError.Report(modId, "message A");
        ModError.Report(modId, "message B");

        var errors = ModError.GetErrors(modId);
        Assert.Equal(2, errors.Count);
    }

    [Fact]
    public void OnError_EventFired()
    {
        ModErrorEntry received = null;
        void handler(ModErrorEntry e) { received = e; }

        ModError.OnError += handler;
        try
        {
            ModError.Report("event-mod", "event test");

            Assert.NotNull(received);
            Assert.Equal("event-mod", received.ModId);
            Assert.Equal("event test", received.Message);
        }
        finally
        {
            ModError.OnError -= handler;
        }
    }

    // --- Helpers ---

    private static void ResetModError()
    {
        ModError.Clear();

        // Clear rate buckets via reflection
        var bucketsField = typeof(ModError).GetField("_rateBuckets",
            BindingFlags.NonPublic | BindingFlags.Static);
        if (bucketsField != null)
        {
            var buckets = bucketsField.GetValue(null) as IDictionary;
            buckets?.Clear();
        }
    }

    private static void DirectAddEntries(string modId, int count)
    {
        var entriesField = typeof(ModError).GetField("_entries",
            BindingFlags.NonPublic | BindingFlags.Static);
        var lockField = typeof(ModError).GetField("_lock",
            BindingFlags.NonPublic | BindingFlags.Static);

        var entries = (IList)entriesField.GetValue(null);
        var lockObj = lockField.GetValue(null);

        lock (lockObj)
        {
            for (int i = 0; i < count; i++)
            {
                entries.Add(new ModErrorEntry
                {
                    ModId = modId,
                    Message = $"Error {i}",
                    Severity = ErrorSeverity.Error,
                    Timestamp = DateTime.UtcNow
                });
            }
        }
    }
}
