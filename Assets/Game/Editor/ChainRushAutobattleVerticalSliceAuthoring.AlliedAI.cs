using System;
using System.Collections.Generic;
using System.Linq;
using Core;
using Core.Activities;
using Core.AI;
using Core.AI.Actions;
using Core.AI.Conditions;
using Core.CapabilityHosts;
using Core.Diplomacy;
using Core.Economy;
using Core.GameRuntime.Installers;
using Core.HostValues;
using Core.Skills;
using Core.Taxonomy;
using Core.World;
using UnityEditor;
using FrameworkSkillData = Core.Skills.SkillData;

namespace ChainRush.Editor
{
    public static partial class ChainRushAutobattleVerticalSliceAuthoring
    {
        internal static void ApplyAlliedAI()
        {
            var economy = LoadRequired<EconomyDefinitionsInstallerData>(EconomyInstallerPath);
            var definitions = GetField<List<EconomyAssetData>>(economy, "assets");
            var installer = LoadRequired<GameplaySkillsInstallerData>(SkillsInstallerPath);
            var skills = GetField<List<FrameworkSkillData>>(installer, "skills");
            double step = GetField<float>(LoadRequired<ActivityData>(ActivityPath).Schedule, "tickDelta");
            var heroRole = WriteCombatTerm("HeroRole", LoadRequired<TaxonomyTermData>(CombatantRolePath));
            foreach (string hero in new[] { "Perfume", "Tabasco" })
            {
                var host = LoadRequired<CapabilityHostData>(SharedRoot + "/Units/" + hero + "/" + hero + ".asset");
                AddUnique(host.Tags, heroRole);
                EditorUtility.SetDirty(host);
            }
            var anchor = WriteCombatTerm("AllyAnchor", LoadRequired<TaxonomyTermData>(CombatTargetPath));
            var support = WriteCombatTerm("AllySupportTarget", LoadRequired<TaxonomyTermData>(CombatTargetPath));
            var states = new Dictionary<string, TaxonomyTermData>();
            foreach (string name in new[] { "AcquireAnchor", "SearchEnemy", "ChaseEnemy", "EngageEnemy", "FollowAnchor", "CatchupAnchor", "ReturnAnchor", "RecoverLeash", "CombatLinger", "IdleAlly",
                "FindHeroDefend", "FindAllyDefend", "FindAllyAssist", "DefendHero", "DefendAlly", "AssistAlly" })
                states.Add(name, WriteCombatTerm(name, LoadRequired<TaxonomyTermData>(CombatStateAPath)));
            foreach (string name in new[] { "Water", "Cola" })
            {
                var source = LoadRequired<UnitData>("Assets/Game/Resources/Units/" + name + "Data.asset");
                var profile = LoadRequired<UnitAIProfile>("Assets/Game/Resources/UnitAIProfile/" + (source.IsMelee ? "Warrior" : "Range") + "UnitProfile.asset");
                if (!profile.overrideDistances || !profile.overrideDetection || !profile.overrideTiming || !profile.overrideScoring || !profile.overrideSpeed)
                    throw new InvalidOperationException("The included AI profile must author its complete behavior: " + profile.name);
                var sourceAI = LoadRequired<UnityEngine.GameObject>("Assets/Game/Prefabs/Units/Unit.prefab")
                    .GetComponent<UnitAIController>();
                if (sourceAI == null) throw new InvalidOperationException("Missing source ally response configuration.");
                var pattern = WriteAnchorPattern(source, profile, sourceAI, anchor, step);
                var normalTarget = WriteAnchorTarget(name + "AnchorTarget", pattern, false);
                var formationTarget = WriteAnchorTarget(name + "FormationTarget", pattern, true);
                for (int form = 1; form <= source.mergeStates.Count; form++)
                {
                    string unit = name + "Unit" + (form == 1 ? "" : form.ToString());
                    var host = LoadRequired<CapabilityHostData>(SharedRoot + "/Units/" + name + "/" + unit + ".asset");
                    var attack = LoadRequired<FrameworkSkillData>(SkillsRoot + "/" + unit + "Attack.asset");
                    var chase = LoadRequired<FrameworkSkillData>(SkillsRoot + "/" + unit + "Approach.asset");
                    var follow = ChainRushBoardPlannerAuthoring.WriteContentAsset(SkillsRoot + "/" + unit + "Follow.asset",
                        chase, host.Id + ".follow", definitions);
                    var returning = ChainRushBoardPlannerAuthoring.WriteContentAsset(SkillsRoot + "/" + unit + "Return.asset",
                        chase, host.Id + ".return", definitions);
                    foreach (var movement in new[] { follow, returning })
                    {
                        SetField(movement, "targetTags", new List<TaxonomyTermData> { LoadRequired<TaxonomyTermData>(CombatantRolePath) });
                        SetField(movement, "analyticSupposedTarget", DiplomacyDispositionType.Friendly);
                        AddUnique(skills, movement);
                        EditorUtility.SetDirty(movement);
                    }
                    SetField(returning.Effects[0], "value", RoundContent(1000 * step * profile.heroSpeedMultiplier));
                    var followingPosition = ChainRushBoardPlannerAuthoring.WriteContentAsset(SkillsRoot + "/" + unit + "FollowPosition.asset",
                        follow, host.Id + ".follow-position", definitions);
                    var catchingUp = ChainRushBoardPlannerAuthoring.WriteContentAsset(SkillsRoot + "/" + unit + "Catchup.asset",
                        follow, host.Id + ".catchup", definitions);
                    SetField(catchingUp.Effects[0], "value", RoundContent(1000 * step * profile.formation.catchupSpeedMultiplier));
                    foreach (var movement in new[] { followingPosition, catchingUp, returning })
                    {
                        SetField(movement, "targetType", SkillTargetType.Position);
                        SetField(movement, "targetTags", new List<TaxonomyTermData>());
                        SetField(movement.Effects[0], "targetGeometryType", WorldTargetGeometryType.None);
                        SetField(movement.Effects[0], "approachInteractionTags", new List<TaxonomyTermData>());
                        AddUnique(skills, movement); EditorUtility.SetDirty(movement);
                    }
                    var seeds = host.WalletEntries.Single(entry => entry.Wallet == LoadRequired<EconomyWalletData>(UnitWalletPath)).Seed;
                    seeds.RemoveAll(seed => seed.Asset == follow || seed.Asset == returning || seed.Asset == followingPosition || seed.Asset == catchingUp);
                    seeds.Add(new SeedEntry(follow, 1, EconomyFormType.Stack));
                    seeds.Add(new SeedEntry(returning, 1, EconomyFormType.Stack));
                    seeds.Add(new SeedEntry(followingPosition, 1, EconomyFormType.Stack));
                    seeds.Add(new SeedEntry(catchingUp, 1, EconomyFormType.Stack));
                    var brain = LoadRequired<AIBrainData>(AIRoot + "/" + unit + "Brain.asset");
                    WriteAlliedBehavior(brain, attack, chase, follow, returning, profile, step, states, anchor, support, heroRole,
                        GetField<float>(sourceAI, "heroHitResponseDuration"), GetField<float>(sourceAI, "allyHitResponseDuration"),
                        followingPosition, catchingUp, pattern, normalTarget, formationTarget);
                    EditorUtility.SetDirty(brain);
                    EditorUtility.SetDirty(host);
                }
            }
            EditorUtility.SetDirty(economy);
            EditorUtility.SetDirty(installer);
        }

