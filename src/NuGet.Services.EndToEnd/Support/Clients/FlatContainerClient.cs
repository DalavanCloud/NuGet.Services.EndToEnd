﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Services.EndToEnd.Support
{
    /// <summary>
    /// A simple interface for interacting with flat container.
    /// </summary>
    public class FlatContainerClient
    {
        private readonly SimpleHttpClient _httpClient;
        private readonly V3IndexClient _v3IndexClient;

        public FlatContainerClient(SimpleHttpClient httpClient, V3IndexClient v3IndexClient)
        {
            _httpClient = httpClient;
            _v3IndexClient = v3IndexClient;
        }

        /// <summary>
        /// Polls all flat container base URLs until the provided ID and version are available.
        /// </summary>
        /// <param name="id">The package ID.</param>
        /// <param name="version">The package version.</param>
        /// <param name="logger">The logger.</param>
        /// <returns>Returns a task that completes when the package is available or the timeout has occurred.</returns>
        public Task WaitForPackageAsync(string id, string version, ITestOutputHelper logger)
        {
            // Retry for connection problem, timeout, or HTTP 5XX. We are okay with retrying on 5XX in this case because
            // the origin server is blob storage. If they are having some internal server error, there's not much we can
            // do.
            return RetryUtility.ExecuteWithRetry(
                async () =>
                {
                    var baseUrls = await _v3IndexClient.GetFlatContainerBaseUrlsAsync(logger);

                    Assert.True(baseUrls.Count > 0, "At least one flat container base URL must be configured.");

                    logger.WriteLine(
                        $"Waiting for package {id} {version} to be available on flat container base URLs:" + Environment.NewLine +
                        string.Join(Environment.NewLine, baseUrls.Select(u => $" - {u}")));

                    var tasks = baseUrls
                        .Select(u => PollAsync(u, id, version, logger))
                        .ToList();

                    await Task.WhenAll(tasks);
                },
                ex => ex.HasTypeOrInnerType<SocketException>()
                   || ex.HasTypeOrInnerType<TaskCanceledException>()
                   || (ex.HasTypeOrInnerType<HttpRequestMessageException>(out var hre)
                       && (hre.StatusCode == HttpStatusCode.InternalServerError
                           || hre.StatusCode == HttpStatusCode.BadGateway
                           || hre.StatusCode == HttpStatusCode.ServiceUnavailable
                           || hre.StatusCode == HttpStatusCode.GatewayTimeout)),
                logger: logger);

        }

        private async Task PollAsync(string baseUrl, string id, string version, ITestOutputHelper logger)
        {
            await Task.Yield();

            var url = $"{baseUrl}/{id.ToLowerInvariant()}/index.json";

            var duration = Stopwatch.StartNew();
            var found = false;
            do
            {
                var response = await _httpClient.GetJsonAsync<FlatContainerIndexResponse>(
                    url,
                    allowNotFound: true,
                    logResponseBody: false,
                    logger: logger);

                if (response != null)
                {
                    found = response.Versions.Contains(version.ToLowerInvariant());
                }

                if (!found && duration.Elapsed + TestData.V3SleepDuration < TestData.FlatContainerWaitDuration)
                {
                    await Task.Delay(TestData.V3SleepDuration);
                }
            }
            while (!found && duration.Elapsed < TestData.FlatContainerWaitDuration);

            Assert.True(found, $"Package {id} {version} was not found on {url} after waiting {TestData.FlatContainerWaitDuration}.");
            logger.WriteLine($"Package {id} {version} was found on {url} after waiting {duration.Elapsed}.");
        }

        private class FlatContainerIndexResponse
        {
            public List<string> Versions { get; set; }
        }
    }
}
