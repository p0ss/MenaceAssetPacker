// Auto-generated SDK - DO NOT EDIT
// Generated from IL2CPP dump.cs

using System;
using UnityEngine;
using Il2CppInterop.Runtime;

namespace Menace.SDK.Generated
{
    // Base class for safe wrappers
    public abstract class SafeGameObject
    {
        protected readonly IntPtr Pointer;
        protected SafeGameObject(Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase obj) { Pointer = obj?.Pointer ?? IntPtr.Zero; }
        protected SafeGameObject(IntPtr ptr) { Pointer = ptr; }
        public bool IsValid => Pointer != IntPtr.Zero && IsNativeAlive(Pointer);
        
        protected T SafeRead<T>(Func<T> getter, T fallback = default)
        {
            if (!IsValid) return fallback;
            try { return getter(); }
            catch { return fallback; }
        }
        
        protected void SafeCall(Action action)
        {
            if (!IsValid) return;
            try { action(); }
            catch { }
        }
        
        protected T SafeCall<T>(Func<T> func, T fallback = default)
        {
            if (!IsValid) return fallback;
            try { return func(); }
            catch { return fallback; }
        }
        
        private static bool IsNativeAlive(IntPtr ptr)
        {
            // TODO: Check m_CachedPtr offset
            return ptr != IntPtr.Zero;
        }
    }

    /// <summary>Safe wrapper for Menace.Tactical.AI.AIConfig</summary>
    public class AIConfigSafe : SafeGameObject
    {
        private readonly AIConfig _obj;
    
        public AIConfigSafe(AIConfig obj) : base(obj) { _obj = obj; }
        public AIConfigSafe(IntPtr ptr) : base(ptr) { _obj = new AIConfig(ptr); }
    
    
    }
    
    /// <summary>Safe wrapper for Menace.Tactical.AI.AIFaction</summary>
    public class AIFactionSafe : SafeGameObject
    {
        private readonly AIFaction _obj;
    
        public AIFactionSafe(AIFaction obj) : base(obj) { _obj = obj; }
        public AIFactionSafe(IntPtr ptr) : base(ptr) { _obj = new AIFaction(ptr); }
    
    
        public List<Opponent> GetOpponents() => SafeCall(() => _obj.GetOpponents());
        public StrategyData GetStrategy() => SafeCall(() => _obj.GetStrategy());
        public OperationalZones GetZones() => SafeCall(() => _obj.GetZones());
        public float GetTime() => SafeCall(() => _obj.GetTime());
        public int GetPickableActorsCount() => SafeCall(() => _obj.GetPickableActorsCount());
        public bool IsThinking() => SafeCall(() => _obj.IsThinking());
        public override bool IsAlliedWithPlayer() => SafeCall(() => _obj.IsAlliedWithPlayer());
        public override bool IsAlliedWith(int _faction) => SafeCall(() => _obj.IsAlliedWith(_faction));
        public override void Process() => SafeCall(() => _obj.Process());
        public override void OnTurnStart() => SafeCall(() => _obj.OnTurnStart());
        public override void OnRoundStart() => SafeCall(() => _obj.OnRoundStart());
        public override void OnOpponentSighted(Actor _a, int _ttl) => SafeCall(() => _obj.OnOpponentSighted(_a, _ttl));
        public override void OnActorDeath(Actor _actor) => SafeCall(() => _obj.OnActorDeath(_actor));
        public bool HasKnownOpponent() => SafeCall(() => _obj.HasKnownOpponent());
        public Opponent GetOpponent(Actor _actor) => SafeCall(() => _obj.GetOpponent(_actor));
    }
    
    /// <summary>Safe wrapper for Menace.Tactical.AI.AIWeightsTemplate</summary>
    public class AIWeightsTemplateSafe : SafeGameObject
    {
        private readonly AIWeightsTemplate _obj;
    
        public AIWeightsTemplateSafe(AIWeightsTemplate obj) : base(obj) { _obj = obj; }
        public AIWeightsTemplateSafe(IntPtr ptr) : base(ptr) { _obj = new AIWeightsTemplate(ptr); }
    
    
    }
    
    /// <summary>Safe wrapper for Menace.Tactical.AI.Agent</summary>
    public class AgentSafe : SafeGameObject
    {
        private readonly Agent _obj;
    
        public AgentSafe(Agent obj) : base(obj) { _obj = obj; }
        public AgentSafe(IntPtr ptr) : base(ptr) { _obj = new Agent(ptr); }
    
        public bool FlaggedForDeactivation => SafeRead(() => _obj.FlaggedForDeactivation);
    
        public bool get_FlaggedForDeactivation() => SafeCall(() => _obj.get_FlaggedForDeactivation());
        public void set_FlaggedForDeactivation(bool value) => SafeCall(() => _obj.set_FlaggedForDeactivation(value));
        public int GetScore() => SafeCall(() => _obj.GetScore());
        public Actor GetActor() => SafeCall(() => _obj.GetActor());
        public RoleData GetRole() => SafeCall(() => _obj.GetRole());
        public AIFaction GetFaction() => SafeCall(() => _obj.GetFaction());
        public PseudoRandom GetRandom() => SafeCall(() => _obj.GetRandom());
        public List<Behavior> GetBehaviors() => SafeCall(() => _obj.GetBehaviors());
        public Agent.State GetState() => SafeCall(() => _obj.GetState());
        public OperationalZones GetZones() => SafeCall(() => _obj.GetZones());
        public int GetNumThreatsFaced() => SafeCall(() => _obj.GetNumThreatsFaced());
        public bool IsDeployed() => SafeCall(() => _obj.IsDeployed());
        public bool IsSleeping() => SafeCall(() => _obj.IsSleeping());
        public void SetSleeping(bool _s) => SafeCall(() => _obj.SetSleeping(_s));
        public void SetDeployed(bool _d) => SafeCall(() => _obj.SetDeployed(_d));
        public void SetFaction(AIFaction _f) => SafeCall(() => _obj.SetFaction(_f));
        public void SetPriority(float _p) => SafeCall(() => _obj.SetPriority(_p));
        public float GetPriority() => SafeCall(() => _obj.GetPriority());
        public void SetNumThreatsFaced(int _t) => SafeCall(() => _obj.SetNumThreatsFaced(_t));
        public bool HasFlag(Agent.Flag _f) => SafeCall(() => _obj.HasFlag(_f));
        public void SetFlag(Agent.Flag _f, bool _v) => SafeCall(() => _obj.SetFlag(_f, _v));
        public Dictionary<Tile, TileScore> GetTiles() => SafeCall(() => _obj.GetTiles());
        public void OnSkillAdded(Skill _skill) => SafeCall(() => _obj.OnSkillAdded(_skill));
        public void OnSkillRemoved(Skill _skill) => SafeCall(() => _obj.OnSkillRemoved(_skill));
        public void Evaluate() => SafeCall(() => _obj.Evaluate());
        public bool Execute() => SafeCall(() => _obj.Execute());
        public void Reset() => SafeCall(() => _obj.Reset());
        public void Dispose() => SafeCall(() => _obj.Dispose());
        public float GetScoreMultForPickingThisAgent() => SafeCall(() => _obj.GetScoreMultForPickingThisAgent());
        public float GetThreatLevel() => SafeCall(() => _obj.GetThreatLevel());
        public void Sleep(float _seconds = 1) => SafeCall(() => _obj.Sleep(1));
        public void OnTurnStart() => SafeCall(() => _obj.OnTurnStart());
        public void OnBeforeProcessing() => SafeCall(() => _obj.OnBeforeProcessing());
        public void OnAfterProcessing() => SafeCall(() => _obj.OnAfterProcessing());
        public void OnPicked() => SafeCall(() => _obj.OnPicked());
        public bool IsDeploymentPhase() => SafeCall(() => _obj.IsDeploymentPhase());
    }
    
    /// <summary>Safe wrapper for Menace.Tactical.AI.BaseFaction</summary>
    public class BaseFactionSafe : SafeGameObject
    {
        private readonly BaseFaction _obj;
    
        public BaseFactionSafe(BaseFaction obj) : base(obj) { _obj = obj; }
        public BaseFactionSafe(IntPtr ptr) : base(ptr) { _obj = new BaseFaction(ptr); }
    
    
        public int GetIndex() => SafeCall(() => _obj.GetIndex());
        public FactionType GetFactionType() => SafeCall(() => _obj.GetFactionType());
        public List<Actor> GetActors() => SafeCall(() => _obj.GetActors());
        public List<Actor> GetDeadActors() => SafeCall(() => _obj.GetDeadActors());
        public bool HasActors() => SafeCall(() => _obj.HasActors());
        public bool HasUnlockedActors() => SafeCall(() => _obj.HasUnlockedActors());
        public int GetActorCount(bool _alive, bool _dead, Nullable<ActorType> _actorType) => SafeCall(() => _obj.GetActorCount(_alive, _dead, _actorType));
        public float GetDeadActorFractionCount() => SafeCall(() => _obj.GetDeadActorFractionCount());
        public bool IsActive() => SafeCall(() => _obj.IsActive());
        public void AddActor(Actor _actor) => SafeCall(() => _obj.AddActor(_actor));
        public void RemoveActor(Actor _actor) => SafeCall(() => _obj.RemoveActor(_actor));
        public int GetAmountOfActorsLeftToAct() => SafeCall(() => _obj.GetAmountOfActorsLeftToAct());
        public void OnActorDeath(Actor _actor) => SafeCall(() => _obj.OnActorDeath(_actor));
        public void OnTurnStart() => SafeCall(() => _obj.OnTurnStart());
        public void OnRoundStart() => SafeCall(() => _obj.OnRoundStart());
        public void OnOpponentSighted(Actor _a, int _ttl) => SafeCall(() => _obj.OnOpponentSighted(_a, _ttl));
    }
    
    /// <summary>Safe wrapper for Menace.Tactical.AI.Behavior</summary>
    public class BehaviorSafe : SafeGameObject
    {
        private readonly Behavior _obj;
    
        public BehaviorSafe(Behavior obj) : base(obj) { _obj = obj; }
        public BehaviorSafe(IntPtr ptr) : base(ptr) { _obj = new Behavior(ptr); }
    
    
        public int GetScore() => SafeCall(() => _obj.GetScore());
        public Agent GetAgent() => SafeCall(() => _obj.GetAgent());
        public RoleData GetRole() => SafeCall(() => _obj.GetRole());
        public StrategyData GetStrategy() => SafeCall(() => _obj.GetStrategy());
        public bool IsFirstExecuted() => SafeCall(() => _obj.IsFirstExecuted());
        public void SetAgent(Agent _a) => SafeCall(() => _obj.SetAgent(_a));
        public bool Collect(Actor _actor) => SafeCall(() => _obj.Collect(_actor));
        public bool Evaluate(Actor _actor, TileScore> _tiles) => SafeCall(() => _obj.Evaluate(_actor, _tiles));
        public void Evaluate(Actor _actor) => SafeCall(() => _obj.Evaluate(_actor));
        public bool Execute(Actor _actor) => SafeCall(() => _obj.Execute(_actor));
        public void ResetScore() => SafeCall(() => _obj.ResetScore());
        public string GetName() => SafeCall(() => _obj.GetName());
        public void OnBeforeProcessing() => SafeCall(() => _obj.OnBeforeProcessing());
        public void OnNewTurn() => SafeCall(() => _obj.OnNewTurn());
        public void OnClear() => SafeCall(() => _obj.OnClear());
    }
    
    /// <summary>Safe wrapper for Menace.Tactical.AI.Behaviors.Assist</summary>
    public class AssistSafe : SafeGameObject
    {
        private readonly Assist _obj;
    
        public AssistSafe(Assist obj) : base(obj) { _obj = obj; }
        public AssistSafe(IntPtr ptr) : base(ptr) { _obj = new Assist(ptr); }
    
    
        public override int GetOrder() => SafeCall(() => _obj.GetOrder());
    }
    
    /// <summary>Safe wrapper for Menace.Tactical.AI.Behaviors.Attack</summary>
    public class AttackSafe : SafeGameObject
    {
        private readonly Attack _obj;
    
        public AttackSafe(Attack obj) : base(obj) { _obj = obj; }
        public AttackSafe(IntPtr ptr) : base(ptr) { _obj = new Attack(ptr); }
    
    
        public override int GetOrder() => SafeCall(() => _obj.GetOrder());
        public Goal GetGoal() => SafeCall(() => _obj.GetGoal());
    }
    
