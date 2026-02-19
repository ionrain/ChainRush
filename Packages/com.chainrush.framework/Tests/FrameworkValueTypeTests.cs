using System;
using System.Collections.Generic;
using NUnit.Framework;

/// <summary>
/// Unit tests for Framework value types: Float2, AABB2D, EntityId, RoleId,
/// WorldSnapshot, ArbiterDecision, TickContext.
/// </summary>
public sealed class FrameworkValueTypeTests
{
    // ──────────────────────────────────────────────────────────────────
    //  Float2
    // ──────────────────────────────────────────────────────────────────

    [Test]
    public void Float2_Arithmetic_Add()
    {
        Float2 a = new Float2(1f, 2f);
        Float2 b = new Float2(3f, 4f);
        Float2 result = a + b;
        Assert.That(result.X, Is.EqualTo(4f));
        Assert.That(result.Y, Is.EqualTo(6f));
    }

    [Test]
    public void Float2_Arithmetic_Subtract()
    {
        Float2 a = new Float2(5f, 3f);
        Float2 b = new Float2(2f, 1f);
        Float2 result = a - b;
        Assert.That(result.X, Is.EqualTo(3f));
        Assert.That(result.Y, Is.EqualTo(2f));
    }

    [Test]
    public void Float2_Arithmetic_UnaryNegate()
    {
        Float2 a = new Float2(3f, -2f);
        Float2 result = -a;
        Assert.That(result.X, Is.EqualTo(-3f));
        Assert.That(result.Y, Is.EqualTo(2f));
    }

    [Test]
    public void Float2_Arithmetic_ScalarMultiply()
    {
        Float2 a = new Float2(2f, 3f);
        Float2 r1 = a * 2f;
        Float2 r2 = 2f * a;
        Assert.That(r1, Is.EqualTo(r2));
        Assert.That(r1.X, Is.EqualTo(4f));
        Assert.That(r1.Y, Is.EqualTo(6f));
    }

    [Test]
    public void Float2_Arithmetic_ScalarDivide()
    {
        Float2 a = new Float2(6f, 4f);
        Float2 result = a / 2f;
        Assert.That(result.X, Is.EqualTo(3f));
        Assert.That(result.Y, Is.EqualTo(2f));
    }

    [Test]
    public void Float2_SqrMagnitude()
    {
        Float2 a = new Float2(3f, 4f);
        Assert.That(a.SqrMagnitude, Is.EqualTo(25f));
    }

    [Test]
    public void Float2_Magnitude()
    {
        Float2 a = new Float2(3f, 4f);
        Assert.That(a.Magnitude, Is.EqualTo(5f).Within(1e-6f));
    }

    [Test]
    public void Float2_Distance()
    {
        Float2 a = new Float2(0f, 0f);
        Float2 b = new Float2(3f, 4f);
        Assert.That(Float2.Distance(a, b), Is.EqualTo(5f).Within(1e-6f));
    }

    [Test]
    public void Float2_DistanceSqr()
    {
        Float2 a = new Float2(1f, 1f);
        Float2 b = new Float2(4f, 5f);
        Assert.That(Float2.DistanceSqr(a, b), Is.EqualTo(25f));
    }

    [Test]
    public void Float2_Dot()
    {
        Float2 a = new Float2(1f, 0f);
        Float2 b = new Float2(0f, 1f);
        Assert.That(Float2.Dot(a, b), Is.EqualTo(0f));

        Float2 c = new Float2(2f, 3f);
        Float2 d = new Float2(4f, 5f);
        Assert.That(Float2.Dot(c, d), Is.EqualTo(23f));
    }

    [Test]
    public void Float2_Normalized()
    {
        Float2 a = new Float2(3f, 4f);
        Float2 n = a.Normalized;
        Assert.That(n.Magnitude, Is.EqualTo(1f).Within(1e-6f));
        Assert.That(n.X, Is.EqualTo(0.6f).Within(1e-6f));
        Assert.That(n.Y, Is.EqualTo(0.8f).Within(1e-6f));
    }

