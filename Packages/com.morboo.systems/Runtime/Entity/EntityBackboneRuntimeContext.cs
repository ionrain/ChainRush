using System;

/// <summary>
/// Runtime context for the currently active entity backbone services.
/// Bridge/experience layer installs the concrete in-memory backbone, while
/// package runtime code reads it through stable contracts.
/// </summary>
public static class EntityBackboneRuntimeContext
{
    public static IEntityRegistry Registry { get; private set; }
    public static IEntityFactory Factory { get; private set; }
    public static IEntityLifecycleService Lifecycle { get; private set; }
    public static IEntityViewBinder ViewBinder { get; private set; }
    public static IEntityStateQuery StateQuery => Registry;
    public static bool IsInstalled => Registry != null && Factory != null && Lifecycle != null && ViewBinder != null;

    public static void Install(
        IEntityRegistry registry,
        IEntityFactory factory,
        IEntityLifecycleService lifecycle,
        IEntityViewBinder viewBinder)
    {
        Registry = registry ?? throw new ArgumentNullException(nameof(registry));
        Factory = factory ?? throw new ArgumentNullException(nameof(factory));
        Lifecycle = lifecycle ?? throw new ArgumentNullException(nameof(lifecycle));
        ViewBinder = viewBinder ?? throw new ArgumentNullException(nameof(viewBinder));
    }

    public static void Clear(IEntityRegistry expectedRegistry = null)
    {
        if (expectedRegistry != null && !ReferenceEquals(Registry, expectedRegistry))
            return;

        Registry = null;
        Factory = null;
        Lifecycle = null;
        ViewBinder = null;
    }
}