    /// <summary>Safe wrapper for Menace.Tactical.AI.Behaviors.Buff</summary>
    public class BuffSafe : SafeGameObject
    {
        private readonly Buff _obj;
    
        public BuffSafe(Buff obj) : base(obj) { _obj = obj; }
        public BuffSafe(IntPtr ptr) : base(ptr) { _obj = new Buff(ptr); }
    
    
        public override ID GetID() => SafeCall(() => _obj.GetID());
    }
    
    /// <summary>Safe wrapper for Menace.Tactical.AI.Behaviors.CreateLOSBlocker</summary>
    public class CreateLOSBlockerSafe : SafeGameObject
    {
        private readonly CreateLOSBlocker _obj;
    
        public CreateLOSBlockerSafe(CreateLOSBlocker obj) : base(obj) { _obj = obj; }
        public CreateLOSBlockerSafe(IntPtr ptr) : base(ptr) { _obj = new CreateLOSBlocker(ptr); }
    
    
        public override ID GetID() => SafeCall(() => _obj.GetID());
        public override string GetName() => SafeCall(() => _obj.GetName());
    }
    
    /// <summary>Safe wrapper for Menace.Tactical.AI.Behaviors.Criterions.AvoidOpponents</summary>
    public class AvoidOpponentsSafe : SafeGameObject
    {
        private readonly AvoidOpponents _obj;
    
        public AvoidOpponentsSafe(AvoidOpponents obj) : base(obj) { _obj = obj; }
        public AvoidOpponentsSafe(IntPtr ptr) : base(ptr) { _obj = new AvoidOpponents(ptr); }
    
    
        public override bool IsValid(Actor _actor) => SafeCall(() => _obj.IsValid(_actor));
        public override void Evaluate(Actor _actor, TileScore _tile) => SafeCall(() => _obj.Evaluate(_actor, _tile));
    }
    
    /// <summary>Safe wrapper for Menace.Tactical.AI.Behaviors.Criterions.ConsiderSurroundings</summary>
    public class ConsiderSurroundingsSafe : SafeGameObject
    {
        private readonly ConsiderSurroundings _obj;
    
        public ConsiderSurroundingsSafe(ConsiderSurroundings obj) : base(obj) { _obj = obj; }
        public ConsiderSurroundingsSafe(IntPtr ptr) : base(ptr) { _obj = new ConsiderSurroundings(ptr); }
    
    
        public override bool IsValid(Actor _actor) => SafeCall(() => _obj.IsValid(_actor));
        public override void Collect(Actor _actor, TileScore> _tiles) => SafeCall(() => _obj.Collect(_actor, _tiles));
    }
    
    /// <summary>Safe wrapper for Menace.Tactical.AI.Behaviors.Criterions.ConsiderZones</summary>
    public class ConsiderZonesSafe : SafeGameObject
    {
        private readonly ConsiderZones _obj;
    
        public ConsiderZonesSafe(ConsiderZones obj) : base(obj) { _obj = obj; }
        public ConsiderZonesSafe(IntPtr ptr) : base(ptr) { _obj = new ConsiderZones(ptr); }
    
    
        public override bool IsValid(Actor _actor) => SafeCall(() => _obj.IsValid(_actor));
        public override void Collect(Actor _actor, TileScore> _tiles) => SafeCall(() => _obj.Collect(_actor, _tiles));
        public override void Evaluate(Actor _actor, TileScore _tile) => SafeCall(() => _obj.Evaluate(_actor, _tile));
        public override void PostProcess(Actor _actor, TileScore> _tiles) => SafeCall(() => _obj.PostProcess(_actor, _tiles));
    }
    
    /// <summary>Safe wrapper for Menace.Tactical.AI.Behaviors.Criterions.CoverAgainstOpponents</summary>
    public class CoverAgainstOpponentsSafe : SafeGameObject
    {
        private readonly CoverAgainstOpponents _obj;
    
        public CoverAgainstOpponentsSafe(CoverAgainstOpponents obj) : base(obj) { _obj = obj; }
        public CoverAgainstOpponentsSafe(IntPtr ptr) : base(ptr) { _obj = new CoverAgainstOpponents(ptr); }
    
    
        public override bool IsValid(Actor _actor) => SafeCall(() => _obj.IsValid(_actor));
        public override void Evaluate(Actor _actor, TileScore _tile) => SafeCall(() => _obj.Evaluate(_actor, _tile));
    }
    
    /// <summary>Safe wrapper for Menace.Tactical.AI.Behaviors.Criterions.Criterion</summary>
    public class CriterionSafe : SafeGameObject
    {
        private readonly Criterion _obj;
    
        public CriterionSafe(Criterion obj) : base(obj) { _obj = obj; }
        public CriterionSafe(IntPtr ptr) : base(ptr) { _obj = new Criterion(ptr); }
    
    
        public int GetThreads() => SafeCall(() => _obj.GetThreads());
        public void Collect(Actor _actor, TileScore> _tiles) => SafeCall(() => _obj.Collect(_actor, _tiles));
        public void Evaluate(Actor _actor, TileScore _tile) => SafeCall(() => _obj.Evaluate(_actor, _tile));
        public void PostProcess(Actor _actor, TileScore> _tiles) => SafeCall(() => _obj.PostProcess(_actor, _tiles));
    }
    
    /// <summary>Safe wrapper for Menace.Tactical.AI.Behaviors.Criterions.DistanceToCurrentTile</summary>
    public class DistanceToCurrentTileSafe : SafeGameObject
    {
        private readonly DistanceToCurrentTile _obj;
    
        public DistanceToCurrentTileSafe(DistanceToCurrentTile obj) : base(obj) { _obj = obj; }
        public DistanceToCurrentTileSafe(IntPtr ptr) : base(ptr) { _obj = new DistanceToCurrentTile(ptr); }
    
    
        public override bool IsValid(Actor _actor) => SafeCall(() => _obj.IsValid(_actor));
        public override void Evaluate(Actor _actor, TileScore _tile) => SafeCall(() => _obj.Evaluate(_actor, _tile));
    }
    
    /// <summary>Safe wrapper for Menace.Tactical.AI.Behaviors.Criterions.ExistingTileEffects</summary>
    public class ExistingTileEffectsSafe : SafeGameObject
    {
        private readonly ExistingTileEffects _obj;
    
        public ExistingTileEffectsSafe(ExistingTileEffects obj) : base(obj) { _obj = obj; }
        public ExistingTileEffectsSafe(IntPtr ptr) : base(ptr) { _obj = new ExistingTileEffects(ptr); }
    
    
        public override bool IsValid(Actor _actor) => SafeCall(() => _obj.IsValid(_actor));
        public override void Evaluate(Actor _actor, TileScore _tile) => SafeCall(() => _obj.Evaluate(_actor, _tile));
    }
    
    /// <summary>Safe wrapper for Menace.Tactical.AI.Behaviors.Criterions.FleeFromOpponents</summary>
    public class FleeFromOpponentsSafe : SafeGameObject
    {
        private readonly FleeFromOpponents _obj;
    
        public FleeFromOpponentsSafe(FleeFromOpponents obj) : base(obj) { _obj = obj; }
        public FleeFromOpponentsSafe(IntPtr ptr) : base(ptr) { _obj = new FleeFromOpponents(ptr); }
    
    
        public override bool IsValid(Actor _actor) => SafeCall(() => _obj.IsValid(_actor));
        public override void Evaluate(Actor _actor, TileScore _tile) => SafeCall(() => _obj.Evaluate(_actor, _tile));
    }
    
    /// <summary>Safe wrapper for Menace.Tactical.AI.Behaviors.Criterions.Roam</summary>
    public class RoamSafe : SafeGameObject
    {
        private readonly Roam _obj;
    
        public RoamSafe(Roam obj) : base(obj) { _obj = obj; }
        public RoamSafe(IntPtr ptr) : base(ptr) { _obj = new Roam(ptr); }
    
    
        public override bool IsValid(Actor _actor) => SafeCall(() => _obj.IsValid(_actor));
        public override void Collect(Actor _actor, TileScore> _tiles) => SafeCall(() => _obj.Collect(_actor, _tiles));
    }
    
    /// <summary>Safe wrapper for Menace.Tactical.AI.Behaviors.Criterions.ThreatFromOpponents</summary>
    public class ThreatFromOpponentsSafe : SafeGameObject
    {
        private readonly ThreatFromOpponents _obj;
    
        public ThreatFromOpponentsSafe(ThreatFromOpponents obj) : base(obj) { _obj = obj; }
        public ThreatFromOpponentsSafe(IntPtr ptr) : base(ptr) { _obj = new ThreatFromOpponents(ptr); }
    
    
        public override bool IsValid(Actor _actor) => SafeCall(() => _obj.IsValid(_actor));
        public override int GetThreads() => SafeCall(() => _obj.GetThreads());
        public override void Evaluate(Actor _actor, TileScore _tile) => SafeCall(() => _obj.Evaluate(_actor, _tile));
        public float Score(Actor _actor, Opponent _attacker, Tile _opponentTile, Entity _target, Tile _tile) => SafeCall(() => _obj.Score(_actor, _attacker, _opponentTile, _target, _tile));
    }
    
    /// <summary>Safe wrapper for Menace.Tactical.AI.Behaviors.Criterions.WakeUp</summary>
    public class WakeUpSafe : SafeGameObject
    {
        private readonly WakeUp _obj;
    
        public WakeUpSafe(WakeUp obj) : base(obj) { _obj = obj; }
        public WakeUpSafe(IntPtr ptr) : base(ptr) { _obj = new WakeUp(ptr); }
    
    
        public override bool IsValid(Actor _actor) => SafeCall(() => _obj.IsValid(_actor));
        public override void Collect(Actor _actor, TileScore> _tiles) => SafeCall(() => _obj.Collect(_actor, _tiles));
    }
    
    /// <summary>Safe wrapper for Menace.Tactical.AI.Behaviors.Data.Buff</summary>
    public class BuffSafe : SafeGameObject
    {
        private readonly Buff _obj;
    
        public BuffSafe(Buff obj) : base(obj) { _obj = obj; }
        public BuffSafe(IntPtr ptr) : base(ptr) { _obj = new Buff(ptr); }
    
    
        public override SkillBehavior CreateBehavior(Agent _agent, Skill _skill) => SafeCall(() => _obj.CreateBehavior(_agent, _skill));
    }
    
    /// <summary>Safe wrapper for Menace.Tactical.AI.Behaviors.Data.CreateLOSBlocker</summary>
    public class CreateLOSBlockerSafe : SafeGameObject
    {
        private readonly CreateLOSBlocker _obj;
    
        public CreateLOSBlockerSafe(CreateLOSBlocker obj) : base(obj) { _obj = obj; }
        public CreateLOSBlockerSafe(IntPtr ptr) : base(ptr) { _obj = new CreateLOSBlocker(ptr); }
    
    
        public override SkillBehavior CreateBehavior(Agent _agent, Skill _skill) => SafeCall(() => _obj.CreateBehavior(_agent, _skill));
    }
    
    /// <summary>Safe wrapper for Menace.Tactical.AI.Behaviors.Data.GainBonusTurn</summary>
    public class GainBonusTurnSafe : SafeGameObject
    {
        private readonly GainBonusTurn _obj;
    
        public GainBonusTurnSafe(GainBonusTurn obj) : base(obj) { _obj = obj; }
        public GainBonusTurnSafe(IntPtr ptr) : base(ptr) { _obj = new GainBonusTurn(ptr); }
    
    
        public override SkillBehavior CreateBehavior(Agent _agent, Skill _skill) => SafeCall(() => _obj.CreateBehavior(_agent, _skill));
    }
    
    /// <summary>Safe wrapper for Menace.Tactical.AI.Behaviors.Data.InflictDamage</summary>
    public class InflictDamageSafe : SafeGameObject
    {
        private readonly InflictDamage _obj;
    
        public InflictDamageSafe(InflictDamage obj) : base(obj) { _obj = obj; }
        public InflictDamageSafe(IntPtr ptr) : base(ptr) { _obj = new InflictDamage(ptr); }
    
    
        public override SkillBehavior CreateBehavior(Agent _agent, Skill _skill) => SafeCall(() => _obj.CreateBehavior(_agent, _skill));
    }
    
    /// <summary>Safe wrapper for Menace.Tactical.AI.Behaviors.Data.InflictSuppression</summary>
    public class InflictSuppressionSafe : SafeGameObject
    {
        private readonly InflictSuppression _obj;
    