    [Test]
    public void Float2_Normalized_Zero_ReturnsZero()
    {
        Float2 zero = Float2.Zero;
        Assert.That(zero.Normalized, Is.EqualTo(Float2.Zero));
    }

    [Test]
    public void Float2_Equality()
    {
        Float2 a = new Float2(1f, 2f);
        Float2 b = new Float2(1f, 2f);
        Float2 c = new Float2(1f, 3f);

        Assert.That(a == b, Is.True);
        Assert.That(a != c, Is.True);
        Assert.That(a.Equals(b), Is.True);
        Assert.That(a.Equals((object)b), Is.True);
    }

    [Test]
    public void Float2_HashCode_EqualValuesMatch()
    {
        Float2 a = new Float2(1f, 2f);
        Float2 b = new Float2(1f, 2f);
        Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
    }

    [Test]
    public void Float2_Zero_And_One()
    {
        Assert.That(Float2.Zero.X, Is.EqualTo(0f));
        Assert.That(Float2.Zero.Y, Is.EqualTo(0f));
        Assert.That(Float2.One.X, Is.EqualTo(1f));
        Assert.That(Float2.One.Y, Is.EqualTo(1f));
    }

    // ──────────────────────────────────────────────────────────────────
    //  AABB2D
    // ──────────────────────────────────────────────────────────────────

    [Test]
    public void AABB2D_Contains_PointInside()
    {
        AABB2D box = new AABB2D(new Float2(0f, 0f), new Float2(10f, 10f));
        Assert.That(box.Contains(new Float2(5f, 5f)), Is.True);
    }

    [Test]
    public void AABB2D_Contains_PointOutside()
    {
        AABB2D box = new AABB2D(new Float2(0f, 0f), new Float2(10f, 10f));
        Assert.That(box.Contains(new Float2(11f, 5f)), Is.False);
        Assert.That(box.Contains(new Float2(-1f, 5f)), Is.False);
    }

    [Test]
    public void AABB2D_Contains_PointOnEdge()
    {
        AABB2D box = new AABB2D(new Float2(0f, 0f), new Float2(10f, 10f));
        Assert.That(box.Contains(new Float2(0f, 0f)), Is.True);
        Assert.That(box.Contains(new Float2(10f, 10f)), Is.True);
    }

    [Test]
    public void AABB2D_FromCenterSize()
    {
        AABB2D box = AABB2D.FromCenterSize(new Float2(5f, 5f), new Float2(10f, 10f));
        Assert.That(box.Min.X, Is.EqualTo(0f));
        Assert.That(box.Min.Y, Is.EqualTo(0f));
        Assert.That(box.Max.X, Is.EqualTo(10f));
        Assert.That(box.Max.Y, Is.EqualTo(10f));
    }

    [Test]
    public void AABB2D_Center_Size_Extents()
    {
        AABB2D box = new AABB2D(new Float2(2f, 4f), new Float2(8f, 10f));
        Assert.That(box.Center.X, Is.EqualTo(5f));
        Assert.That(box.Center.Y, Is.EqualTo(7f));
        Assert.That(box.Size.X, Is.EqualTo(6f));
        Assert.That(box.Size.Y, Is.EqualTo(6f));
        Assert.That(box.Extents.X, Is.EqualTo(3f));
        Assert.That(box.Extents.Y, Is.EqualTo(3f));
    }

    // ──────────────────────────────────────────────────────────────────
    //  EntityId
    // ──────────────────────────────────────────────────────────────────

    [Test]
    public void EntityId_None_IsDefault()
    {
        Assert.That(EntityId.None.IsNone, Is.True);
        Assert.That(EntityId.None.ToStableInt(), Is.EqualTo(0));
        Assert.That(default(EntityId).IsNone, Is.True);
    }

    [Test]
    public void EntityId_Equality()
    {
        EntityId a = new EntityId(42);
        EntityId b = new EntityId(42);
        EntityId c = new EntityId(99);

        Assert.That(a == b, Is.True);
        Assert.That(a != c, Is.True);
        Assert.That(a.Equals(b), Is.True);
        Assert.That(a.Equals((object)b), Is.True);
        Assert.That(a.Equals(c), Is.False);
    }

