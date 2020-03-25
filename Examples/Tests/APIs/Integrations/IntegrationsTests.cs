using System.Net;
using System.IO;
using System;
using Xunit;
using Xunit.Priority;
using Xunit.Abstractions;
using System.Threading.Tasks;
using Skylight.Api.Integrations.V1.IntegrationsRequests;
using Skylight.Api.Integrations.V1.Models;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Skylight.Sdk.Tests
{
    public class IntegrationsTests : APITest
    {
        public IntegrationsTests(SkylightFixture fixture) : base(fixture) {
        }

        //Our tests
        [Fact]
        public async Task TestGetIntegrations() {
            
            var getIntegrationsRequest = new GetIntegrationsRequest();
            
            var getIntegrationsResponse = await SkyManager.ApiClient.ExecuteRequestAsync(getIntegrationsRequest);
            
            Assert.InRange(getIntegrationsResponse.Content.Count, 0, int.MaxValue);
        }

    }
}
