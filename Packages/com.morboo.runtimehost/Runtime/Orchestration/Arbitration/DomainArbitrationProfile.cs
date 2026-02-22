/// <summary>
/// Minimal domain arbitration metadata used by RuntimeHost to classify proposals
/// without hardcoding domain names in the arbiter proposal loop.
/// C04A bootstrap: extend incrementally as onboarding policy becomes data-driven.
/// </summary>
public readonly struct DomainArbitrationProfile
{
    public readonly bool StickyPrimary;

    public DomainArbitrationProfile(bool stickyPrimary)
    {
        StickyPrimary = stickyPrimary;
    }
}

/// <summary>
/// Optional domain metadata seam for arbiter-facing arbitration behavior.
/// Domains expose minimal profile data here instead of requiring host-runtime
/// hardcoded branching by domain name.
/// </summary>
public interface IDomainArbitrationProfileSource
{
    DomainArbitrationProfile GetArbitrationProfile();
}
