using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Core.Activities;
using Core.Activities.Events;
using Core.Events;
using Core.GameRuntime;
using Core.Players;
using Core.Taxonomy;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace ChainRush.Tests.PlayMode
{
    public sealed class ChainRushActivityCompositionPlayModeTests : IPrebuildSetup, IPostBuildCleanup
    {
        const string PlayMainEditorPrefKey = "Game/Play Game";
        const string HadPlayMainEditorPrefSessionKey =
            "ChainRush.Tests.ActivityComposition.HadPlayMainEditorPref";
        const string PlayMainEditorPrefValueSessionKey =
            "ChainRush.Tests.ActivityComposition.PlayMainEditorPrefValue";
        const string RuntimeHostPrefabPath =
            "Assets/Game/Runtime/Host/ChainRushGameRuntimeHost.prefab";
        const string BoardActivationTermPath =
            "Assets/Game/Activities/Shared/Taxonomy/ChainRushBoardActivationTerm.asset";
        const string AutobattleActivityTypeId = "chainrush.activity-type.autobattle";
        const string BoardActivityTypeId = "chainrush.activity-type.board";
        const float StartupTimeoutSeconds = 10f;

        public void Setup()
        {
            bool hadPreference = EditorPrefs.HasKey(PlayMainEditorPrefKey);
            SessionState.SetBool(HadPlayMainEditorPrefSessionKey, hadPreference);
            SessionState.SetBool(
                PlayMainEditorPrefValueSessionKey,
                hadPreference && EditorPrefs.GetBool(PlayMainEditorPrefKey));
            EditorPrefs.SetBool(PlayMainEditorPrefKey, false);
        }

        public void Cleanup()
        {
            if (SessionState.GetBool(HadPlayMainEditorPrefSessionKey, false))
            {
                EditorPrefs.SetBool(
                    PlayMainEditorPrefKey,
                    SessionState.GetBool(PlayMainEditorPrefValueSessionKey, false));
            }
            else
            {
                EditorPrefs.DeleteKey(PlayMainEditorPrefKey);
            }

            SessionState.EraseBool(HadPlayMainEditorPrefSessionKey);
            SessionState.EraseBool(PlayMainEditorPrefValueSessionKey);
        }

        [UnityTest]
        public IEnumerator RuntimeComposition_LaunchesBoardOnceAndParentCloseClosesIt()
        {
            GameObject hostPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RuntimeHostPrefabPath);
            Assert.NotNull(hostPrefab, "Runtime host prefab is missing.");
            GameObject hostInstance = Object.Instantiate(hostPrefab);
            GameRuntimeHost host = hostInstance.GetComponent<GameRuntimeHost>();
            Assert.NotNull(host, "Runtime host prefab does not contain GameRuntimeHost.");

            float startupDeadline = Time.realtimeSinceStartup + StartupTimeoutSeconds;
            while (Time.realtimeSinceStartup < startupDeadline)
            {
                if (host.RuntimeContext != null
                    && host.RuntimeContext.IsInitialized
                    && TryFindRunningActivities(out _, out _))
                {
                    break;
                }

                yield return null;
            }

            Assert.IsTrue(host.RuntimeContext != null && host.RuntimeContext.IsInitialized);
            Assert.IsTrue(
                TryFindRunningActivities(out ActivityRuntimeSnapshot autobattle, out ActivityRuntimeSnapshot board),
                "Autobattle and Board did not both reach Running state.");

            Assert.IsFalse(autobattle.ParentActivityId.IsValid);
            Assert.AreEqual(2, autobattle.Participants.Count);
            AssertParticipant(autobattle.Participants, 0, PlayerControlType.LocalHuman);
            AssertParticipant(autobattle.Participants, 1, PlayerControlType.Bot);

            Assert.AreEqual(autobattle.Id, board.ParentActivityId);
            Assert.AreEqual(1, board.Participants.Count);
            AssertParticipant(board.Participants, 0, PlayerControlType.LocalHuman);
            Assert.AreEqual(0, board.Objectives.Count);
            CollectionAssert.AreEqual(
                new List<ActivityId> { board.Id },
                ActivityService.GetChildActivityIds(autobattle.Id));

            TaxonomyTermData activation =
                AssetDatabase.LoadAssetAtPath<TaxonomyTermData>(BoardActivationTermPath);
            Assert.NotNull(activation);
            EventBus.Trigger(new ActivityChildActivationEvent(
                autobattle.Id,
                new List<TaxonomyTermData> { activation }));
            yield return null;
            Assert.AreEqual(1, ActivityService.GetChildActivityIds(autobattle.Id).Count);

            Assert.IsTrue(ActivityService.Close(autobattle.Id, ActivityCloseCauseType.Manual));
            Assert.IsTrue(ActivityService.TryGetSnapshot(board.Id, out board));
            Assert.AreEqual(ActivityState.Closed, board.State);
            Assert.AreEqual(ActivityCloseCauseType.ParentClosed, board.CloseCauseType);
            Assert.AreEqual(ActivityResultType.Cancelled, board.ResultType);
        }

        static bool TryFindRunningActivities(
            out ActivityRuntimeSnapshot autobattle,
            out ActivityRuntimeSnapshot board)
        {
            autobattle = default;
            board = default;
            bool hasAutobattle = false;
            bool hasBoard = false;
            List<ActivityRuntimeSnapshot> snapshots = ActivityService.GetAll();
            for (int i = 0; i < snapshots.Count; i++)
            {
                ActivityRuntimeSnapshot snapshot = snapshots[i];
                string activityTypeId = snapshot.ActivityType == null
                    ? null
                    : snapshot.ActivityType.Id;
                if (activityTypeId == AutobattleActivityTypeId)
                {
                    autobattle = snapshot;
                    hasAutobattle = snapshot.State == ActivityState.Running;
                }
                else if (activityTypeId == BoardActivityTypeId)
                {
                    board = snapshot;
                    hasBoard = snapshot.State == ActivityState.Running;
                }
            }

            return hasAutobattle && hasBoard;
        }

        static void AssertParticipant(
            IReadOnlyList<ActivityParticipantBinding> participants,
            int teamIndex,
            PlayerControlType controlType)
        {
            ActivityParticipantBinding participant =
                participants.Single(binding => binding.TeamIndex == teamIndex);
            Assert.AreEqual(controlType, participant.ControlType);
        }
    }
}