    [Test]
    public void EntityId_HashCode_EqualValuesMatch()
    {
        EntityId a = new EntityId(42);
        EntityId b = new EntityId(42);
        Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
    }

    [Test]
    public void EntityId_ToStableInt()
    {
        EntityId id = new EntityId(123);
        Assert.That(id.ToStableInt(), Is.EqualTo(123));
        Assert.That(id.IsNone, Is.False);
    }

    [Test]
    public void EntityId_DictionaryKey()
    {
        var dict = new Dictionary<EntityId, string>();
        EntityId a = new EntityId(1);
        EntityId b = new EntityId(2);
        dict[a] = "alpha";
        dict[b] = "beta";

        Assert.That(dict[new EntityId(1)], Is.EqualTo("alpha"));
        Assert.That(dict[new EntityId(2)], Is.EqualTo("beta"));
    }

    // ──────────────────────────────────────────────────────────────────
    //  RoleId
    // ──────────────────────────────────────────────────────────────────

    [Test]
    public void RoleId_None_IsDefault()
    {
        Assert.That(RoleId.None.IsNone, Is.True);
        Assert.That(RoleId.None.ToStableInt(), Is.EqualTo(0));
        Assert.That(default(RoleId).IsNone, Is.True);
    }

    [Test]
    public void RoleId_Equality()
    {
        RoleId a = new RoleId(10);
        RoleId b = new RoleId(10);
        RoleId c = new RoleId(20);

        Assert.That(a == b, Is.True);
        Assert.That(a != c, Is.True);
        Assert.That(a.Equals(b), Is.True);
        Assert.That(a.Equals(c), Is.False);
    }

    [Test]
    public void RoleId_ToStableInt()
    {
        RoleId id = new RoleId(55);
        Assert.That(id.ToStableInt(), Is.EqualTo(55));
        Assert.That(id.IsNone, Is.False);
    }

    // ──────────────────────────────────────────────────────────────────
    //  WorldSnapshot
    // ──────────────────────────────────────────────────────────────────

    [Test]
    public void WorldSnapshot_CarriesValues()
    {
        Float2 anchor = new Float2(1f, 2f);
        WorldSnapshot snap = new WorldSnapshot(anchor, 10.5f, 42);

        Assert.That(snap.Anchor, Is.EqualTo(anchor));
        Assert.That(snap.Now, Is.EqualTo(10.5f));
        Assert.That(snap.ActorCount, Is.EqualTo(42));
    }

    // ──────────────────────────────────────────────────────────────────
    //  ArbiterDecision
    // ──────────────────────────────────────────────────────────────────

    [Test]
    public void ArbiterDecision_DefaultIsNone()
    {
        ArbiterDecision d = default;
        Assert.That(d.DomainKey, Is.EqualTo(0));
        Assert.That(d.ProposalKey, Is.EqualTo(0));
        Assert.That(d.ModeChanged, Is.False);
    }

    [Test]
    public void ArbiterDecision_CarriesValues()
    {
        ArbiterDecision d = new ArbiterDecision
        {
            DomainKey = 1,
            ProposalKey = 2,
            ModeChanged = true
        };
        Assert.That(d.DomainKey, Is.EqualTo(1));
        Assert.That(d.ProposalKey, Is.EqualTo(2));
        Assert.That(d.ModeChanged, Is.True);
    }

    // ──────────────────────────────────────────────────────────────────
    //  TickContext
    // ──────────────────────────────────────────────────────────────────

    [Test]
    public void TickContext_CarriesValues()
    {
        TickContext ctx = new TickContext(1.5f, 0.25f, 7);
        Assert.That(ctx.Now, Is.EqualTo(1.5f));
        Assert.That(ctx.DeltaTime, Is.EqualTo(0.25f));
        Assert.That(ctx.TickIndex, Is.EqualTo(7));
    }
}