        public InflictSuppressionSafe(InflictSuppression obj) : base(obj) { _obj = obj; }
        public InflictSuppressionSafe(IntPtr ptr) : base(ptr) { _obj = new InflictSuppression(ptr); }
    
    
        public override SkillBehavior CreateBehavior(Agent _agent, Skill _skill) => SafeCall(() => _obj.CreateBehavior(_agent, _skill));
    }
    
    /// <summary>Safe wrapper for Menace.Tactical.AI.Behaviors.Data.Mindray</summary>
    public class MindraySafe : SafeGameObject
    {
        private readonly Mindray _obj;
    
        public MindraySafe(Mindray obj) : base(obj) { _obj = obj; }
        public MindraySafe(IntPtr ptr) : base(ptr) { _obj = new Mindray(ptr); }
    
    
        public override SkillBehavior CreateBehavior(Agent _agent, Skill _skill) => SafeCall(() => _obj.CreateBehavior(_agent, _skill));
    }
    
    /// <summary>Safe wrapper for Menace.Tactical.AI.Behaviors.Data.MovementSkill</summary>
    public class MovementSkillSafe : SafeGameObject
    {
        private readonly MovementSkill _obj;
    
        public MovementSkillSafe(MovementSkill obj) : base(obj) { _obj = obj; }
        public MovementSkillSafe(IntPtr ptr) : base(ptr) { _obj = new MovementSkill(ptr); }
    
    
        public override SkillBehavior CreateBehavior(Agent _agent, Skill _skill) => SafeCall(() => _obj.CreateBehavior(_agent, _skill));
    }
    
    /// <summary>Safe wrapper for Menace.Tactical.AI.Behaviors.Data.Reload</summary>
    public class ReloadSafe : SafeGameObject
    {
        private readonly Reload _obj;
    
        public ReloadSafe(Reload obj) : base(obj) { _obj = obj; }
        public ReloadSafe(IntPtr ptr) : base(ptr) { _obj = new Reload(ptr); }
    
    
        public override SkillBehavior CreateBehavior(Agent _agent, Skill _skill) => SafeCall(() => _obj.CreateBehavior(_agent, _skill));
    }
    
    /// <summary>Safe wrapper for Menace.Tactical.AI.Behaviors.Data.RemoveStatusEffect</summary>
    public class RemoveStatusEffectSafe : SafeGameObject
    {
        private readonly RemoveStatusEffect _obj;
    
        public RemoveStatusEffectSafe(RemoveStatusEffect obj) : base(obj) { _obj = obj; }
        public RemoveStatusEffectSafe(IntPtr ptr) : base(ptr) { _obj = new RemoveStatusEffect(ptr); }
    
    
        public override SkillBehavior CreateBehavior(Agent _agent, Skill _skill) => SafeCall(() => _obj.CreateBehavior(_agent, _skill));
    }
    
    /// <summary>Safe wrapper for Menace.Tactical.AI.Behaviors.Data.Scan</summary>
    public class ScanSafe : SafeGameObject
    {
        private readonly Scan _obj;
    
        public ScanSafe(Scan obj) : base(obj) { _obj = obj; }
        public ScanSafe(IntPtr ptr) : base(ptr) { _obj = new Scan(ptr); }
    
    
        public override SkillBehavior CreateBehavior(Agent _agent, Skill _skill) => SafeCall(() => _obj.CreateBehavior(_agent, _skill));
    }
    
    /// <summary>Safe wrapper for Menace.Tactical.AI.Behaviors.Data.SpawnHovermine</summary>
    public class SpawnHovermineSafe : SafeGameObject
    {
        private readonly SpawnHovermine _obj;
    
        public SpawnHovermineSafe(SpawnHovermine obj) : base(obj) { _obj = obj; }
        public SpawnHovermineSafe(IntPtr ptr) : base(ptr) { _obj = new SpawnHovermine(ptr); }
    
    
        public override SkillBehavior CreateBehavior(Agent _agent, Skill _skill) => SafeCall(() => _obj.CreateBehavior(_agent, _skill));
    }
    
    /// <summary>Safe wrapper for Menace.Tactical.AI.Behaviors.Data.SpawnPhantom</summary>
    public class SpawnPhantomSafe : SafeGameObject
    {
        private readonly SpawnPhantom _obj;
    
        public SpawnPhantomSafe(SpawnPhantom obj) : base(obj) { _obj = obj; }
        public SpawnPhantomSafe(IntPtr ptr) : base(ptr) { _obj = new SpawnPhantom(ptr); }
    
    
        public override SkillBehavior CreateBehavior(Agent _agent, Skill _skill) => SafeCall(() => _obj.CreateBehavior(_agent, _skill));
    }
    
    /// <summary>Safe wrapper for Menace.Tactical.AI.Behaviors.Data.Stun</summary>
    public class StunSafe : SafeGameObject
    {
        private readonly Stun _obj;
    
        public StunSafe(Stun obj) : base(obj) { _obj = obj; }
        public StunSafe(IntPtr ptr) : base(ptr) { _obj = new Stun(ptr); }
    
    
        public override SkillBehavior CreateBehavior(Agent _agent, Skill _skill) => SafeCall(() => _obj.CreateBehavior(_agent, _skill));
    }
    
    /// <summary>Safe wrapper for Menace.Tactical.AI.Behaviors.Data.SupplyAmmo</summary>
    public class SupplyAmmoSafe : SafeGameObject
    {
        private readonly SupplyAmmo _obj;
    
        public SupplyAmmoSafe(SupplyAmmo obj) : base(obj) { _obj = obj; }
        public SupplyAmmoSafe(IntPtr ptr) : base(ptr) { _obj = new SupplyAmmo(ptr); }
    
    
        public override SkillBehavior CreateBehavior(Agent _agent, Skill _skill) => SafeCall(() => _obj.CreateBehavior(_agent, _skill));
    }
    
    /// <summary>Safe wrapper for Menace.Tactical.AI.Behaviors.Data.TargetDesignator</summary>
    public class TargetDesignatorSafe : SafeGameObject
    {
        private readonly TargetDesignator _obj;
    
        public TargetDesignatorSafe(TargetDesignator obj) : base(obj) { _obj = obj; }
        public TargetDesignatorSafe(IntPtr ptr) : base(ptr) { _obj = new TargetDesignator(ptr); }
    
    
        public override SkillBehavior CreateBehavior(Agent _agent, Skill _skill) => SafeCall(() => _obj.CreateBehavior(_agent, _skill));
    }
    
    /// <summary>Safe wrapper for Menace.Tactical.AI.Behaviors.Data.TransportEntity</summary>
    public class TransportEntitySafe : SafeGameObject
    {
        private readonly TransportEntity _obj;
    
        public TransportEntitySafe(TransportEntity obj) : base(obj) { _obj = obj; }
        public TransportEntitySafe(IntPtr ptr) : base(ptr) { _obj = new TransportEntity(ptr); }
    
    
        public override SkillBehavior CreateBehavior(Agent _agent, Skill _skill) => SafeCall(() => _obj.CreateBehavior(_agent, _skill));
    }
    
    /// <summary>Safe wrapper for Menace.Tactical.AI.Behaviors.Data.TurnArmorTowardsThreat</summary>
    public class TurnArmorTowardsThreatSafe : SafeGameObject
    {
        private readonly TurnArmorTowardsThreat _obj;
    
        public TurnArmorTowardsThreatSafe(TurnArmorTowardsThreat obj) : base(obj) { _obj = obj; }
        public TurnArmorTowardsThreatSafe(IntPtr ptr) : base(ptr) { _obj = new TurnArmorTowardsThreat(ptr); }
    
    
        public override SkillBehavior CreateBehavior(Agent _agent, Skill _skill) => SafeCall(() => _obj.CreateBehavior(_agent, _skill));
    }
    
    /// <summary>Safe wrapper for Menace.Tactical.AI.Behaviors.Deploy</summary>
    public class DeploySafe : SafeGameObject
    {
        private readonly Deploy _obj;
    
        public DeploySafe(Deploy obj) : base(obj) { _obj = obj; }
        public DeploySafe(IntPtr ptr) : base(ptr) { _obj = new Deploy(ptr); }
    
    
        public override int GetOrder() => SafeCall(() => _obj.GetOrder());
        public override ID GetID() => SafeCall(() => _obj.GetID());
    }
    
    /// <summary>Safe wrapper for Menace.Tactical.AI.Behaviors.GainBonusTurn</summary>
    public class GainBonusTurnSafe : SafeGameObject
    {
        private readonly GainBonusTurn _obj;
    
        public GainBonusTurnSafe(GainBonusTurn obj) : base(obj) { _obj = obj; }
        public GainBonusTurnSafe(IntPtr ptr) : base(ptr) { _obj = new GainBonusTurn(ptr); }
    
    
        public override int GetOrder() => SafeCall(() => _obj.GetOrder());
        public override ID GetID() => SafeCall(() => _obj.GetID());
        public override string GetName() => SafeCall(() => _obj.GetName());
        public override void OnNewTurn() => SafeCall(() => _obj.OnNewTurn());
    }
    
    /// <summary>Safe wrapper for Menace.Tactical.AI.Behaviors.Idle</summary>
    public class IdleSafe : SafeGameObject
    {
        private readonly Idle _obj;
    
        public IdleSafe(Idle obj) : base(obj) { _obj = obj; }
        public IdleSafe(IntPtr ptr) : base(ptr) { _obj = new Idle(ptr); }
    
    
        public override int GetOrder() => SafeCall(() => _obj.GetOrder());
        public override ID GetID() => SafeCall(() => _obj.GetID());
    }
    
    /// <summary>Safe wrapper for Menace.Tactical.AI.Behaviors.InflictDamage</summary>
    public class InflictDamageSafe : SafeGameObject
    {
        private readonly InflictDamage _obj;
    
        public InflictDamageSafe(InflictDamage obj) : base(obj) { _obj = obj; }
        public InflictDamageSafe(IntPtr ptr) : base(ptr) { _obj = new InflictDamage(ptr); }
    
    
        public override ID GetID() => SafeCall(() => _obj.GetID());
        public override string GetName() => SafeCall(() => _obj.GetName());
    }
    
    /// <summary>Safe wrapper for Menace.Tactical.AI.Behaviors.InflictSuppression</summary>
    public class InflictSuppressionSafe : SafeGameObject
    {
        private readonly InflictSuppression _obj;
    
        public InflictSuppressionSafe(InflictSuppression obj) : base(obj) { _obj = obj; }
        public InflictSuppressionSafe(IntPtr ptr) : base(ptr) { _obj = new InflictSuppression(ptr); }
    
    
        public override ID GetID() => SafeCall(() => _obj.GetID());
        public override string GetName() => SafeCall(() => _obj.GetName());
    }
    
    /// <summary>Safe wrapper for Menace.Tactical.AI.Behaviors.Mindray</summary>
    public class MindraySafe : SafeGameObject
    {
        private readonly Mindray _obj;
    
        public MindraySafe(Mindray obj) : base(obj) { _obj = obj; }
        public MindraySafe(IntPtr ptr) : base(ptr) { _obj = new Mindray(ptr); }
    
    
        public override ID GetID() => SafeCall(() => _obj.GetID());
        public override string GetName() => SafeCall(() => _obj.GetName());
    }
    
    /// <summary>Safe wrapper for Menace.Tactical.AI.Behaviors.Move</summary>
    public class MoveSafe : SafeGameObject
    {
        private readonly Move _obj;
    
        public MoveSafe(Move obj) : base(obj) { _obj = obj; }
        public MoveSafe(IntPtr ptr) : base(ptr) { _obj = new Move(ptr); }
    
    
        public override int GetOrder() => SafeCall(() => _obj.GetOrder());
        public override ID GetID() => SafeCall(() => _obj.GetID());
        public bool IsMovementDone() => SafeCall(() => _obj.IsMovementDone());
        public bool HasMovedThisTurn() => SafeCall(() => _obj.HasMovedThisTurn());
        public bool HasDelayedMovementThisTurn() => SafeCall(() => _obj.HasDelayedMovementThisTurn());
        public bool IsDelayingMovement() => SafeCall(() => _obj.IsDelayingMovement());
        public bool IsInsideContainerAndInert() => SafeCall(() => _obj.IsInsideContainerAndInert());
        public TileScore GetTargetTile() => SafeCall(() => _obj.GetTargetTile());
        public override void OnBeforeProcessing() => SafeCall(() => _obj.OnBeforeProcessing());
        public override void OnNewTurn() => SafeCall(() => _obj.OnNewTurn());
        public override void OnClear() => SafeCall(() => _obj.OnClear());
    }
    
