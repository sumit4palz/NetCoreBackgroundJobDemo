﻿using Microsoft.AspNetCore.Http;
using Microsoft.eShopOnContainers.BuildingBlocks.Resilience.Http;
using Microsoft.Extensions.Logging;
using Polly;
using System;
using System.Diagnostics;
using System.Net.Http;

namespace NetCoreBackgroundJobDemo.Infastructure
{
    public class ResilientHttpClientFactory : IResilientHttpClientFactory
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<ResilientHttpClient> _logger;
        private readonly int _exceptionsAllowedBeforeBreaking;
        private readonly int _retryCount;

        public ResilientHttpClientFactory(ILogger<ResilientHttpClient> logger,
            IHttpContextAccessor httpContextAccessor, int exceptionsAllowedBeforeBreaking = 5,
            int retryCount = 6)
        {
            _logger = logger;
            _exceptionsAllowedBeforeBreaking = exceptionsAllowedBeforeBreaking;
            _retryCount = retryCount;
            _httpContextAccessor = httpContextAccessor;
        }

        public ResilientHttpClient CreateResilientHttpClient()
            => new ResilientHttpClient((origin) => CreatePolicies(), _logger, _httpContextAccessor);

        private Policy[] CreatePolicies()
            => new Policy[]
            {
                Policy.Handle<HttpRequestException>()
                .WaitAndRetryAsync(
                    // number of retries
                    _retryCount,
                    // exponential backofff
                    retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    // on retry
                    (exception, timeSpan, retryCount, context) =>
                    {
                        var msg = $"Retry {retryCount} implemented with Polly's RetryPolicy " +
                            $"of {context.PolicyKey} " +
                            $"at {context.ExecutionKey}, " +
                            $"due to: {exception}.";
                        _logger.LogWarning(msg);
                        _logger.LogDebug(msg);
                    }),

                 //TODO : We need to decide if we need a CircuitBreaker

                Policy.Handle<HttpRequestException>()
                .CircuitBreakerAsync(
                   // number of exceptions before breaking circuit
                   _exceptionsAllowedBeforeBreaking,
                   // time circuit opened before retry
                   TimeSpan.FromMinutes(1),
                   (exception, duration) =>
                   {
                        // on circuit opened
                        _logger.LogTrace("Circuit breaker opened");
                   },
                   () =>
                   {
                        // on circuit closed
                        _logger.LogTrace("Circuit breaker reset");
                   })
            };
    }
}