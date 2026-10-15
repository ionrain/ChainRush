#if UNITY_EDITOR
using Core.Activities;
using Core.CapabilityHosts;
using Core.Economy;
using Core.HostValues;
using Core.Taxonomy;
using Core.World;
using UnityEngine;

namespace ChainRush.Diagnostics
{
    public sealed class PerformanceBenchAssets : ScriptableObject
    {
        public ActivityData straightActivity;
        public ActivityData navigationActivity;
        public CapabilityHostData staticObject;
        public CapabilityHostData movingObject;
        public CapabilityHostData obstacleObject;
        public EconomyWalletData worldWallet;
        public HostValueData speedValue;
        public TaxonomyTermData controlNode;
        public TaxonomyTermData moveState;
        public MovementTrafficProfileData trafficProfile;
        public NavigationWorkBudgetData navigationBudget;
        public GameObject projectionPrefab;
        public float tickDelta = 1f / 30;
    }
}
#endif
