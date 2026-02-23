/// <summary>
/// C04A route onboarding seam: cached domain-owned contributor that can register
/// one or more execution routes into <see cref="ExecutionRouter"/> without editing
/// the router entrypoint or constructor.
/// </summary>
public interface IDomainExecutionRouteContributor
{
    void RegisterRoutes(ExecutionRouter router);
}

/// <summary>
/// RuntimeHost generic helpers for cached route-contributor patterns.
/// C04A: owns only route-registration composition helpers, not concrete domain route logic.
/// </summary>
public static class DomainExecutionRouteContributors
{
    public static IDomainExecutionRouteContributor CreateFixed(params ExecutionRouteRegistration[] registrations)
    {
        return registrations == null || registrations.Length == 0
            ? null
            : new FixedDomainExecutionRouteContributor(registrations, null);
    }

    public static IDomainExecutionRouteContributor CreateFixedWithUnknownRouteFallback(
        DomainExecutionRouteExecutor unknownRouteFallback,
        params ExecutionRouteRegistration[] registrations)
    {
        if (unknownRouteFallback == null && (registrations == null || registrations.Length == 0))
            return null;

        return new FixedDomainExecutionRouteContributor(registrations, unknownRouteFallback);
    }

    sealed class FixedDomainExecutionRouteContributor : IDomainExecutionRouteContributor
    {
        readonly ExecutionRouteRegistration[] _registrations;
        readonly DomainExecutionRouteExecutor _unknownRouteFallback;

        public FixedDomainExecutionRouteContributor(
            ExecutionRouteRegistration[] registrations,
            DomainExecutionRouteExecutor unknownRouteFallback)
        {
            _registrations = registrations;
            _unknownRouteFallback = unknownRouteFallback;
        }

        public void RegisterRoutes(ExecutionRouter router)
        {
            if (router == null)
                return;

            if (_unknownRouteFallback != null)
                router.RegisterUnknownRouteFallback(_unknownRouteFallback);

            if (_registrations == null)
                return;

            for (int i = 0; i < _registrations.Length; i++)
                router.RegisterRoute(_registrations[i]);
        }
    }
}
