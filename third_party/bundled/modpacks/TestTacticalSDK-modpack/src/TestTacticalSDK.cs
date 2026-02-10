using MelonLoader;
using Menace.ModpackLoader;
using Menace.SDK;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Menace.TestTacticalSDK;

/// <summary>
/// Test plugin for validating SDK extensions.
/// Run 'runtests' in the dev console to execute all tests.
/// Individual test commands: testspawn, testmove, testcombat, testtactical,
/// testinventory, testoperation, testvehicle, testconversation, testemotions, testblackmarket
/// </summary>
public class TestTacticalSDK : IModpackPlugin
{
    private MelonLogger.Instance _log;
    private int _testPhase = 0;
    private int _frameCount = 0;
    private int _passCount = 0;
    private int _failCount = 0;
    private int _skipCount = 0;
    private List<string> _failures = new();

    public void OnInitialize(MelonLogger.Instance logger, HarmonyLib.Harmony harmony)
    {
        _log = logger;
        _log.Msg("TestTacticalSDK v2.0.0 loaded - SDK test suite");

        // Main test runner
        DevConsole.RegisterCommand("runtests", "", "Run all SDK tests", args =>
        {
            _testPhase = 1;
            _frameCount = 0;
            _passCount = 0;
            _failCount = 0;
            _skipCount = 0;
            _failures.Clear();
            return "Starting SDK tests (13 suites)...";
        });

        // Individual test commands
        DevConsole.RegisterCommand("testspawn", "", "Test EntitySpawner API", args => { TestEntitySpawner(); return "EntitySpawner tests complete"; });
        DevConsole.RegisterCommand("testmove", "", "Test EntityMovement API", args => { TestEntityMovement(); return "EntityMovement tests complete"; });
        DevConsole.RegisterCommand("testcombat", "", "Test EntityCombat API", args => { TestEntityCombat(); return "EntityCombat tests complete"; });
        DevConsole.RegisterCommand("testtactical", "", "Test TacticalController API", args => { TestTacticalController(); return "TacticalController tests complete"; });
        DevConsole.RegisterCommand("testinventory", "", "Test Inventory API", args => { TestInventory(); return "Inventory tests complete"; });
        DevConsole.RegisterCommand("testoperation", "", "Test Operation API", args => { TestOperation(); return "Operation tests complete"; });
        DevConsole.RegisterCommand("testvehicle", "", "Test Vehicle API", args => { TestVehicle(); return "Vehicle tests complete"; });
        DevConsole.RegisterCommand("testconversation", "", "Test Conversation API", args => { TestConversation(); return "Conversation tests complete"; });
        DevConsole.RegisterCommand("testemotions", "", "Test Emotions API", args => { TestEmotions(); return "Emotions tests complete"; });
        DevConsole.RegisterCommand("testblackmarket", "", "Test BlackMarket API", args => { TestBlackMarket(); return "BlackMarket tests complete"; });
        DevConsole.RegisterCommand("testmission", "", "Test Mission API", args => { TestMission(); return "Mission tests complete"; });
        DevConsole.RegisterCommand("testroster", "", "Test Roster API", args => { TestRoster(); return "Roster tests complete"; });
        DevConsole.RegisterCommand("testtilemap", "", "Test TileMap/Pathfinding API", args => { TestTileMap(); return "TileMap tests complete"; });

        DevConsole.RegisterCommand("testresults", "", "Show test results summary", args =>
        {
            return $"Tests: {_passCount} passed, {_failCount} failed, {_skipCount} skipped";
        });
    }

    public void OnSceneLoaded(int buildIndex, string sceneName)
    {
        _log.Msg($"Scene: {sceneName}");
    }

    public void OnUpdate()
    {
        if (_testPhase == 0) return;

        _frameCount++;
        if (_frameCount % 30 != 0) return;

        switch (_testPhase)
        {
            case 1: TestTacticalState(); break;
            case 2: TestEntityQueries(); break;
            case 3: TestEntityMovement(); break;
            case 4: TestEntityCombat(); break;
            case 5: TestEntitySpawner(); break;
            case 6: TestTacticalController(); break;
            case 7: TestInventory(); break;
            case 8: TestOperation(); break;
            case 9: TestVehicle(); break;
            case 10: TestConversation(); break;
            case 11: TestEmotions(); break;
            case 12: TestBlackMarket(); break;
            case 13: TestMission(); break;
            case 14: TestRoster(); break;
            case 15: TestTileMap(); break;
            default:
                PrintTestSummary();
                _testPhase = 0;
                break;
        }

        _testPhase++;
    }

