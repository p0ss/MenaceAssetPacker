#nullable disable
using System;
using System.Collections;
using System.Linq;
using MelonLoader;
using Menace.SDK;
using Menace.SDK.Repl;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Menace.ModpackLoader;

/// <summary>
/// Console commands for automated testing.
/// Enables agents to control game flow, navigate scenes, and assert conditions.
/// </summary>
public static class TestHarnessCommands
{
    private static UnityEngine.Coroutine _waitCoroutine;
    private static bool _waitComplete;
    private static string _waitResult;

    public static void Register()
    {
        // test.status - Get test harness status
        DevConsole.RegisterCommand("test.status", "", "Get test harness status", _ =>
        {
            return $"Test Harness Ready\n" +
                   $"  Current Scene: {GameState.CurrentScene}\n" +
                   $"  Is Tactical: {GameState.IsTactical}\n" +
                   $"  REPL Available: {ReplPanel.IsAvailable}\n" +
                   $"  MCP Server: {Mcp.GameMcpServer.IsRunning}";
        });

        // test.scene - Get or set current scene
        DevConsole.RegisterCommand("test.scene", "[scene_name]", "Get current scene or load a new scene", args =>
        {
            if (args.Length == 0)
            {
                return $"Current scene: {GameState.CurrentScene}";
            }

            var sceneName = args[0];
            try
            {
                MainThreadExecutor.Enqueue(() => SceneManager.LoadScene(sceneName));
                return $"Loading scene: {sceneName}";
            }
            catch (Exception ex)
            {
                return $"Error loading scene: {ex.Message}";
            }
        });

        // test.goto_main - Navigate to main menu
        DevConsole.RegisterCommand("test.goto_main", "", "Navigate to main menu", _ =>
        {
            try
            {
                MainThreadExecutor.Enqueue(() => SceneManager.LoadScene("MainMenu"));
                return "Loading main menu";
            }
            catch (Exception ex)
            {
                return $"Error: {ex.Message}";
            }
        });

        // test.goto_strategy - Navigate to strategy map
        DevConsole.RegisterCommand("test.goto_strategy", "", "Navigate to strategy map", _ =>
        {
            try
            {
                MainThreadExecutor.Enqueue(() => SceneManager.LoadScene("StrategyMap"));
                return "Loading strategy map";
            }
            catch (Exception ex)
            {
                return $"Error: {ex.Message}";
            }
        });

        // test.start_mission - Start a tactical mission with specific parameters
        DevConsole.RegisterCommand("test.start_mission", "<seed> <difficulty> [template]",
            "Start a test mission with specific seed and difficulty", args =>
            {
                if (args.Length < 2)
                    return "Usage: test.start_mission <seed> <difficulty> [template]";

                if (!int.TryParse(args[0], out int seed))
                    return "Invalid seed (must be integer)";

                if (!int.TryParse(args[1], out int difficulty))
                    return "Invalid difficulty (must be integer)";

                var templateName = args.Length > 2 ? args[2] : null;

                try
                {
                    // Store test mission parameters for TryCreateMapLayout patch to use
                    TestMissionState.SetTestMission(seed, difficulty, templateName);

                    // For now, just set the flag - actual mission creation needs strategy context
                    // This will be used by custom maps system when it's implemented
                    return $"Test mission queued: seed={seed}, difficulty={difficulty}, template={templateName ?? "default"}\n" +
                           "Note: Full mission creation requires strategy state integration (custom maps feature)";
                }
                catch (Exception ex)
                {
                    return $"Error: {ex.Message}";
                }
            });

        // test.wait - Wait for a condition or duration
        DevConsole.RegisterCommand("test.wait", "<condition_or_ms> [timeout_ms]",
            "Wait for a condition to be true or wait for specified milliseconds", args =>
            {
                if (args.Length == 0)
                    return "Usage: test.wait <condition_or_ms> [timeout_ms]\n" +
                           "Examples:\n" +
                           "  test.wait 5000                 # Wait 5 seconds\n" +
                           "  test.wait scene_ready 10000    # Wait for scene, max 10s\n" +
                           "  test.wait mission_ready 30000  # Wait for mission, max 30s";

                // Check if it's a simple duration
                if (int.TryParse(args[0], out int durationMs))
                {
                    MelonCoroutines.Start(WaitForDuration(durationMs));
                    return $"Waiting {durationMs}ms...";
                }

                // It's a condition
                var condition = args[0];
                var timeout = args.Length > 1 && int.TryParse(args[1], out int t) ? t : 10000;

                MelonCoroutines.Start(WaitForCondition(condition, timeout));
                return $"Waiting for '{condition}' (timeout: {timeout}ms)...";
            });

        // test.wait_result - Get result of last wait operation
        DevConsole.RegisterCommand("test.wait_result", "", "Get result of last wait operation", _ =>
        {
            if (_waitCoroutine == null)
                return "No wait operation in progress";

            if (!_waitComplete)
                return "Wait operation still in progress";

            return _waitResult ?? "Wait completed";
        });

        // test.assert - Assert a condition is true
        DevConsole.RegisterCommand("test.assert", "<expression> <expected>",
            "Assert that an expression equals expected value", args =>
            {
                if (args.Length < 2)
                    return "Usage: test.assert <expression> <expected>\n" +
                           "Example: test.assert 'mission.Seed' '12345'";

                var expression = args[0];
                var expected = args[1];

                try
                {
                    // Use REPL to evaluate expression
                    if (!ReplPanel.IsAvailable)
                        return "ERROR: REPL not available - assertions require Roslyn";

                    var result = ReplPanel.Evaluate(expression);
                    if (!result.Success)
                        return $"ASSERTION FAILED: Could not evaluate '{expression}'\nError: {result.Error}";

                    var actual = result.DisplayText?.Trim();
                    if (actual == expected)
                    {
                        return $"ASSERTION PASSED: {expression} == {expected}";
                    }
                    else
                    {
                        return $"ASSERTION FAILED: {expression}\n" +
                               $"  Expected: {expected}\n" +
                               $"  Actual:   {actual}";
                    }
                }
                catch (Exception ex)
                {
                    return $"ASSERTION ERROR: {ex.Message}";
                }
            });

        // test.assert_contains - Assert a value contains a substring
        DevConsole.RegisterCommand("test.assert_contains", "<expression> <substring>",
            "Assert that an expression result contains a substring", args =>
            {
                if (args.Length < 2)
                    return "Usage: test.assert_contains <expression> <substring>";

                var expression = args[0];
                var substring = args[1];

                try
                {
                    if (!ReplPanel.IsAvailable)
                        return "ERROR: REPL not available";

                    var result = ReplPanel.Evaluate(expression);
                    if (!result.Success)
                        return $"ASSERTION FAILED: Could not evaluate '{expression}'\nError: {result.Error}";

                    var actual = result.DisplayText?.Trim() ?? "";
                    if (actual.Contains(substring))
                    {
                        return $"ASSERTION PASSED: {expression} contains '{substring}'";
                    }
                    else
                    {
                        return $"ASSERTION FAILED: {expression} does not contain '{substring}'\n" +
                               $"  Actual value: {actual}";
                    }
                }
                catch (Exception ex)
                {
                    return $"ASSERTION ERROR: {ex.Message}";
                }
            });

        // test.screenshot - Capture screenshot
        DevConsole.RegisterCommand("test.screenshot", "<filename>",
            "Capture screenshot to TestScreenshots folder", args =>
            {
                if (args.Length == 0)
                    return "Usage: test.screenshot <filename>";

                try
                {
                    var filename = args[0];
                    if (!filename.EndsWith(".png"))
                        filename += ".png";

                    var dir = System.IO.Path.Combine(Application.dataPath, "..", "TestScreenshots");
                    if (!System.IO.Directory.Exists(dir))
                        System.IO.Directory.CreateDirectory(dir);

                    var path = System.IO.Path.Combine(dir, filename);

                    // Use Application.CaptureScreenshot for IL2CPP compatibility
                    // Takes relative path from project root
                    var relativePath = System.IO.Path.Combine("TestScreenshots", filename);
                    Application.CaptureScreenshot(relativePath);

                    return $"Screenshot saved: {path}";
                }
                catch (Exception ex)
                {
                    return $"Error: {ex.Message}";
                }
            });

        // test.eval - Evaluate expression (alias for REPL for testing)
        DevConsole.RegisterCommand("test.eval", "<expression>",
            "Evaluate C# expression and return result", args =>
            {
                if (args.Length == 0)
                    return "Usage: test.eval <expression>";

                if (!ReplPanel.IsAvailable)
                    return "ERROR: REPL not available";

                try
                {
                    var result = ReplPanel.Evaluate(string.Join(" ", args));
                    return result.Success
                        ? $"Result: {result.DisplayText}"
                        : $"Error: {result.Error}";
                }
                catch (Exception ex)
                {
                    return $"Error: {ex.Message}";
                }
            });

        // test.inspect - Inspect game object or value
        DevConsole.RegisterCommand("test.inspect", "<path>",
            "Inspect a game object or value by path (e.g., 'mission.Seed', 'TileMap.Width')", args =>
            {
                if (args.Length == 0)
                    return "Usage: test.inspect <path>\n" +
                           "Examples:\n" +
                           "  test.inspect GameState.CurrentScene\n" +
                           "  test.inspect TileMap.GetMapInfo().Width";

                var path = string.Join(" ", args);

                try
                {
                    if (!ReplPanel.IsAvailable)
                        return "ERROR: REPL not available";

                    var result = ReplPanel.Evaluate(path);
                    if (!result.Success)
                        return $"Error evaluating '{path}': {result.Error}";

                    return $"{path} = {result.DisplayText}";
                }
                catch (Exception ex)
                {
                    return $"Error: {ex.Message}";
                }
            });

        SdkLogger.Msg("[TestHarness] Registered test commands");
    }