    /// <summary>Safe wrapper for Menace.Tactical.AI.Behaviors.MovementSkill</summary>
    public class MovementSkillSafe : SafeGameObject
    {
        private readonly MovementSkill _obj;
    
        public MovementSkillSafe(MovementSkill obj) : base(obj) { _obj = obj; }
        public MovementSkillSafe(IntPtr ptr) : base(ptr) { _obj = new MovementSkill(ptr); }
    
    
        public override int GetOrder() => SafeCall(() => _obj.GetOrder());
        public override ID GetID() => SafeCall(() => _obj.GetID());
        public override string GetName() => SafeCall(() => _obj.GetName());
        public override void OnNewTurn() => SafeCall(() => _obj.OnNewTurn());
    }
    
    /// <summary>Safe wrapper for Menace.Tactical.AI.Behaviors.Reload</summary>
    public class ReloadSafe : SafeGameObject
    {
        private readonly Reload _obj;
    
        public ReloadSafe(Reload obj) : base(obj) { _obj = obj; }
        public ReloadSafe(IntPtr ptr) : base(ptr) { _obj = new Reload(ptr); }
    
    
        public override int GetOrder() => SafeCall(() => _obj.GetOrder());
        public override ID GetID() => SafeCall(() => _obj.GetID());
    }
    
    /// <summary>Safe wrapper for Menace.Tactical.AI.Behaviors.RemoveStatusEffect</summary>
    public class RemoveStatusEffectSafe : SafeGameObject
    {
        private readonly RemoveStatusEffect _obj;
    
        public RemoveStatusEffectSafe(RemoveStatusEffect obj) : base(obj) { _obj = obj; }
        public RemoveStatusEffectSafe(IntPtr ptr) : base(ptr) { _obj = new RemoveStatusEffect(ptr); }
    
    
        public override int GetOrder() => SafeCall(() => _obj.GetOrder());
        public override ID GetID() => SafeCall(() => _obj.GetID());
    }
    
    /// <summary>Safe wrapper for Menace.Tactical.AI.Behaviors.Scan</summary>
    public class ScanSafe : SafeGameObject
    {
        private readonly Scan _obj;
    
        public ScanSafe(Scan obj) : base(obj) { _obj = obj; }
        public ScanSafe(IntPtr ptr) : base(ptr) { _obj = new Scan(ptr); }
    
    
        public override int GetOrder() => SafeCall(() => _obj.GetOrder());
        public override ID GetID() => SafeCall(() => _obj.GetID());
        public override string GetName() => SafeCall(() => _obj.GetName());
        public override void OnNewTurn() => SafeCall(() => _obj.OnNewTurn());
    }
    
    /// <summary>Safe wrapper for Menace.Tactical.AI.Behaviors.SpawnHovermine</summary>
    public class SpawnHovermineSafe : SafeGameObject
    {
        private readonly SpawnHovermine _obj;
    
        public SpawnHovermineSafe(SpawnHovermine obj) : base(obj) { _obj = obj; }
        public SpawnHovermineSafe(IntPtr ptr) : base(ptr) { _obj = new SpawnHovermine(ptr); }
    
    
        public override ID GetID() => SafeCall(() => _obj.GetID());
        public override string GetName() => SafeCall(() => _obj.GetName());
    }
    
    /// <summary>Safe wrapper for Menace.Tactical.AI.Behaviors.SpawnPhantom</summary>
    public class SpawnPhantomSafe : SafeGameObject
    {
        private readonly SpawnPhantom _obj;
    
        public SpawnPhantomSafe(SpawnPhantom obj) : base(obj) { _obj = obj; }
        public SpawnPhantomSafe(IntPtr ptr) : base(ptr) { _obj = new SpawnPhantom(ptr); }
    
    
        public override ID GetID() => SafeCall(() => _obj.GetID());
        public override string GetName() => SafeCall(() => _obj.GetName());
    }
    
    /// <summary>Safe wrapper for Menace.Tactical.AI.Behaviors.Stun</summary>
    public class StunSafe : SafeGameObject
    {
        private readonly Stun _obj;
    
        public StunSafe(Stun obj) : base(obj) { _obj = obj; }
        public StunSafe(IntPtr ptr) : base(ptr) { _obj = new Stun(ptr); }
    
    
        public override ID GetID() => SafeCall(() => _obj.GetID());
        public override string GetName() => SafeCall(() => _obj.GetName());
    }
    
    /// <summary>Safe wrapper for Menace.Tactical.AI.Behaviors.SupplyAmmo</summary>
    public class SupplyAmmoSafe : SafeGameObject
    {
        private readonly SupplyAmmo _obj;
    
        public SupplyAmmoSafe(SupplyAmmo obj) : base(obj) { _obj = obj; }
        public SupplyAmmoSafe(IntPtr ptr) : base(ptr) { _obj = new SupplyAmmo(ptr); }
    
    
        public override ID GetID() => SafeCall(() => _obj.GetID());
    }
    
    /// <summary>Safe wrapper for Menace.Tactical.AI.Behaviors.TargetDesignator</summary>
    public class TargetDesignatorSafe : SafeGameObject
    {
        private readonly TargetDesignator _obj;
    
        public TargetDesignatorSafe(TargetDesignator obj) : base(obj) { _obj = obj; }
        public TargetDesignatorSafe(IntPtr ptr) : base(ptr) { _obj = new TargetDesignator(ptr); }
    
    
        public override ID GetID() => SafeCall(() => _obj.GetID());
        public override string GetName() => SafeCall(() => _obj.GetName());
    }
    
    /// <summary>Safe wrapper for Menace.Tactical.AI.Behaviors.TransportEntity</summary>
    public class TransportEntitySafe : SafeGameObject
    {
        private readonly TransportEntity _obj;
    
        public TransportEntitySafe(TransportEntity obj) : base(obj) { _obj = obj; }
        public TransportEntitySafe(IntPtr ptr) : base(ptr) { _obj = new TransportEntity(ptr); }
    
    
        public bool IsTargetReached() => SafeCall(() => _obj.IsTargetReached());
        public override int GetOrder() => SafeCall(() => _obj.GetOrder());
        public override ID GetID() => SafeCall(() => _obj.GetID());
    }
    
    /// <summary>Safe wrapper for Menace.Tactical.AI.Behaviors.TurnArmorTowardsThreat</summary>
    public class TurnArmorTowardsThreatSafe : SafeGameObject
    {
        private readonly TurnArmorTowardsThreat _obj;
    
        public TurnArmorTowardsThreatSafe(TurnArmorTowardsThreat obj) : base(obj) { _obj = obj; }
        public TurnArmorTowardsThreatSafe(IntPtr ptr) : base(ptr) { _obj = new TurnArmorTowardsThreat(ptr); }
    
    
        public override int GetOrder() => SafeCall(() => _obj.GetOrder());
        public override ID GetID() => SafeCall(() => _obj.GetID());
        public override void OnNewTurn() => SafeCall(() => _obj.OnNewTurn());
    }
    
    /// <summary>Safe wrapper for Menace.Tactical.AI.Data.Assessment</summary>
    public class AssessmentSafe : SafeGameObject
    {
        private readonly Assessment _obj;
    
        public AssessmentSafe(Assessment obj) : base(obj) { _obj = obj; }
        public AssessmentSafe(IntPtr ptr) : base(ptr) { _obj = new Assessment(ptr); }
    
    
        public void Update(OperationalZones _zones, IEnumerable<Actor> _ourActors) => SafeCall(() => _obj.Update(_zones, _ourActors));
        public void Reset() => SafeCall(() => _obj.Reset());
        public void RemoveActor(Actor _actor) => SafeCall(() => _obj.RemoveActor(_actor));
    }
    
    /// <summary>Safe wrapper for Menace.Tactical.AI.Data.RoleData</summary>
    public class RoleDataSafe : SafeGameObject
    {
        private readonly RoleData _obj;
    
        public RoleDataSafe(RoleData obj) : base(obj) { _obj = obj; }
        public RoleDataSafe(IntPtr ptr) : base(ptr) { _obj = new RoleData(ptr); }
    
    
    }
    
    /// <summary>Safe wrapper for Menace.Tactical.AI.Data.SkillBehavior</summary>
    public class SkillBehaviorSafe : SafeGameObject
    {
        private readonly SkillBehavior _obj;
    
        public SkillBehaviorSafe(SkillBehavior obj) : base(obj) { _obj = obj; }
        public SkillBehaviorSafe(IntPtr ptr) : base(ptr) { _obj = new SkillBehavior(ptr); }
    
    
    }
    
    /// <summary>Safe wrapper for Menace.Tactical.AI.Data.StrategyData</summary>
    public class StrategyDataSafe : SafeGameObject
    {
        private readonly StrategyData _obj;
    
        public StrategyDataSafe(StrategyData obj) : base(obj) { _obj = obj; }
        public StrategyDataSafe(IntPtr ptr) : base(ptr) { _obj = new StrategyData(ptr); }
    
    
        public AIFaction GetFaction() => SafeCall(() => _obj.GetFaction());
        public void Update() => SafeCall(() => _obj.Update());
    }
    
    /// <summary>Safe wrapper for Menace.Tactical.AI.Data.TileData</summary>
    public class TileDataSafe : SafeGameObject
    {
        private readonly TileData _obj;
    
        public TileDataSafe(TileData obj) : base(obj) { _obj = obj; }
        public TileDataSafe(IntPtr ptr) : base(ptr) { _obj = new TileData(ptr); }
    
    
        public bool IsAvailable(Actor _actor, float _score) => SafeCall(() => _obj.IsAvailable(_actor, _score));
        public void Grab(Actor _actor, float _score) => SafeCall(() => _obj.Grab(_actor, _score));
        public void Release(Actor _actor) => SafeCall(() => _obj.Release(_actor));
    }
    
    /// <summary>Safe wrapper for Menace.Tactical.AI.Data.TileScore</summary>
    public class TileScoreSafe : SafeGameObject
    {
        private readonly TileScore _obj;
    
        public TileScoreSafe(TileScore obj) : base(obj) { _obj = obj; }
        public TileScoreSafe(IntPtr ptr) : base(ptr) { _obj = new TileScore(ptr); }
    
    
        public TileScore GetClone() => SafeCall(() => _obj.GetClone());
        public float GetScore() => SafeCall(() => _obj.GetScore());
        public float GetScoreWithoutDistance() => SafeCall(() => _obj.GetScoreWithoutDistance());
        public float GetScaledScore() => SafeCall(() => _obj.GetScaledScore());
        public void Reset(Tile _tile) => SafeCall(() => _obj.Reset(_tile));
        public override string ToString() => SafeCall(() => _obj.ToString());
    }
    
    /// <summary>Safe wrapper for Menace.Tactical.AI.Data.TileScorePool</summary>
    public class TileScorePoolSafe : SafeGameObject
    {
        private readonly TileScorePool _obj;
    
        public TileScorePoolSafe(TileScorePool obj) : base(obj) { _obj = obj; }
        public TileScorePoolSafe(IntPtr ptr) : base(ptr) { _obj = new TileScorePool(ptr); }
    
    
        public TileScore Grab(Tile _tile) => SafeCall(() => _obj.Grab(_tile));
        public void Return(TileScore _tileScore) => SafeCall(() => _obj.Return(_tileScore));
    }
    
    /// <summary>Safe wrapper for Menace.Tactical.AI.Data.UsageData</summary>
    public class UsageDataSafe : SafeGameObject
    {
        private readonly UsageData _obj;
    
        public UsageDataSafe(UsageData obj) : base(obj) { _obj = obj; }
        public UsageDataSafe(IntPtr ptr) : base(ptr) { _obj = new UsageData(ptr); }
    
    
    }
    
    /// <summary>Safe wrapper for Menace.Tactical.AI.OperationalZones</summary>
    public class OperationalZonesSafe : SafeGameObject
    {
        private readonly OperationalZones _obj;
    