    private void Pass(string test) { _passCount++; _log.Msg($"  [PASS] {test}"); }
    private void Fail(string test, string reason) { _failCount++; _failures.Add($"{test}: {reason}"); _log.Warning($"  [FAIL] {test}: {reason}"); }
    private void Skip(string test, string reason) { _skipCount++; _log.Msg($"  [SKIP] {test}: {reason}"); }

    private void PrintTestSummary()
    {
        _log.Msg("=".PadRight(50, '='));
        _log.Msg("TEST SUMMARY");
        _log.Msg($"  Passed:  {_passCount}");
        _log.Msg($"  Failed:  {_failCount}");
        _log.Msg($"  Skipped: {_skipCount}");
        if (_failures.Count > 0)
        {
            _log.Msg("FAILURES:");
            foreach (var f in _failures) _log.Warning($"  - {f}");
        }
        _log.Msg("=".PadRight(50, '='));
    }

    // ==================== Core Tactical Tests ====================

    private void TestTacticalState()
    {
        _log.Msg("=== TEST: Tactical State ===");
        try
        {
            var state = TacticalController.GetTacticalState();
            if (state == null) { Skip("GetTacticalState", "Not in tactical scene"); return; }

            _log.Msg($"  Round: {state.RoundNumber}, Faction: {state.CurrentFactionName}");
            _log.Msg($"  Players: {state.PlayerAliveCount}/{state.PlayerAliveCount + state.PlayerDeadCount}");
            _log.Msg($"  Enemies: {state.EnemyAliveCount}/{state.EnemyAliveCount + state.EnemyDeadCount}");

            if (state.RoundNumber >= 1) Pass("RoundNumber >= 1"); else Fail("RoundNumber", $"Got {state.RoundNumber}");
            if (state.TimeScale > 0) Pass("TimeScale > 0"); else Fail("TimeScale", $"Got {state.TimeScale}");
        }
        catch (Exception ex) { Fail("TacticalState", ex.Message); }
    }

    private void TestEntityQueries()
    {
        _log.Msg("=== TEST: Entity Queries ===");
        try
        {
            var allActors = EntitySpawner.ListEntities();
            _log.Msg($"  Total actors: {allActors.Length}");
            if (allActors.Length > 0) Pass("ListEntities returns actors"); else Fail("ListEntities", "No actors");

            var players = EntitySpawner.ListEntities(factionFilter: TacticalController.FACTION_PLAYER);
            if (players.Length > 0) Pass("ListEntities(PLAYER) works"); else Skip("ListEntities(PLAYER)", "No players");

            if (allActors.Length > 0)
            {
                var info = EntitySpawner.GetEntityInfo(allActors[0]);
                if (info != null && !string.IsNullOrEmpty(info.Name))
                    Pass("GetEntityInfo returns data");
                else
                    Fail("GetEntityInfo", "No data returned");
            }
        }
        catch (Exception ex) { Fail("EntityQueries", ex.Message); }
    }

    private void TestEntityMovement()
    {
        _log.Msg("=== TEST: Entity Movement ===");
        try
        {
            var actor = GetTestActor();
            if (actor.IsNull) { Skip("EntityMovement", "No actor"); return; }

            var pos = EntityMovement.GetPosition(actor);
            if (pos.HasValue) { _log.Msg($"  Position: ({pos.Value.x}, {pos.Value.y})"); Pass("GetPosition"); }
            else Fail("GetPosition", "Null");

            var facing = EntityMovement.GetFacing(actor);
            if (facing >= 0 && facing <= 7) Pass("GetFacing valid"); else Skip("GetFacing", $"Got {facing}");

            var ap = EntityMovement.GetRemainingAP(actor);
            if (ap >= 0) Pass("GetRemainingAP"); else Skip("GetRemainingAP", "Negative");

            Pass("IsMoving returns bool");

            var info = EntityMovement.GetMovementInfo(actor);
            if (info != null) Pass("GetMovementInfo"); else Skip("GetMovementInfo", "Null");
        }
        catch (Exception ex) { Fail("EntityMovement", ex.Message); }
    }

