// Copyright (c) 2026 MMPWorks LLC
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.
#nullable enable

using MMP.Herald.Levels;

namespace MMP.Herald.Tests.Helpers;

/// <summary>
/// Creates pre-configured ILogLevelRegistry instances for tests.
/// </summary>
public static class RegistryHelper
{
    /// <summary>
    /// Creates the full default registry with all known levels registered.
    /// </summary>
    public static ILogLevelRegistry CreateDefault()
    {
        return new DefaultLogLevelRegistryFactory().Create();
    }

    /// <summary>
    /// Creates a minimal registry with only the 5 base levels (Trace, Debug, Info, Warn, Error).
    /// </summary>
    public static ILogLevelRegistry CreateMinimal()
    {
        return LogLevelRegistry.CreateDefault();
    }
}