        public OperationalZonesSafe(OperationalZones obj) : base(obj) { _obj = obj; }
        public OperationalZonesSafe(IntPtr ptr) : base(ptr) { _obj = new OperationalZones(ptr); }
    
    
        public int GetNumZones() => SafeCall(() => _obj.GetNumZones());
        public void Reset() => SafeCall(() => _obj.Reset());
        public Zone GetZone(Tile _tile) => SafeCall(() => _obj.GetZone(_tile));
        public List<Zone> GetZones() => SafeCall(() => _obj.GetZones());
        public void QueryZones(List<Zone> _into, int _orders = 2147483647) => SafeCall(() => _obj.QueryZones(_into, 2147483647));
        public void QueryZonesInRange(Tile _center, int _radius, List<Zone> _into, int _orders = 2147483647) => SafeCall(() => _obj.QueryZonesInRange(_center, _radius, _into, 2147483647));
        public void AddZone(Zone _z) => SafeCall(() => _obj.AddZone(_z));
    }
    
    /// <summary>Safe wrapper for Menace.Tactical.AI.Opponent</summary>
    public class OpponentSafe : SafeGameObject
    {
        private readonly Opponent _obj;
    
        public OpponentSafe(Opponent obj) : base(obj) { _obj = obj; }
        public OpponentSafe(IntPtr ptr) : base(ptr) { _obj = new Opponent(ptr); }
    
    
        public bool IsKnown() => SafeCall(() => _obj.IsKnown());
    }
    
    /// <summary>Safe wrapper for Menace.Tactical.AI.PlayerFaction</summary>
    public class PlayerFactionSafe : SafeGameObject
    {
        private readonly PlayerFaction _obj;
    
        public PlayerFactionSafe(PlayerFaction obj) : base(obj) { _obj = obj; }
        public PlayerFactionSafe(IntPtr ptr) : base(ptr) { _obj = new PlayerFaction(ptr); }
    
    
        public override bool IsAlliedWithPlayer() => SafeCall(() => _obj.IsAlliedWithPlayer());
        public override bool IsAlliedWith(int _faction) => SafeCall(() => _obj.IsAlliedWith(_faction));
        public override void Process() => SafeCall(() => _obj.Process());
    }
    
    /// <summary>Safe wrapper for Menace.Tactical.AI.Score</summary>
    public class ScoreSafe : SafeGameObject
    {
        private readonly Score _obj;
    
        public ScoreSafe(Score obj) : base(obj) { _obj = obj; }
        public ScoreSafe(IntPtr ptr) : base(ptr) { _obj = new Score(ptr); }
    
    
    }
    
    /// <summary>Safe wrapper for Menace.Tactical.AI.SkillBehavior</summary>
    public class SkillBehaviorSafe : SafeGameObject
    {
        private readonly SkillBehavior _obj;
    
        public SkillBehaviorSafe(SkillBehavior obj) : base(obj) { _obj = obj; }
        public SkillBehaviorSafe(IntPtr ptr) : base(ptr) { _obj = new SkillBehavior(ptr); }
    
    
        public Skill GetSkill() => SafeCall(() => _obj.GetSkill());
        public int GetSkillIDHash() => SafeCall(() => _obj.GetSkillIDHash());
        public float GetTargetValue(bool _forImmediateUse, int _uses, Tile _target, Goal _goal, Skill _skill, Tile _from, Tile _targetedTile) => SafeCall(() => _obj.GetTargetValue(_forImmediateUse, _uses, _target, _goal, _skill, _from, _targetedTile));
    }
    
    /// <summary>Safe wrapper for Menace.Tactical.AI.Zone</summary>
    public class ZoneSafe : SafeGameObject
    {
        private readonly Zone _obj;
    
        public ZoneSafe(Zone obj) : base(obj) { _obj = obj; }
        public ZoneSafe(IntPtr ptr) : base(ptr) { _obj = new Zone(ptr); }
    
    
        public bool HasOrder(ZoneOrder _order) => SafeCall(() => _obj.HasOrder(_order));
    }
    
    // ═══════════════════════════════════════════════════════
    // Hook Registration
    // ═══════════════════════════════════════════════════════

    /// <summary>Hooks for Menace.Tactical.AI.AIFaction</summary>
    public static class AIFactionHooks
    {
        public static event Action<AIFactionSafe> BeforeGetOpponents;
        public static event Func<AIFactionSafe, List<Opponent>, List<Opponent>> AfterGetOpponents;
        public static event Action<AIFactionSafe> BeforeGetStrategy;
        public static event Func<AIFactionSafe, StrategyData, StrategyData> AfterGetStrategy;
        public static event Action<AIFactionSafe> BeforeGetZones;
        public static event Func<AIFactionSafe, OperationalZones, OperationalZones> AfterGetZones;
        public static event Action<AIFactionSafe> BeforeGetTime;
        public static event Func<AIFactionSafe, float, float> AfterGetTime;
        public static event Action<AIFactionSafe> BeforeGetPickableActorsCount;
        public static event Func<AIFactionSafe, int, int> AfterGetPickableActorsCount;
        public static event Action<AIFactionSafe> BeforeIsThinking;
        public static event Func<AIFactionSafe, bool, bool> AfterIsThinking;
        public static event Action<AIFactionSafe> BeforeIsAlliedWithPlayer;
        public static event Func<AIFactionSafe, override bool, override bool> AfterIsAlliedWithPlayer;
        public static event Action<AIFactionSafe, int> BeforeIsAlliedWith;
        public static event Func<AIFactionSafe, int, override bool, override bool> AfterIsAlliedWith;
        public static event Action<AIFactionSafe> BeforeProcess;
        public static event Func<AIFactionSafe, override void, override void> AfterProcess;
        public static event Action<AIFactionSafe> BeforeOnTurnStart;
        public static event Func<AIFactionSafe, override void, override void> AfterOnTurnStart;
        public static event Action<AIFactionSafe> BeforeOnRoundStart;
        public static event Func<AIFactionSafe, override void, override void> AfterOnRoundStart;
        public static event Action<AIFactionSafe, Actor, int> BeforeOnOpponentSighted;
        public static event Func<AIFactionSafe, Actor, int, override void, override void> AfterOnOpponentSighted;
        public static event Action<AIFactionSafe, Actor> BeforeOnActorDeath;
        public static event Func<AIFactionSafe, Actor, override void, override void> AfterOnActorDeath;
        public static event Action<AIFactionSafe> BeforeHasKnownOpponent;
        public static event Func<AIFactionSafe, bool, bool> AfterHasKnownOpponent;
        public static event Action<AIFactionSafe, Actor> BeforeGetOpponent;
        public static event Func<AIFactionSafe, Actor, Opponent, Opponent> AfterGetOpponent;
    }
    
    /// <summary>Hooks for Menace.Tactical.AI.Agent</summary>
    public static class AgentHooks
    {
        public static event Action<AgentSafe> Beforeget_FlaggedForDeactivation;
        public static event Func<AgentSafe, bool, bool> Afterget_FlaggedForDeactivation;
        public static event Action<AgentSafe, bool> Beforeset_FlaggedForDeactivation;
        public static event Action<AgentSafe, bool> Afterset_FlaggedForDeactivation;
        public static event Action<AgentSafe> BeforeGetScore;
        public static event Func<AgentSafe, int, int> AfterGetScore;
        public static event Action<AgentSafe> BeforeGetActor;
        public static event Func<AgentSafe, Actor, Actor> AfterGetActor;
        public static event Action<AgentSafe> BeforeGetRole;
        public static event Func<AgentSafe, RoleData, RoleData> AfterGetRole;
        public static event Action<AgentSafe> BeforeGetFaction;
        public static event Func<AgentSafe, AIFaction, AIFaction> AfterGetFaction;
        public static event Action<AgentSafe> BeforeGetRandom;
        public static event Func<AgentSafe, PseudoRandom, PseudoRandom> AfterGetRandom;
        public static event Action<AgentSafe> BeforeGetBehaviors;
        public static event Func<AgentSafe, List<Behavior>, List<Behavior>> AfterGetBehaviors;
        public static event Action<AgentSafe> BeforeGetState;
        public static event Func<AgentSafe, Agent.State, Agent.State> AfterGetState;
        public static event Action<AgentSafe> BeforeGetZones;
        public static event Func<AgentSafe, OperationalZones, OperationalZones> AfterGetZones;
        public static event Action<AgentSafe> BeforeGetNumThreatsFaced;
        public static event Func<AgentSafe, int, int> AfterGetNumThreatsFaced;
        public static event Action<AgentSafe> BeforeIsDeployed;
        public static event Func<AgentSafe, bool, bool> AfterIsDeployed;
        public static event Action<AgentSafe> BeforeIsSleeping;
        public static event Func<AgentSafe, bool, bool> AfterIsSleeping;
        public static event Action<AgentSafe, bool> BeforeSetSleeping;
        public static event Action<AgentSafe, bool> AfterSetSleeping;
        public static event Action<AgentSafe, bool> BeforeSetDeployed;
        public static event Action<AgentSafe, bool> AfterSetDeployed;
        public static event Action<AgentSafe, AIFaction> BeforeSetFaction;
        public static event Action<AgentSafe, AIFaction> AfterSetFaction;
        public static event Action<AgentSafe, float> BeforeSetPriority;
        public static event Action<AgentSafe, float> AfterSetPriority;
        public static event Action<AgentSafe> BeforeGetPriority;
        public static event Func<AgentSafe, float, float> AfterGetPriority;
        public static event Action<AgentSafe, int> BeforeSetNumThreatsFaced;
        public static event Action<AgentSafe, int> AfterSetNumThreatsFaced;
        public static event Action<AgentSafe, Agent.Flag> BeforeHasFlag;
        public static event Func<AgentSafe, Agent.Flag, bool, bool> AfterHasFlag;
        public static event Action<AgentSafe, Agent.Flag, bool> BeforeSetFlag;
        public static event Action<AgentSafe, Agent.Flag, bool> AfterSetFlag;
        public static event Action<AgentSafe> BeforeGetTiles;
        public static event Func<AgentSafe, Dictionary<Tile, TileScore>, Dictionary<Tile, TileScore>> AfterGetTiles;
        public static event Action<AgentSafe, Skill> BeforeOnSkillAdded;
        public static event Action<AgentSafe, Skill> AfterOnSkillAdded;
        public static event Action<AgentSafe, Skill> BeforeOnSkillRemoved;
        public static event Action<AgentSafe, Skill> AfterOnSkillRemoved;
        public static event Action<AgentSafe> BeforeEvaluate;
        public static event Action<AgentSafe> AfterEvaluate;
        public static event Action<AgentSafe> BeforeExecute;
        public static event Func<AgentSafe, bool, bool> AfterExecute;
        public static event Action<AgentSafe> BeforeReset;
        public static event Action<AgentSafe> AfterReset;
        public static event Action<AgentSafe> BeforeDispose;
        public static event Action<AgentSafe> AfterDispose;
        public static event Action<AgentSafe> BeforeGetScoreMultForPickingThisAgent;
        public static event Func<AgentSafe, float, float> AfterGetScoreMultForPickingThisAgent;
        public static event Action<AgentSafe> BeforeGetThreatLevel;
        public static event Func<AgentSafe, float, float> AfterGetThreatLevel;
        public static event Action<AgentSafe, float _seconds => BeforeSleep;
        public static event Action<AgentSafe, float _seconds => AfterSleep;
        public static event Action<AgentSafe> BeforeOnTurnStart;
        public static event Action<AgentSafe> AfterOnTurnStart;
        public static event Action<AgentSafe> BeforeOnBeforeProcessing;
        public static event Action<AgentSafe> AfterOnBeforeProcessing;
        public static event Action<AgentSafe> BeforeOnAfterProcessing;
        public static event Action<AgentSafe> AfterOnAfterProcessing;
        public static event Action<AgentSafe> BeforeOnPicked;
        public static event Action<AgentSafe> AfterOnPicked;
        public static event Action<AgentSafe> BeforeIsDeploymentPhase;
        public static event Func<AgentSafe, bool, bool> AfterIsDeploymentPhase;
    }
    