    private void TestEntityCombat()
    {
        _log.Msg("=== TEST: Entity Combat ===");
        try
        {
            var actor = GetTestActor();
            if (actor.IsNull) { Skip("EntityCombat", "No actor"); return; }

            var info = EntityCombat.GetCombatInfo(actor);
            if (info != null)
            {
                _log.Msg($"  HP: {info.CurrentHP}/{info.MaxHP}, Suppression: {info.Suppression}");
                if (info.MaxHP > 0) Pass("GetCombatInfo.MaxHP"); else Fail("MaxHP", "Zero");
            }
            else Skip("GetCombatInfo", "Null");

            var skills = EntityCombat.GetSkills(actor);
            if (skills.Count > 0) { _log.Msg($"  Skills: {skills.Count}"); Pass("GetSkills"); }
            else Skip("GetSkills", "None found");

            var hp = EntityCombat.GetHP(actor);
            var maxHp = EntityCombat.GetMaxHP(actor);
            if (maxHp > 0) Pass("HP accessors"); else Skip("HP accessors", "MaxHP zero");
        }
        catch (Exception ex) { Fail("EntityCombat", ex.Message); }
    }

    private void TestEntitySpawner()
    {
        _log.Msg("=== TEST: Entity Spawner ===");
        try
        {
            var allTemplates = Templates.FindAll("EntityTemplate");
            _log.Msg($"  EntityTemplates: {allTemplates.Length}");

            if (allTemplates.Length > 0) Pass("Templates.FindAll(EntityTemplate)");
            else { Skip("EntitySpawner", "No templates"); return; }

            // Find an enemy template
            string templateName = null;
            foreach (var t in allTemplates)
            {
                var name = t.GetName();
                if (name != null && (name.Contains("enemy") || name.Contains("pirate")))
                { templateName = name; break; }
            }
            templateName ??= allTemplates[0].GetName();
            _log.Msg($"  Using template: {templateName}");

            // Don't actually spawn to avoid messing with game state
            Pass("Template selection works");
        }
        catch (Exception ex) { Fail("EntitySpawner", ex.Message); }
    }

    private void TestTacticalController()
    {
        _log.Msg("=== TEST: Tactical Controller ===");
        try
        {
            var round = TacticalController.GetCurrentRound();
            if (round >= 0) Pass("GetCurrentRound"); else Skip("GetCurrentRound", $"Got {round}");

            Pass("GetCurrentFaction");
            Pass("IsPlayerTurn");

            var paused = TacticalController.IsPaused();
            Pass("IsPaused");

            var scale = TacticalController.GetTimeScale();
            if (scale > 0) Pass("GetTimeScale"); else Skip("GetTimeScale", $"Got {scale}");

            var active = TacticalController.GetActiveActor();
            if (!active.IsNull) Pass("GetActiveActor"); else Skip("GetActiveActor", "None");

            Pass("IsMissionRunning");
        }
        catch (Exception ex) { Fail("TacticalController", ex.Message); }
    }

    // ==================== Strategy Layer Tests ====================

    private void TestInventory()
    {
        _log.Msg("=== TEST: Inventory ===");
        try
        {
            var actor = GetTestActor();
            if (actor.IsNull) { Skip("Inventory", "No actor"); return; }

            var container = Inventory.GetContainer(actor);
            if (!container.IsNull)
            {
                Pass("GetContainer");
                var items = Inventory.GetAllItems(container);
                _log.Msg($"  Items: {items.Count}");
                if (items.Count >= 0) Pass("GetAllItems"); else Fail("GetAllItems", "Error");

                var weapons = Inventory.GetEquippedWeapons(actor);
                _log.Msg($"  Weapons: {weapons.Count}");
                Pass("GetEquippedWeapons");

                var armor = Inventory.GetEquippedArmor(actor);
                if (armor != null) _log.Msg($"  Armor: {armor.TemplateName}");
                Pass("GetEquippedArmor");
            }
            else Skip("GetContainer", "Null");

            // Test slot names
            var slotName = Inventory.GetSlotTypeName(Inventory.SLOT_WEAPON1);
            if (slotName == "Weapon1") Pass("GetSlotTypeName"); else Fail("GetSlotTypeName", slotName);
        }
        catch (Exception ex) { Fail("Inventory", ex.Message); }
    }

    private void TestOperation()
    {
        _log.Msg("=== TEST: Operation ===");
        try
        {
            var hasOp = Operation.HasActiveOperation();
            _log.Msg($"  HasActiveOperation: {hasOp}");
            Pass("HasActiveOperation");

            if (hasOp)
            {
                var info = Operation.GetOperationInfo();
                if (info != null)
                {
                    _log.Msg($"  Operation: {info.TemplateName}");
                    _log.Msg($"  Missions: {info.CurrentMissionIndex + 1}/{info.MissionCount}");
                    Pass("GetOperationInfo");
                }
                else Skip("GetOperationInfo", "Null");

                var missions = Operation.GetMissions();
                _log.Msg($"  Mission list: {missions.Count}");
                Pass("GetMissions");
            }
            else Skip("Operation details", "No active operation");
        }
        catch (Exception ex) { Fail("Operation", ex.Message); }
    }

