﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace NuGet.Services.EndToEnd.Support
{
    /// <summary>
    /// This code duplicates some existing infrastructure in gallery's functional test suite. It should be
    /// consolidated at some point.
    /// https://github.com/NuGet/NuGetGallery/blob/master/tests/NuGetGallery.FunctionalTests.Core/EnvironmentSettings.cs
    /// </summary>
    public static class EnvironmentSettings
    {
        private static EnvironmentVariableTarget[] Targets = new[]
        {
            EnvironmentVariableTarget.Process,
            EnvironmentVariableTarget.User,
            EnvironmentVariableTarget.Machine
        };

        public static string ConfigurationName => GetEnvironmentVariable("ConfigurationName", required: true);

        private static string GetEnvironmentVariable(string key, bool required)
        {
            var output = Targets
                .Select(target => Environment.GetEnvironmentVariable(key, target))
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .FirstOrDefault();

            if (required && output == null)
            {
                throw new ArgumentException($"The environment variable '{key}' is not defined.");
            }

            return output;
        }
    }
}