        static void WriteAlliedBehavior(AIBrainData brain, FrameworkSkillData attack, FrameworkSkillData chase,
            FrameworkSkillData follow, FrameworkSkillData returning, UnitAIProfile profile, double step,
            Dictionary<string, TaxonomyTermData> tags, TaxonomyTermData anchor, TaxonomyTermData support, TaxonomyTermData heroRole,
            float heroResponse, float allyResponse, FrameworkSkillData followPosition, FrameworkSkillData catchup,
            AIBrainAnchorPatternData pattern, AIBrainAnchorTargetResolverData normalTarget, AIBrainAnchorTargetResolverData formationTarget)
        {
            var nodeId = LoadRequired<TaxonomyTermData>(CombatNodePath);
            var target = LoadRequired<TaxonomyTermData>(CombatTargetPath);
            var defeated = LoadRequired<TaxonomyTermData>(DefeatStatePath);
            var node = brain.Nodes.Single(value => value.NodeId == nodeId);
            var death = node.States.Single(value => value.Tag == defeated);
            node.States.Clear(); node.States.Add(death); brain.Transitions.Clear();
            SetField(node, "entryState", tags["AcquireAnchor"]);
            var states = new Dictionary<string, AIBrainStateData>();
            foreach (var pair in tags)
            {
                var state = new AIBrainStateData(); SetField(state, "tag", pair.Value);
                states.Add(pair.Key, state); node.States.Add(state);
                var condition = new HostValueConditionAIBrainConditionData();
                SetField(condition, "hostValue", LoadRequired<HostValueData>(HealthPath));
                SetField(condition, "compareOperation", CompareOperation.LessOrEqual);
                SetField(condition, "targetValue", 0L);
                brain.Transitions.Add(CreateBrainTransition(pair.Value, nodeId, defeated, condition));
            }
            var clearAnchor = new ClearAIBrainTargetActionData(); SetField(clearAnchor, "targetKey", anchor);
            states["AcquireAnchor"].OnEnterActions.Add(clearAnchor);
            var findAnchor = new SelectActivityTargetAIBrainActionData();
            SetField(findAnchor, "targetKey", anchor);
            SetField(findAnchor, "requiredDisposition", DiplomacyDispositionType.Friendly);
            SetField(findAnchor, "requiredTargetTags", new List<TaxonomyTermData> { heroRole });
            SetField(findAnchor, "blockedTargetStates", new List<TaxonomyTermData> { defeated });
            states["AcquireAnchor"].OnTickActions.Add(findAnchor);
            var observe = new ObserveAnchorPatternAIBrainActionData(); SetField(observe, "pattern", pattern);
            states["AcquireAnchor"].OnTickActions.Add(observe);
            foreach (string state in new[] { "ChaseEnemy", "EngageEnemy", "DefendHero", "DefendAlly", "AssistAlly", "CombatLinger" })
            {
                var threat = new ObserveAnchorPatternAIBrainActionData();
                SetField(threat, "pattern", pattern); SetField(threat, "markThreat", true);
                states[state].OnEnterActions.Add(threat);
            }
            Add("AcquireAnchor", "FindHeroDefend", Delay(1), CreateTargetExistsCondition(anchor, true));
            Add("AcquireAnchor", "SearchEnemy", Result(), Delay(1), CreateTargetExistsCondition(anchor, false));
            var engaged = new List<TaxonomyTermData> { tags["ChaseEnemy"], tags["EngageEnemy"] };
            AddSupportSearch("FindHeroDefend", "DefendHero", "FindAllyDefend", Distance(profile.returnRadius), heroRole, heroResponse);
            AddSupportSearch("FindAllyDefend", "DefendAlly", "SearchEnemy", Distance(profile.allyDefendRadius),
                LoadRequired<TaxonomyTermData>(CombatantRolePath), allyResponse);
            AddSupportSearch("FindAllyAssist", "AssistAlly", "FollowAnchor", Distance(profile.allyDefendRadius),
                LoadRequired<TaxonomyTermData>(CombatantRolePath), 0);
            foreach (string name in new[] { "DefendHero", "DefendAlly", "AssistAlly" })
            {
                AddMovement(name, follow, support);
                Add(name, "ReturnAnchor", AtDistance(anchor, Distance(profile.returnRadius), CompareOperation.Greater));
                var lost = new TargetBrainStateAIBrainConditionData();
                SetField(lost, "targetKey", support);
                SetField(lost, "blockedStates", new List<TaxonomyTermData> { defeated });
                SetField(lost, "shouldMatch", false);
                Add(name, "AcquireAnchor", lost);
                Add(name, "AcquireAnchor", CreateTargetExistsCondition(support, false));
                if (name == "AssistAlly")
                {
                    var finished = new TargetBrainStateAIBrainConditionData();
                    SetField(finished, "targetKey", support); SetField(finished, "requiredStates", new List<TaxonomyTermData>(engaged));
                    SetField(finished, "shouldMatch", false);
                    Add(name, "AcquireAnchor", finished);
                }
                else
                {
                    var expired = new RecentTargetValueDecreaseAIBrainConditionData();
                    SetField(expired, "targetKey", support);
                    SetField(expired, "observation", RecentDecrease(name == "DefendHero" ? heroResponse : allyResponse));
                    SetField(expired, "expected", false);
                    Add(name, "AcquireAnchor", expired);
                }
            }

            var clear = new ClearAIBrainTargetActionData(); SetField(clear, "targetKey", target);
            states["SearchEnemy"].OnEnterActions.Add(clear);
            var search = new SelectEntityTargetByQueryAIBrainActionData();
            SetField(search, "targetKey", target);
            SetField(search, "searchOriginType", AIBrainTargetSearchOriginType.NamedTargetOrOwner);
            SetField(search, "searchOriginKey", anchor);
            SetField(search, "captureSearchOrigin", true);
            SetField(search, "searchRadius", Distance(profile.heroThreatRadius > 0 ? profile.heroThreatRadius : profile.enemyDetectRadius));
            SetField(search, "maxCandidates", 10);
            SetField(search, "compatibleSkill", attack);
            SetField(search, "targetGeometryType", WorldTargetGeometryType.Spatial);
            SetField(search, "requiredTargetTags", new List<TaxonomyTermData> { LoadRequired<TaxonomyTermData>(CombatantRolePath) });
            SetField(search, "blockedTargetStates", new List<TaxonomyTermData> { defeated });
            var score = new AIBrainTargetDistanceScoreData();
            SetField(score, "maximumOwnerDistance", Distance(profile.maxUnitAcquireDistance));
            SetField(score, "preferredOriginRadius", Distance(profile.combatRadius));
            SetField(score, "originWeight", profile.wHero * 1000);
            SetField(score, "ownerWeight", profile.wUnit * 1000);
            SetField(score, "crowdWeight", profile.wCrowd);
            SetField(score, "leashWeight", profile.wLeash / 1000);
            SetField(search, "distanceScore", score);
            states["SearchEnemy"].OnTickActions.Add(search);
            Add("SearchEnemy", "ChaseEnemy", CreateTargetExistsCondition(target, true));
            Add("SearchEnemy", "FindAllyAssist", Result(), CreateTargetExistsCondition(anchor, true));
            Add("SearchEnemy", "IdleAlly", Result(), CreateTargetExistsCondition(anchor, false));

            AddMovement("ChaseEnemy", chase, target);
            AddMovement("FollowAnchor", followPosition, anchor, normalTarget);
            AddMovement("CatchupAnchor", catchup, anchor, formationTarget);
            AddMovement("ReturnAnchor", returning, anchor, formationTarget);
            AddMovement("RecoverLeash", followPosition, anchor, formationTarget);
            var startCatchup = new AnchorPatternCatchupAIBrainConditionData(); SetField(startCatchup, "pattern", pattern);
            var endCatchup = new AnchorPatternCatchupAIBrainConditionData(); SetField(endCatchup, "pattern", pattern); SetField(endCatchup, "expected", false);
            Add("FollowAnchor", "CatchupAnchor", startCatchup);
            Add("CatchupAnchor", "FollowAnchor", endCatchup);
            foreach (string name in new[] { "ChaseEnemy", "EngageEnemy", "FollowAnchor", "CatchupAnchor" })
                Add(name, "ReturnAnchor", AtDistance(anchor, Distance(profile.returnRadius), CompareOperation.Greater));
            foreach (string name in new[] { "ChaseEnemy", "EngageEnemy" })
            {
                var leash = new TargetAcquisitionLeashAIBrainConditionData();
                SetField(leash, "targetKey", target);
                SetField(leash, "radius", Distance(profile.maxChaseDistanceFromAnchor));
                SetField(leash, "commitmentDuration", checked((int)ContentDuration(profile.enemyCommitTime, step)));
                Add(name, "RecoverLeash", CreateTargetExistsCondition(anchor, true), leash);
                var lost = new AnyAIBrainConditionData();
                lost.Conditions.Add(CreateTargetExistsCondition(target, false));
                var deadTarget = new TargetBrainStateAIBrainConditionData();
                SetField(deadTarget, "targetKey", target);
                SetField(deadTarget, "requiredStates", new List<TaxonomyTermData> { defeated });
                lost.Conditions.Add(deadTarget);
                Add(name, "CombatLinger", lost);
            }
            int attackRadius = attack.Requirements.OfType<SkillTargetDistanceRequirementData>().Single().Distance;
            Add("ChaseEnemy", "EngageEnemy", AtDistance(target, attackRadius, CompareOperation.LessOrEqual));
            Add("ChaseEnemy", "CombatLinger", CreateStateResultCondition(AIBrainStateResultMask.Fail | AIBrainStateResultMask.Interrupted), Delay(1));
            Add("EngageEnemy", "ChaseEnemy", AtDistance(target, attackRadius, CompareOperation.Greater));
            WriteIndependentWeaponNode(brain, attack,
                LoadRequired<TaxonomyTermData>(TaxonomyRoot + "/WeaponNode.asset"),
                LoadRequired<TaxonomyTermData>(TaxonomyRoot + "/WeaponTarget.asset"),
                LoadRequired<TaxonomyTermData>(TaxonomyRoot + "/WeaponReady.asset"),
                LoadRequired<TaxonomyTermData>(TaxonomyRoot + "/WeaponRetry.asset"),
                LoadRequired<TaxonomyTermData>(TaxonomyRoot + "/WeaponStopped.asset"), attackRadius);
            Add("FollowAnchor", "AcquireAnchor", Delay(checked((int)ContentDuration(profile.targetEvaluationInterval, step))));
            Add("CatchupAnchor", "AcquireAnchor", Delay(checked((int)ContentDuration(profile.targetEvaluationInterval, step))));
            Add("CombatLinger", "AcquireAnchor", Delay(checked((int)ContentDuration(profile.combatLingerTime, step))));
            Add("IdleAlly", "AcquireAnchor", Delay(checked((int)ContentDuration(profile.targetEvaluationInterval, step))));
            Add("ReturnAnchor", "AcquireAnchor", AtDistance(anchor, Distance(profile.followRadius), CompareOperation.Less));
            Add("RecoverLeash", "AcquireAnchor", Delay(checked((int)ContentDuration(profile.leashRecoveryTime, step))),
                AtDistance(anchor, Distance(profile.followRadius), CompareOperation.Less));
            foreach (string name in new[] { "ReturnAnchor", "RecoverLeash" })
                Add(name, "AcquireAnchor", CreateTargetExistsCondition(anchor, false));

            void Add(string from, string to, params AIBrainConditionData[] conditions) =>
                brain.Transitions.Add(CreateBrainTransition(tags[from], nodeId, tags[to], conditions));
            void AddMovement(string state, FrameworkSkillData skill, TaxonomyTermData key, AIBrainSkillTargetResolverData resolver = null)
            {
                var use = CreateUseSkillAction(skill, key, SkillCompletionPolicyType.OnExecutionComplete);
                SetField(use, "targetResolver", resolver);
                states[state].OnTickActions.Add(use);
                var stop = new InterruptSkillAIBrainExitActionData(); SetField(stop, "skill", skill);
                states[state].OnExitActions.Add(stop);
            }
            AIBrainRecentValueDecreaseData RecentDecrease(float duration)
            {
                var observation = new AIBrainRecentValueDecreaseData();
                SetField(observation, "value", LoadRequired<HostValueData>(HealthPath));
                SetField(observation, "duration", checked((int)ContentDuration(duration, step)));
                return observation;
            }
            void AddSupportSearch(string state, string found, string missing, int radius, TaxonomyTermData role, float response)
            {
                var clearSupport = new ClearAIBrainTargetActionData(); SetField(clearSupport, "targetKey", support);
                states[state].OnEnterActions.Add(clearSupport);
                var query = new SelectEntityTargetByQueryAIBrainActionData();
                SetField(query, "targetKey", support); SetField(query, "searchRadius", radius);
                SetField(query, "maxCandidates", 256);
                SetField(query, "requiredDisposition", DiplomacyDispositionType.Friendly);
                SetField(query, "requiredTargetTags", new List<TaxonomyTermData> { role });
                SetField(query, "blockedTargetStates", new List<TaxonomyTermData> { defeated });
                SetField(query, "targetGeometryType", WorldTargetGeometryType.Spatial);
                if (response > 0) SetField(query, "recentValueDecrease", RecentDecrease(response));
                else SetField(query, "requiredTargetStates", new List<TaxonomyTermData>(engaged));
                states[state].OnTickActions.Add(query);
                Add(state, found, CreateTargetExistsCondition(support, true));
                Add(state, missing, Result(), CreateTargetExistsCondition(support, false));
            }
        }

        static int Distance(float value) => checked((int)RoundContent(value * 1000));
        static AIBrainConditionData Result() => CreateStateResultCondition(
            AIBrainStateResultMask.Success | AIBrainStateResultMask.Fail | AIBrainStateResultMask.Interrupted);
        static TimeInStateAIBrainConditionData Delay(int duration)
        {
            var result = new TimeInStateAIBrainConditionData(); SetField(result, "minimumStateDuration", Math.Max(1, duration)); return result;
        }
        static TargetDistanceAIBrainConditionData AtDistance(TaxonomyTermData target, int distance, CompareOperation operation)
        {
            var result = new TargetDistanceAIBrainConditionData();
            SetField(result, "targetKey", target); SetField(result, "distance", distance);
            SetField(result, "compareOperation", operation); SetField(result, "targetGeometryType", WorldTargetGeometryType.Spatial);
            return result;
        }
    }
}
