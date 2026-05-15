// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using Xunit;

namespace MMP.Herald.Tests.Helpers;

/// <summary>
/// Marker collection for any test class that registers or unregisters
/// kinds on <see cref="MMP.Herald.Quick.HeraldHost.Default"/> through the
/// static <c>EnricherJsonRegistry</c>, <c>DecoratorJsonRegistry</c>, or
/// <c>HeraldRegistry</c> facades.
///
/// <para>
/// xUnit runs tests in DIFFERENT collections in parallel by default. Two
/// parallel test classes that both register custom kinds on the default
/// host through the static facades can step on each other's
/// try/finally Unregister cleanup. Joining a single collection serialises
/// these tests at the class level, removing the cross-class race without
/// disabling parallelism for the rest of the suite.
/// </para>
///
/// <para>
/// Tests that exercise instance APIs only (constructing their own
/// <c>HeraldHost</c>) do not need this collection — instance state is
/// already isolated.
/// </para>
/// </summary>
[CollectionDefinition(Name)]
public sealed class DefaultHostCollection : ICollectionFixture<DefaultHostCollection>
{
    public const string Name = "DefaultHost";
}