    /// <summary>Hooks for Menace.Tactical.AI.BaseFaction</summary>
    public static class BaseFactionHooks
    {
        public static event Action<BaseFactionSafe> BeforeGetIndex;
        public static event Func<BaseFactionSafe, int, int> AfterGetIndex;
        public static event Action<BaseFactionSafe> BeforeGetFactionType;
        public static event Func<BaseFactionSafe, FactionType, FactionType> AfterGetFactionType;
        public static event Action<BaseFactionSafe> BeforeGetActors;
        public static event Func<BaseFactionSafe, List<Actor>, List<Actor>> AfterGetActors;
        public static event Action<BaseFactionSafe> BeforeGetDeadActors;
        public static event Func<BaseFactionSafe, List<Actor>, List<Actor>> AfterGetDeadActors;
        public static event Action<BaseFactionSafe> BeforeHasActors;
        public static event Func<BaseFactionSafe, bool, bool> AfterHasActors;
        public static event Action<BaseFactionSafe> BeforeHasUnlockedActors;
        public static event Func<BaseFactionSafe, bool, bool> AfterHasUnlockedActors;
        public static event Action<BaseFactionSafe, bool, bool, Nullable<ActorType>> BeforeGetActorCount;
        public static event Func<BaseFactionSafe, bool, bool, Nullable<ActorType>, int, int> AfterGetActorCount;
        public static event Action<BaseFactionSafe> BeforeGetDeadActorFractionCount;
        public static event Func<BaseFactionSafe, float, float> AfterGetDeadActorFractionCount;
        public static event Action<BaseFactionSafe> BeforeIsActive;
        public static event Func<BaseFactionSafe, bool, bool> AfterIsActive;
        public static event Action<BaseFactionSafe, Actor> BeforeAddActor;
        public static event Action<BaseFactionSafe, Actor> AfterAddActor;
        public static event Action<BaseFactionSafe, Actor> BeforeRemoveActor;
        public static event Action<BaseFactionSafe, Actor> AfterRemoveActor;
        public static event Action<BaseFactionSafe> BeforeGetAmountOfActorsLeftToAct;
        public static event Func<BaseFactionSafe, int, int> AfterGetAmountOfActorsLeftToAct;
        public static event Action<BaseFactionSafe, Actor> BeforeOnActorDeath;
        public static event Action<BaseFactionSafe, Actor> AfterOnActorDeath;
        public static event Action<BaseFactionSafe> BeforeOnTurnStart;
        public static event Action<BaseFactionSafe> AfterOnTurnStart;
        public static event Action<BaseFactionSafe> BeforeOnRoundStart;
        public static event Action<BaseFactionSafe> AfterOnRoundStart;
        public static event Action<BaseFactionSafe, Actor, int> BeforeOnOpponentSighted;
        public static event Action<BaseFactionSafe, Actor, int> AfterOnOpponentSighted;
    }
    
    /// <summary>Hooks for Menace.Tactical.AI.Behavior</summary>
    public static class BehaviorHooks
    {
        public static event Action<BehaviorSafe> BeforeGetScore;
        public static event Func<BehaviorSafe, int, int> AfterGetScore;
        public static event Action<BehaviorSafe> BeforeGetAgent;
        public static event Func<BehaviorSafe, Agent, Agent> AfterGetAgent;
        public static event Action<BehaviorSafe> BeforeGetRole;
        public static event Func<BehaviorSafe, RoleData, RoleData> AfterGetRole;
        public static event Action<BehaviorSafe> BeforeGetStrategy;
        public static event Func<BehaviorSafe, StrategyData, StrategyData> AfterGetStrategy;
        public static event Action<BehaviorSafe> BeforeIsFirstExecuted;
        public static event Func<BehaviorSafe, bool, bool> AfterIsFirstExecuted;
        public static event Action<BehaviorSafe, Agent> BeforeSetAgent;
        public static event Action<BehaviorSafe, Agent> AfterSetAgent;
        public static event Action<BehaviorSafe, Actor> BeforeCollect;
        public static event Func<BehaviorSafe, Actor, bool, bool> AfterCollect;
        public static event Action<BehaviorSafe, Actor, TileScore>> BeforeEvaluate;
        public static event Func<BehaviorSafe, Actor, TileScore>, bool, bool> AfterEvaluate;
        public static event Action<BehaviorSafe, Actor> BeforeEvaluate;
        public static event Action<BehaviorSafe, Actor> AfterEvaluate;
        public static event Action<BehaviorSafe, Actor> BeforeExecute;
        public static event Func<BehaviorSafe, Actor, bool, bool> AfterExecute;
        public static event Action<BehaviorSafe> BeforeResetScore;
        public static event Action<BehaviorSafe> AfterResetScore;
        public static event Action<BehaviorSafe> BeforeGetName;
        public static event Func<BehaviorSafe, string, string> AfterGetName;
        public static event Action<BehaviorSafe> BeforeOnBeforeProcessing;
        public static event Action<BehaviorSafe> AfterOnBeforeProcessing;
        public static event Action<BehaviorSafe> BeforeOnNewTurn;
        public static event Action<BehaviorSafe> AfterOnNewTurn;
        public static event Action<BehaviorSafe> BeforeOnClear;
        public static event Action<BehaviorSafe> AfterOnClear;
    }
    
    /// <summary>Hooks for Menace.Tactical.AI.Behaviors.Assist</summary>
    public static class AssistHooks
    {
        public static event Action<AssistSafe> BeforeGetOrder;
        public static event Func<AssistSafe, override int, override int> AfterGetOrder;
    }
    
    /// <summary>Hooks for Menace.Tactical.AI.Behaviors.Attack</summary>
    public static class AttackHooks
    {
        public static event Action<AttackSafe> BeforeGetOrder;
        public static event Func<AttackSafe, override int, override int> AfterGetOrder;
        public static event Action<AttackSafe> BeforeGetGoal;
        public static event Func<AttackSafe, Goal, Goal> AfterGetGoal;
    }
    
    /// <summary>Hooks for Menace.Tactical.AI.Behaviors.Buff</summary>
    public static class BuffHooks
    {
        public static event Action<BuffSafe> BeforeGetID;
        public static event Func<BuffSafe, override ID, override ID> AfterGetID;
    }
    
    /// <summary>Hooks for Menace.Tactical.AI.Behaviors.CreateLOSBlocker</summary>
    public static class CreateLOSBlockerHooks
    {
        public static event Action<CreateLOSBlockerSafe> BeforeGetID;
        public static event Func<CreateLOSBlockerSafe, override ID, override ID> AfterGetID;
        public static event Action<CreateLOSBlockerSafe> BeforeGetName;
        public static event Func<CreateLOSBlockerSafe, override string, override string> AfterGetName;
    }
    
    /// <summary>Hooks for Menace.Tactical.AI.Behaviors.Criterions.AvoidOpponents</summary>
    public static class AvoidOpponentsHooks
    {
        public static event Action<AvoidOpponentsSafe, Actor> BeforeIsValid;
        public static event Func<AvoidOpponentsSafe, Actor, override bool, override bool> AfterIsValid;
        public static event Action<AvoidOpponentsSafe, Actor, TileScore> BeforeEvaluate;
        public static event Func<AvoidOpponentsSafe, Actor, TileScore, override void, override void> AfterEvaluate;
    }
    
    /// <summary>Hooks for Menace.Tactical.AI.Behaviors.Criterions.ConsiderSurroundings</summary>
    public static class ConsiderSurroundingsHooks
    {
        public static event Action<ConsiderSurroundingsSafe, Actor> BeforeIsValid;
        public static event Func<ConsiderSurroundingsSafe, Actor, override bool, override bool> AfterIsValid;
        public static event Action<ConsiderSurroundingsSafe, Actor, TileScore>> BeforeCollect;
        public static event Func<ConsiderSurroundingsSafe, Actor, TileScore>, override void, override void> AfterCollect;
    }
    
    /// <summary>Hooks for Menace.Tactical.AI.Behaviors.Criterions.ConsiderZones</summary>
    public static class ConsiderZonesHooks
    {
        public static event Action<ConsiderZonesSafe, Actor> BeforeIsValid;
        public static event Func<ConsiderZonesSafe, Actor, override bool, override bool> AfterIsValid;
        public static event Action<ConsiderZonesSafe, Actor, TileScore>> BeforeCollect;
        public static event Func<ConsiderZonesSafe, Actor, TileScore>, override void, override void> AfterCollect;
        public static event Action<ConsiderZonesSafe, Actor, TileScore> BeforeEvaluate;
        public static event Func<ConsiderZonesSafe, Actor, TileScore, override void, override void> AfterEvaluate;
        public static event Action<ConsiderZonesSafe, Actor, TileScore>> BeforePostProcess;
        public static event Func<ConsiderZonesSafe, Actor, TileScore>, override void, override void> AfterPostProcess;
    }
    
    /// <summary>Hooks for Menace.Tactical.AI.Behaviors.Criterions.CoverAgainstOpponents</summary>
    public static class CoverAgainstOpponentsHooks
    {
        public static event Action<CoverAgainstOpponentsSafe, Actor> BeforeIsValid;
        public static event Func<CoverAgainstOpponentsSafe, Actor, override bool, override bool> AfterIsValid;
        public static event Action<CoverAgainstOpponentsSafe, Actor, TileScore> BeforeEvaluate;
        public static event Func<CoverAgainstOpponentsSafe, Actor, TileScore, override void, override void> AfterEvaluate;
    }
    
    /// <summary>Hooks for Menace.Tactical.AI.Behaviors.Criterions.Criterion</summary>
    public static class CriterionHooks
    {
        public static event Action<CriterionSafe> BeforeGetThreads;
        public static event Func<CriterionSafe, int, int> AfterGetThreads;
        public static event Action<CriterionSafe, Actor, TileScore>> BeforeCollect;
        public static event Action<CriterionSafe, Actor, TileScore>> AfterCollect;
        public static event Action<CriterionSafe, Actor, TileScore> BeforeEvaluate;
        public static event Action<CriterionSafe, Actor, TileScore> AfterEvaluate;
        public static event Action<CriterionSafe, Actor, TileScore>> BeforePostProcess;
        public static event Action<CriterionSafe, Actor, TileScore>> AfterPostProcess;
    }
    
    /// <summary>Hooks for Menace.Tactical.AI.Behaviors.Criterions.DistanceToCurrentTile</summary>
    public static class DistanceToCurrentTileHooks
    {
        public static event Action<DistanceToCurrentTileSafe, Actor> BeforeIsValid;
        public static event Func<DistanceToCurrentTileSafe, Actor, override bool, override bool> AfterIsValid;
        public static event Action<DistanceToCurrentTileSafe, Actor, TileScore> BeforeEvaluate;
        public static event Func<DistanceToCurrentTileSafe, Actor, TileScore, override void, override void> AfterEvaluate;
    }
    
    /// <summary>Hooks for Menace.Tactical.AI.Behaviors.Criterions.ExistingTileEffects</summary>
    public static class ExistingTileEffectsHooks
    {
        public static event Action<ExistingTileEffectsSafe, Actor> BeforeIsValid;
        public static event Func<ExistingTileEffectsSafe, Actor, override bool, override bool> AfterIsValid;
        public static event Action<ExistingTileEffectsSafe, Actor, TileScore> BeforeEvaluate;
        public static event Func<ExistingTileEffectsSafe, Actor, TileScore, override void, override void> AfterEvaluate;
    }
    
    /// <summary>Hooks for Menace.Tactical.AI.Behaviors.Criterions.FleeFromOpponents</summary>
    public static class FleeFromOpponentsHooks
    {
        public static event Action<FleeFromOpponentsSafe, Actor> BeforeIsValid;
        public static event Func<FleeFromOpponentsSafe, Actor, override bool, override bool> AfterIsValid;
        public static event Action<FleeFromOpponentsSafe, Actor, TileScore> BeforeEvaluate;
        public static event Func<FleeFromOpponentsSafe, Actor, TileScore, override void, override void> AfterEvaluate;
    }
    
    /// <summary>Hooks for Menace.Tactical.AI.Behaviors.Criterions.Roam</summary>
    public static class RoamHooks
    {
        public static event Action<RoamSafe, Actor> BeforeIsValid;
        public static event Func<RoamSafe, Actor, override bool, override bool> AfterIsValid;
        public static event Action<RoamSafe, Actor, TileScore>> BeforeCollect;
        public static event Func<RoamSafe, Actor, TileScore>, override void, override void> AfterCollect;
    }
    
    /// <summary>Hooks for Menace.Tactical.AI.Behaviors.Criterions.ThreatFromOpponents</summary>
    public static class ThreatFromOpponentsHooks
    {
        public static event Action<ThreatFromOpponentsSafe, Actor> BeforeIsValid;
        public static event Func<ThreatFromOpponentsSafe, Actor, override bool, override bool> AfterIsValid;
        public static event Action<ThreatFromOpponentsSafe> BeforeGetThreads;
        public static event Func<ThreatFromOpponentsSafe, override int, override int> AfterGetThreads;
        public static event Action<ThreatFromOpponentsSafe, Actor, TileScore> BeforeEvaluate;
        public static event Func<ThreatFromOpponentsSafe, Actor, TileScore, override void, override void> AfterEvaluate;
        public static event Action<ThreatFromOpponentsSafe, Actor, Opponent, Tile, Entity, Tile> BeforeScore;
        public static event Func<ThreatFromOpponentsSafe, Actor, Opponent, Tile, Entity, Tile, float, float> AfterScore;
    }
    