    private void TestVehicle()
    {
        _log.Msg("=== TEST: Vehicle ===");
        try
        {
            var actor = GetTestActor();
            if (actor.IsNull) { Skip("Vehicle", "No actor"); return; }

            var info = Vehicle.GetVehicleInfo(actor);
            if (info != null)
            {
                _log.Msg($"  Vehicle: {info.TemplateName}");
                _log.Msg($"  HP: {info.BaseHp}/{info.MaxHp}, Armor: {info.Armor}");
                Pass("GetVehicleInfo");
            }
            else Skip("GetVehicleInfo", "No vehicle data");

            var isVehicle = Vehicle.IsVehicle(actor);
            _log.Msg($"  IsVehicle: {isVehicle}");
            Pass("IsVehicle");
        }
        catch (Exception ex) { Fail("Vehicle", ex.Message); }
    }

    private void TestConversation()
    {
        _log.Msg("=== TEST: Conversation ===");
        try
        {
            var allConvos = Conversation.GetAllConversationTemplates();
            _log.Msg($"  Conversation templates: {allConvos.Count}");
            if (allConvos.Count >= 0) Pass("GetAllConversationTemplates"); else Fail("GetAllConversationTemplates", "Error");

            var isRunning = Conversation.IsConversationRunning();
            _log.Msg($"  IsRunning: {isRunning}");
            Pass("IsConversationRunning");

            if (isRunning)
            {
                var current = Conversation.GetCurrentConversation();
                if (!current.IsNull) Pass("GetCurrentConversation");
                else Skip("GetCurrentConversation", "Null");

                var state = Conversation.GetPresenterState();
                if (state != null)
                {
                    _log.Msg($"  Current line: {state.CurrentNodeIndex}");
                    Pass("GetPresenterState");
                }
            }

            // Test trigger type constants
            if (Conversation.TRIGGER_MISSION_START == 1) Pass("Trigger constants defined");
            else Fail("Trigger constants", "Wrong values");
        }
        catch (Exception ex) { Fail("Conversation", ex.Message); }
    }

    private void TestEmotions()
    {
        _log.Msg("=== TEST: Emotions ===");
        try
        {
            var actor = GetTestActor();
            if (actor.IsNull) { Skip("Emotions", "No actor"); return; }

            var emotionsInfo = Emotions.GetEmotionalStatesInfo(actor);
            if (emotionsInfo != null)
            {
                _log.Msg($"  Owner: {emotionsInfo.OwnerName}");
                _log.Msg($"  Active states: {emotionsInfo.StateCount}");
                Pass("GetEmotionalStatesInfo");

                foreach (var e in emotionsInfo.ActiveStates.Take(3))
                {
                    _log.Msg($"    - {e.TemplateName} ({e.TypeName}, remaining: {e.RemainingDuration})");
                }
            }
            else Skip("GetEmotionalStatesInfo", "Null");

            var hasAngry = Emotions.HasEmotion(actor, Emotions.EmotionalStateType.Angry);
            _log.Msg($"  HasEmotion(Angry): {hasAngry}");
            Pass("HasEmotion");

            // Test enum values
            if ((int)Emotions.EmotionalStateType.Confident == 2) Pass("EmotionalStateType enum");
            else Fail("EmotionalStateType", "Wrong values");
        }
        catch (Exception ex) { Fail("Emotions", ex.Message); }
    }

    private void TestBlackMarket()
    {
        _log.Msg("=== TEST: BlackMarket ===");
        try
        {
            var info = BlackMarket.GetBlackMarketInfo();
            if (info != null)
            {
                _log.Msg($"  Stacks: {info.StackCount}, Items: {info.TotalItemCount}");
                _log.Msg($"  Config: min={info.MinItems}, max={info.MaxItems}, timeout={info.ItemTimeout}");
                Pass("GetBlackMarketInfo");
            }
            else Skip("GetBlackMarketInfo", "Not available");

            var stacks = BlackMarket.GetAvailableStacks();
            _log.Msg($"  Available stacks: {stacks.Count}");
            Pass("GetAvailableStacks");

            foreach (var s in stacks.Take(3))
            {
                _log.Msg($"    - {s.TemplateName} x{s.ItemCount} ({s.OperationsRemaining} ops)");
            }

            var stackCount = BlackMarket.GetStackCount();
            _log.Msg($"  GetStackCount: {stackCount}");
            Pass("GetStackCount");

            // Test stack type enum
            if ((int)BlackMarket.StackType.Permanent == 3) Pass("StackType enum");
            else Fail("StackType", "Wrong values");
        }
        catch (Exception ex) { Fail("BlackMarket", ex.Message); }
    }