    private static IEnumerator WaitForDuration(int milliseconds)
    {
        _waitComplete = false;
        _waitResult = null;

        var seconds = milliseconds / 1000f;
        yield return new WaitForSeconds(seconds);

        _waitComplete = true;
        _waitResult = $"Waited {milliseconds}ms";
        _waitCoroutine = null;
    }

    private static IEnumerator WaitForCondition(string condition, int timeoutMs)
    {
        _waitComplete = false;
        _waitResult = null;

        var startTime = Time.realtimeSinceStartup;
        var timeoutSeconds = timeoutMs / 1000f;

        while (Time.realtimeSinceStartup - startTime < timeoutSeconds)
        {
            bool conditionMet = CheckCondition(condition);

            if (conditionMet)
            {
                _waitComplete = true;
                _waitResult = $"Condition '{condition}' met after {(Time.realtimeSinceStartup - startTime) * 1000:F0}ms";
                _waitCoroutine = null;
                yield break;
            }

            yield return new WaitForSeconds(0.5f);
        }

        _waitComplete = true;
        _waitResult = $"TIMEOUT: Condition '{condition}' not met after {timeoutMs}ms";
        _waitCoroutine = null;
    }

    private static bool CheckCondition(string condition)
    {
        try
        {
            return condition.ToLowerInvariant() switch
            {
                "scene_ready" => !string.IsNullOrEmpty(GameState.CurrentScene),
                "tactical_ready" => GameState.IsTactical,
                "mission_ready" => GameState.IsTactical && TacticalController.GetTacticalState()?.IsMissionRunning == true,
                "strategy_ready" => GameState.CurrentScene?.Contains("Strategy") == true,
                _ => false
            };
        }
        catch
        {
            return false;
        }
    }
}

/// <summary>
/// Stores test mission parameters for use by map generation system.
/// </summary>
public static class TestMissionState
{
    public static int? TestSeed { get; private set; }
    public static int? TestDifficulty { get; private set; }
    public static string TestTemplate { get; private set; }
    public static bool HasTestMission => TestSeed.HasValue;

    public static void SetTestMission(int seed, int difficulty, string template)
    {
        TestSeed = seed;
        TestDifficulty = difficulty;
        TestTemplate = template;
        SdkLogger.Msg($"[TestHarness] Test mission set: seed={seed}, difficulty={difficulty}, template={template ?? "default"}");
    }

    public static void ClearTestMission()
    {
        TestSeed = null;
        TestDifficulty = null;
        TestTemplate = null;
    }

    public static (int seed, int difficulty, string template) GetTestMission()
    {
        if (!HasTestMission)
            throw new InvalidOperationException("No test mission set");

        return (TestSeed.Value, TestDifficulty.Value, TestTemplate);
    }
}