    /// <summary>Hooks for Menace.Tactical.AI.Behaviors.Criterions.WakeUp</summary>
    public static class WakeUpHooks
    {
        public static event Action<WakeUpSafe, Actor> BeforeIsValid;
        public static event Func<WakeUpSafe, Actor, override bool, override bool> AfterIsValid;
        public static event Action<WakeUpSafe, Actor, TileScore>> BeforeCollect;
        public static event Func<WakeUpSafe, Actor, TileScore>, override void, override void> AfterCollect;
    }
    
    /// <summary>Hooks for Menace.Tactical.AI.Behaviors.Data.Buff</summary>
    public static class BuffHooks
    {
        public static event Action<BuffSafe, Agent, Skill> BeforeCreateBehavior;
        public static event Func<BuffSafe, Agent, Skill, override SkillBehavior, override SkillBehavior> AfterCreateBehavior;
    }
    
    /// <summary>Hooks for Menace.Tactical.AI.Behaviors.Data.CreateLOSBlocker</summary>
    public static class CreateLOSBlockerHooks
    {
        public static event Action<CreateLOSBlockerSafe, Agent, Skill> BeforeCreateBehavior;
        public static event Func<CreateLOSBlockerSafe, Agent, Skill, override SkillBehavior, override SkillBehavior> AfterCreateBehavior;
    }
    
    /// <summary>Hooks for Menace.Tactical.AI.Behaviors.Data.GainBonusTurn</summary>
    public static class GainBonusTurnHooks
    {
        public static event Action<GainBonusTurnSafe, Agent, Skill> BeforeCreateBehavior;
        public static event Func<GainBonusTurnSafe, Agent, Skill, override SkillBehavior, override SkillBehavior> AfterCreateBehavior;
    }
    
    /// <summary>Hooks for Menace.Tactical.AI.Behaviors.Data.InflictDamage</summary>
    public static class InflictDamageHooks
    {
        public static event Action<InflictDamageSafe, Agent, Skill> BeforeCreateBehavior;
        public static event Func<InflictDamageSafe, Agent, Skill, override SkillBehavior, override SkillBehavior> AfterCreateBehavior;
    }
    
    /// <summary>Hooks for Menace.Tactical.AI.Behaviors.Data.InflictSuppression</summary>
    public static class InflictSuppressionHooks
    {
        public static event Action<InflictSuppressionSafe, Agent, Skill> BeforeCreateBehavior;
        public static event Func<InflictSuppressionSafe, Agent, Skill, override SkillBehavior, override SkillBehavior> AfterCreateBehavior;
    }
    
    /// <summary>Hooks for Menace.Tactical.AI.Behaviors.Data.Mindray</summary>
    public static class MindrayHooks
    {
        public static event Action<MindraySafe, Agent, Skill> BeforeCreateBehavior;
        public static event Func<MindraySafe, Agent, Skill, override SkillBehavior, override SkillBehavior> AfterCreateBehavior;
    }
    
    /// <summary>Hooks for Menace.Tactical.AI.Behaviors.Data.MovementSkill</summary>
    public static class MovementSkillHooks
    {
        public static event Action<MovementSkillSafe, Agent, Skill> BeforeCreateBehavior;
        public static event Func<MovementSkillSafe, Agent, Skill, override SkillBehavior, override SkillBehavior> AfterCreateBehavior;
    }
    
    /// <summary>Hooks for Menace.Tactical.AI.Behaviors.Data.Reload</summary>
    public static class ReloadHooks
    {
        public static event Action<ReloadSafe, Agent, Skill> BeforeCreateBehavior;
        public static event Func<ReloadSafe, Agent, Skill, override SkillBehavior, override SkillBehavior> AfterCreateBehavior;
    }
    
    /// <summary>Hooks for Menace.Tactical.AI.Behaviors.Data.RemoveStatusEffect</summary>
    public static class RemoveStatusEffectHooks
    {
        public static event Action<RemoveStatusEffectSafe, Agent, Skill> BeforeCreateBehavior;
        public static event Func<RemoveStatusEffectSafe, Agent, Skill, override SkillBehavior, override SkillBehavior> AfterCreateBehavior;
    }
    
    /// <summary>Hooks for Menace.Tactical.AI.Behaviors.Data.Scan</summary>
    public static class ScanHooks
    {
        public static event Action<ScanSafe, Agent, Skill> BeforeCreateBehavior;
        public static event Func<ScanSafe, Agent, Skill, override SkillBehavior, override SkillBehavior> AfterCreateBehavior;
    }
    
    /// <summary>Hooks for Menace.Tactical.AI.Behaviors.Data.SpawnHovermine</summary>
    public static class SpawnHovermineHooks
    {
        public static event Action<SpawnHovermineSafe, Agent, Skill> BeforeCreateBehavior;
        public static event Func<SpawnHovermineSafe, Agent, Skill, override SkillBehavior, override SkillBehavior> AfterCreateBehavior;
    }
    
    /// <summary>Hooks for Menace.Tactical.AI.Behaviors.Data.SpawnPhantom</summary>
    public static class SpawnPhantomHooks
    {
        public static event Action<SpawnPhantomSafe, Agent, Skill> BeforeCreateBehavior;
        public static event Func<SpawnPhantomSafe, Agent, Skill, override SkillBehavior, override SkillBehavior> AfterCreateBehavior;
    }
    
    /// <summary>Hooks for Menace.Tactical.AI.Behaviors.Data.Stun</summary>
    public static class StunHooks
    {
        public static event Action<StunSafe, Agent, Skill> BeforeCreateBehavior;
        public static event Func<StunSafe, Agent, Skill, override SkillBehavior, override SkillBehavior> AfterCreateBehavior;
    }
    
    /// <summary>Hooks for Menace.Tactical.AI.Behaviors.Data.SupplyAmmo</summary>
    public static class SupplyAmmoHooks
    {
        public static event Action<SupplyAmmoSafe, Agent, Skill> BeforeCreateBehavior;
        public static event Func<SupplyAmmoSafe, Agent, Skill, override SkillBehavior, override SkillBehavior> AfterCreateBehavior;
    }
    
    /// <summary>Hooks for Menace.Tactical.AI.Behaviors.Data.TargetDesignator</summary>
    public static class TargetDesignatorHooks
    {
        public static event Action<TargetDesignatorSafe, Agent, Skill> BeforeCreateBehavior;
        public static event Func<TargetDesignatorSafe, Agent, Skill, override SkillBehavior, override SkillBehavior> AfterCreateBehavior;
    }
    
    /// <summary>Hooks for Menace.Tactical.AI.Behaviors.Data.TransportEntity</summary>
    public static class TransportEntityHooks
    {
        public static event Action<TransportEntitySafe, Agent, Skill> BeforeCreateBehavior;
        public static event Func<TransportEntitySafe, Agent, Skill, override SkillBehavior, override SkillBehavior> AfterCreateBehavior;
    }
    
    /// <summary>Hooks for Menace.Tactical.AI.Behaviors.Data.TurnArmorTowardsThreat</summary>
    public static class TurnArmorTowardsThreatHooks
    {
        public static event Action<TurnArmorTowardsThreatSafe, Agent, Skill> BeforeCreateBehavior;
        public static event Func<TurnArmorTowardsThreatSafe, Agent, Skill, override SkillBehavior, override SkillBehavior> AfterCreateBehavior;
    }
    
    /// <summary>Hooks for Menace.Tactical.AI.Behaviors.Deploy</summary>
    public static class DeployHooks
    {
        public static event Action<DeploySafe> BeforeGetOrder;
        public static event Func<DeploySafe, override int, override int> AfterGetOrder;
        public static event Action<DeploySafe> BeforeGetID;
        public static event Func<DeploySafe, override ID, override ID> AfterGetID;
    }
    
    /// <summary>Hooks for Menace.Tactical.AI.Behaviors.GainBonusTurn</summary>
    public static class GainBonusTurnHooks
    {
        public static event Action<GainBonusTurnSafe> BeforeGetOrder;
        public static event Func<GainBonusTurnSafe, override int, override int> AfterGetOrder;
        public static event Action<GainBonusTurnSafe> BeforeGetID;
        public static event Func<GainBonusTurnSafe, override ID, override ID> AfterGetID;
        public static event Action<GainBonusTurnSafe> BeforeGetName;
        public static event Func<GainBonusTurnSafe, override string, override string> AfterGetName;
        public static event Action<GainBonusTurnSafe> BeforeOnNewTurn;
        public static event Func<GainBonusTurnSafe, override void, override void> AfterOnNewTurn;
    }
    
    /// <summary>Hooks for Menace.Tactical.AI.Behaviors.Idle</summary>
    public static class IdleHooks
    {
        public static event Action<IdleSafe> BeforeGetOrder;
        public static event Func<IdleSafe, override int, override int> AfterGetOrder;
        public static event Action<IdleSafe> BeforeGetID;
        public static event Func<IdleSafe, override ID, override ID> AfterGetID;
    }
    
    /// <summary>Hooks for Menace.Tactical.AI.Behaviors.InflictDamage</summary>
    public static class InflictDamageHooks
    {
        public static event Action<InflictDamageSafe> BeforeGetID;
        public static event Func<InflictDamageSafe, override ID, override ID> AfterGetID;
        public static event Action<InflictDamageSafe> BeforeGetName;
        public static event Func<InflictDamageSafe, override string, override string> AfterGetName;
    }
    
    /// <summary>Hooks for Menace.Tactical.AI.Behaviors.InflictSuppression</summary>
    public static class InflictSuppressionHooks
    {
        public static event Action<InflictSuppressionSafe> BeforeGetID;
        public static event Func<InflictSuppressionSafe, override ID, override ID> AfterGetID;
        public static event Action<InflictSuppressionSafe> BeforeGetName;
        public static event Func<InflictSuppressionSafe, override string, override string> AfterGetName;
    }
    
    /// <summary>Hooks for Menace.Tactical.AI.Behaviors.Mindray</summary>
    public static class MindrayHooks
    {
        public static event Action<MindraySafe> BeforeGetID;
        public static event Func<MindraySafe, override ID, override ID> AfterGetID;
        public static event Action<MindraySafe> BeforeGetName;
        public static event Func<MindraySafe, override string, override string> AfterGetName;
    }
    
    /// <summary>Hooks for Menace.Tactical.AI.Behaviors.Move</summary>
    public static class MoveHooks
    {
        public static event Action<MoveSafe> BeforeGetOrder;
        public static event Func<MoveSafe, override int, override int> AfterGetOrder;
        public static event Action<MoveSafe> BeforeGetID;
        public static event Func<MoveSafe, override ID, override ID> AfterGetID;
        public static event Action<MoveSafe> BeforeIsMovementDone;
        public static event Func<MoveSafe, bool, bool> AfterIsMovementDone;
        public static event Action<MoveSafe> BeforeHasMovedThisTurn;
        public static event Func<MoveSafe, bool, bool> AfterHasMovedThisTurn;
        public static event Action<MoveSafe> BeforeHasDelayedMovementThisTurn;
        public static event Func<MoveSafe, bool, bool> AfterHasDelayedMovementThisTurn;
        public static event Action<MoveSafe> BeforeIsDelayingMovement;
        public static event Func<MoveSafe, bool, bool> AfterIsDelayingMovement;
        public static event Action<MoveSafe> BeforeIsInsideContainerAndInert;
        public static event Func<MoveSafe, bool, bool> AfterIsInsideContainerAndInert;
        public static event Action<MoveSafe> BeforeGetTargetTile;
        public static event Func<MoveSafe, TileScore, TileScore> AfterGetTargetTile;
        public static event Action<MoveSafe> BeforeOnBeforeProcessing;
        public static event Func<MoveSafe, override void, override void> AfterOnBeforeProcessing;
        public static event Action<MoveSafe> BeforeOnNewTurn;
        public static event Func<MoveSafe, override void, override void> AfterOnNewTurn;
        public static event Action<MoveSafe> BeforeOnClear;
        public static event Func<MoveSafe, override void, override void> AfterOnClear;
    }
    