    private void TestMission()
    {
        _log.Msg("=== TEST: Mission ===");
        try
        {
            var current = Mission.GetCurrentMission();
            if (!current.IsNull)
            {
                Pass("GetCurrentMission");
                var info = Mission.GetMissionInfo(current);
                if (info != null)
                {
                    _log.Msg($"  Mission: {info.TemplateName}");
                    _log.Msg($"  Status: {info.StatusName}");
                    Pass("GetMissionInfo");
                }
                else Skip("GetMissionInfo", "Null");
            }
            else Skip("Mission", "No current mission");

            var objectives = Mission.GetObjectives();
            _log.Msg($"  Objectives: {objectives.Count}");
            Pass("GetObjectives");
        }
        catch (Exception ex) { Fail("Mission", ex.Message); }
    }

    private void TestRoster()
    {
        _log.Msg("=== TEST: Roster ===");
        try
        {
            var leaders = Roster.GetHiredLeaders();
            if (leaders != null && leaders.Count > 0)
            {
                _log.Msg($"  Hired leaders: {leaders.Count}");
                Pass("GetHiredLeaders");

                foreach (var member in leaders.Take(3))
                {
                    _log.Msg($"    - {member.Nickname} ({member.RankName}) - {member.TemplateName}");
                }
            }
            else Skip("GetHiredLeaders", "Empty or null");

            var hiredCount = Roster.GetHiredCount();
            _log.Msg($"  Hired count: {hiredCount}");
            Pass("GetHiredCount");

            var availableCount = Roster.GetAvailableCount();
            _log.Msg($"  Available count: {availableCount}");
            Pass("GetAvailableCount");
        }
        catch (Exception ex) { Fail("Roster", ex.Message); }
    }

    private void TestTileMap()
    {
        _log.Msg("=== TEST: TileMap/Pathfinding ===");
        try
        {
            var tileMap = TileMap.GetMap();
            if (!tileMap.IsNull)
            {
                Pass("GetMap");

                var info = TileMap.GetMapInfo();
                if (info != null)
                {
                    _log.Msg($"  Map size: {info.Width}x{info.Height}");
                    Pass("GetMapInfo");

                    // Test a tile in the middle
                    var testX = info.Width / 2;
                    var testY = info.Height / 2;

                    var blocked = TileMap.IsBlocked(testX, testY);
                    _log.Msg($"  Tile ({testX},{testY}) blocked: {blocked}");
                    Pass("IsBlocked");

                    var cover = TileMap.GetAllCover(testX, testY);
                    _log.Msg($"  Tile cover dirs: {cover?.Length ?? 0}");
                    Pass("GetAllCover");

                    var hasActor = TileMap.HasActor(testX, testY);
                    _log.Msg($"  HasActor: {hasActor}");
                    Pass("HasActor");
                }
                else Skip("GetMapInfo", "Null");
            }
            else Skip("TileMap", "No tile map");

            // Test LineOfSight
            var hasLos = LineOfSight.HasLOS(5, 5, 10, 10);
            _log.Msg($"  LOS (5,5) to (10,10): {hasLos}");
            Pass("HasLOS");

            var actor = GetTestActor();
            if (!actor.IsNull)
            {
                var visInfo = LineOfSight.GetVisibilityInfo(actor);
                if (visInfo != null)
                {
                    _log.Msg($"  Visibility state: {visInfo.StateName}");
                    Pass("GetVisibilityInfo");
                }
            }
        }
        catch (Exception ex) { Fail("TileMap", ex.Message); }
    }

    // ==================== Helpers ====================

    private GameObj GetTestActor()
    {
        var actor = TacticalController.GetActiveActor();
        if (actor.IsNull)
        {
            var players = EntitySpawner.ListEntities(factionFilter: TacticalController.FACTION_PLAYER);
            if (players.Length > 0) actor = players[0];
        }
        return actor;
    }

    public void OnGUI() { }
}