    /// <summary>Hooks for Menace.Tactical.AI.Behaviors.MovementSkill</summary>
    public static class MovementSkillHooks
    {
        public static event Action<MovementSkillSafe> BeforeGetOrder;
        public static event Func<MovementSkillSafe, override int, override int> AfterGetOrder;
        public static event Action<MovementSkillSafe> BeforeGetID;
        public static event Func<MovementSkillSafe, override ID, override ID> AfterGetID;
        public static event Action<MovementSkillSafe> BeforeGetName;
        public static event Func<MovementSkillSafe, override string, override string> AfterGetName;
        public static event Action<MovementSkillSafe> BeforeOnNewTurn;
        public static event Func<MovementSkillSafe, override void, override void> AfterOnNewTurn;
    }
    
    /// <summary>Hooks for Menace.Tactical.AI.Behaviors.Reload</summary>
    public static class ReloadHooks
    {
        public static event Action<ReloadSafe> BeforeGetOrder;
        public static event Func<ReloadSafe, override int, override int> AfterGetOrder;
        public static event Action<ReloadSafe> BeforeGetID;
        public static event Func<ReloadSafe, override ID, override ID> AfterGetID;
    }
    
    /// <summary>Hooks for Menace.Tactical.AI.Behaviors.RemoveStatusEffect</summary>
    public static class RemoveStatusEffectHooks
    {
        public static event Action<RemoveStatusEffectSafe> BeforeGetOrder;
        public static event Func<RemoveStatusEffectSafe, override int, override int> AfterGetOrder;
        public static event Action<RemoveStatusEffectSafe> BeforeGetID;
        public static event Func<RemoveStatusEffectSafe, override ID, override ID> AfterGetID;
    }
    
    /// <summary>Hooks for Menace.Tactical.AI.Behaviors.Scan</summary>
    public static class ScanHooks
    {
        public static event Action<ScanSafe> BeforeGetOrder;
        public static event Func<ScanSafe, override int, override int> AfterGetOrder;
        public static event Action<ScanSafe> BeforeGetID;
        public static event Func<ScanSafe, override ID, override ID> AfterGetID;
        public static event Action<ScanSafe> BeforeGetName;
        public static event Func<ScanSafe, override string, override string> AfterGetName;
        public static event Action<ScanSafe> BeforeOnNewTurn;
        public static event Func<ScanSafe, override void, override void> AfterOnNewTurn;
    }
    
    /// <summary>Hooks for Menace.Tactical.AI.Behaviors.SpawnHovermine</summary>
    public static class SpawnHovermineHooks
    {
        public static event Action<SpawnHovermineSafe> BeforeGetID;
        public static event Func<SpawnHovermineSafe, override ID, override ID> AfterGetID;
        public static event Action<SpawnHovermineSafe> BeforeGetName;
        public static event Func<SpawnHovermineSafe, override string, override string> AfterGetName;
    }
    
    /// <summary>Hooks for Menace.Tactical.AI.Behaviors.SpawnPhantom</summary>
    public static class SpawnPhantomHooks
    {
        public static event Action<SpawnPhantomSafe> BeforeGetID;
        public static event Func<SpawnPhantomSafe, override ID, override ID> AfterGetID;
        public static event Action<SpawnPhantomSafe> BeforeGetName;
        public static event Func<SpawnPhantomSafe, override string, override string> AfterGetName;
    }
    
    /// <summary>Hooks for Menace.Tactical.AI.Behaviors.Stun</summary>
    public static class StunHooks
    {
        public static event Action<StunSafe> BeforeGetID;
        public static event Func<StunSafe, override ID, override ID> AfterGetID;
        public static event Action<StunSafe> BeforeGetName;
        public static event Func<StunSafe, override string, override string> AfterGetName;
    }
    
    /// <summary>Hooks for Menace.Tactical.AI.Behaviors.SupplyAmmo</summary>
    public static class SupplyAmmoHooks
    {
        public static event Action<SupplyAmmoSafe> BeforeGetID;
        public static event Func<SupplyAmmoSafe, override ID, override ID> AfterGetID;
    }
    
    /// <summary>Hooks for Menace.Tactical.AI.Behaviors.TargetDesignator</summary>
    public static class TargetDesignatorHooks
    {
        public static event Action<TargetDesignatorSafe> BeforeGetID;
        public static event Func<TargetDesignatorSafe, override ID, override ID> AfterGetID;
        public static event Action<TargetDesignatorSafe> BeforeGetName;
        public static event Func<TargetDesignatorSafe, override string, override string> AfterGetName;
    }
    
    /// <summary>Hooks for Menace.Tactical.AI.Behaviors.TransportEntity</summary>
    public static class TransportEntityHooks
    {
        public static event Action<TransportEntitySafe> BeforeIsTargetReached;
        public static event Func<TransportEntitySafe, bool, bool> AfterIsTargetReached;
        public static event Action<TransportEntitySafe> BeforeGetOrder;
        public static event Func<TransportEntitySafe, override int, override int> AfterGetOrder;
        public static event Action<TransportEntitySafe> BeforeGetID;
        public static event Func<TransportEntitySafe, override ID, override ID> AfterGetID;
    }
    
    /// <summary>Hooks for Menace.Tactical.AI.Behaviors.TurnArmorTowardsThreat</summary>
    public static class TurnArmorTowardsThreatHooks
    {
        public static event Action<TurnArmorTowardsThreatSafe> BeforeGetOrder;
        public static event Func<TurnArmorTowardsThreatSafe, override int, override int> AfterGetOrder;
        public static event Action<TurnArmorTowardsThreatSafe> BeforeGetID;
        public static event Func<TurnArmorTowardsThreatSafe, override ID, override ID> AfterGetID;
        public static event Action<TurnArmorTowardsThreatSafe> BeforeOnNewTurn;
        public static event Func<TurnArmorTowardsThreatSafe, override void, override void> AfterOnNewTurn;
    }
    
    /// <summary>Hooks for Menace.Tactical.AI.Data.Assessment</summary>
    public static class AssessmentHooks
    {
        public static event Action<AssessmentSafe, OperationalZones, IEnumerable<Actor>> BeforeUpdate;
        public static event Action<AssessmentSafe, OperationalZones, IEnumerable<Actor>> AfterUpdate;
        public static event Action<AssessmentSafe> BeforeReset;
        public static event Action<AssessmentSafe> AfterReset;
        public static event Action<AssessmentSafe, Actor> BeforeRemoveActor;
        public static event Action<AssessmentSafe, Actor> AfterRemoveActor;
    }
    
    /// <summary>Hooks for Menace.Tactical.AI.Data.StrategyData</summary>
    public static class StrategyDataHooks
    {
        public static event Action<StrategyDataSafe> BeforeGetFaction;
        public static event Func<StrategyDataSafe, AIFaction, AIFaction> AfterGetFaction;
        public static event Action<StrategyDataSafe> BeforeUpdate;
        public static event Action<StrategyDataSafe> AfterUpdate;
    }
    
    /// <summary>Hooks for Menace.Tactical.AI.Data.TileData</summary>
    public static class TileDataHooks
    {
        public static event Action<TileDataSafe, Actor, float> BeforeIsAvailable;
        public static event Func<TileDataSafe, Actor, float, bool, bool> AfterIsAvailable;
        public static event Action<TileDataSafe, Actor, float> BeforeGrab;
        public static event Action<TileDataSafe, Actor, float> AfterGrab;
        public static event Action<TileDataSafe, Actor> BeforeRelease;
        public static event Action<TileDataSafe, Actor> AfterRelease;
    }
    
    /// <summary>Hooks for Menace.Tactical.AI.Data.TileScore</summary>
    public static class TileScoreHooks
    {
        public static event Action<TileScoreSafe> BeforeGetClone;
        public static event Func<TileScoreSafe, TileScore, TileScore> AfterGetClone;
        public static event Action<TileScoreSafe> BeforeGetScore;
        public static event Func<TileScoreSafe, float, float> AfterGetScore;
        public static event Action<TileScoreSafe> BeforeGetScoreWithoutDistance;
        public static event Func<TileScoreSafe, float, float> AfterGetScoreWithoutDistance;
        public static event Action<TileScoreSafe> BeforeGetScaledScore;
        public static event Func<TileScoreSafe, float, float> AfterGetScaledScore;
        public static event Action<TileScoreSafe, Tile> BeforeReset;
        public static event Action<TileScoreSafe, Tile> AfterReset;
        public static event Action<TileScoreSafe> BeforeToString;
        public static event Func<TileScoreSafe, override string, override string> AfterToString;
    }
    
    /// <summary>Hooks for Menace.Tactical.AI.Data.TileScorePool</summary>
    public static class TileScorePoolHooks
    {
        public static event Action<TileScorePoolSafe, Tile> BeforeGrab;
        public static event Func<TileScorePoolSafe, Tile, TileScore, TileScore> AfterGrab;
        public static event Action<TileScorePoolSafe, TileScore> BeforeReturn;
        public static event Action<TileScorePoolSafe, TileScore> AfterReturn;
    }
    
    /// <summary>Hooks for Menace.Tactical.AI.OperationalZones</summary>
    public static class OperationalZonesHooks
    {
        public static event Action<OperationalZonesSafe> BeforeGetNumZones;
        public static event Func<OperationalZonesSafe, int, int> AfterGetNumZones;
        public static event Action<OperationalZonesSafe> BeforeReset;
        public static event Action<OperationalZonesSafe> AfterReset;
        public static event Action<OperationalZonesSafe, Tile> BeforeGetZone;
        public static event Func<OperationalZonesSafe, Tile, Zone, Zone> AfterGetZone;
        public static event Action<OperationalZonesSafe> BeforeGetZones;
        public static event Func<OperationalZonesSafe, List<Zone>, List<Zone>> AfterGetZones;
        public static event Action<OperationalZonesSafe, List<Zone>, int _orders => BeforeQueryZones;
        public static event Action<OperationalZonesSafe, List<Zone>, int _orders => AfterQueryZones;
        public static event Action<OperationalZonesSafe, Tile, int, List<Zone>, int _orders => BeforeQueryZonesInRange;
        public static event Action<OperationalZonesSafe, Tile, int, List<Zone>, int _orders => AfterQueryZonesInRange;
        public static event Action<OperationalZonesSafe, Zone> BeforeAddZone;
        public static event Action<OperationalZonesSafe, Zone> AfterAddZone;
    }
    
    /// <summary>Hooks for Menace.Tactical.AI.Opponent</summary>
    public static class OpponentHooks
    {
        public static event Action<OpponentSafe> BeforeIsKnown;
        public static event Func<OpponentSafe, bool, bool> AfterIsKnown;
    }
    
    /// <summary>Hooks for Menace.Tactical.AI.PlayerFaction</summary>
    public static class PlayerFactionHooks
    {
        public static event Action<PlayerFactionSafe> BeforeIsAlliedWithPlayer;
        public static event Func<PlayerFactionSafe, override bool, override bool> AfterIsAlliedWithPlayer;
        public static event Action<PlayerFactionSafe, int> BeforeIsAlliedWith;
        public static event Func<PlayerFactionSafe, int, override bool, override bool> AfterIsAlliedWith;
        public static event Action<PlayerFactionSafe> BeforeProcess;
        public static event Func<PlayerFactionSafe, override void, override void> AfterProcess;
    }
    
    /// <summary>Hooks for Menace.Tactical.AI.SkillBehavior</summary>
    public static class SkillBehaviorHooks
    {
        public static event Action<SkillBehaviorSafe> BeforeGetSkill;
        public static event Func<SkillBehaviorSafe, Skill, Skill> AfterGetSkill;
        public static event Action<SkillBehaviorSafe> BeforeGetSkillIDHash;
        public static event Func<SkillBehaviorSafe, int, int> AfterGetSkillIDHash;
        public static event Action<SkillBehaviorSafe, bool, int, Tile, Goal, Skill, Tile, Tile> BeforeGetTargetValue;
        public static event Func<SkillBehaviorSafe, bool, int, Tile, Goal, Skill, Tile, Tile, float, float> AfterGetTargetValue;
    }
    
    /// <summary>Hooks for Menace.Tactical.AI.Zone</summary>
    public static class ZoneHooks
    {
        public static event Action<ZoneSafe, ZoneOrder> BeforeHasOrder;
        public static event Func<ZoneSafe, ZoneOrder, bool, bool> AfterHasOrder;
    }
    
}