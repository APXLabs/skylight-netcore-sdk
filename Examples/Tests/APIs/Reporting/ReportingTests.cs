using System;
using Xunit;
using Xunit.Priority;
using Xunit.Abstractions;
using System.Threading.Tasks;
using Skylight.Api.Reporting.V1.APIRequests;
using Skylight.Api.Reporting.V1.Models;
using Skylight.Api.Reporting.V1.ReportingRequests;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Skylight.Sdk.Tests
{
    public class ReportingTests : APITest
    {
        public ReportingTests(SkylightFixture fixture) : base(fixture) {
        }

        //Our tests
        [Fact]
        public async Task TestGetApi() {
            await SkyManager.ApiClient.ExecuteRequestAsync(new GetApiRequest());
        }

        [Fact]
        public async Task TestGetEvents() {
            var getEventsRequest = new GetEventsRequest();
            getEventsRequest.AddFromQuery(DateTime.MinValue.ToString("yyyy-MM-dd'T'HH:mm:ss.fffzzz"));
            getEventsRequest.AddToQuery(DateTime.MaxValue.ToString("yyyy-MM-dd'T'HH:mm:ss.fffzzz"));
            getEventsRequest.AddLimitQuery(100);
            getEventsRequest.AddNamesQuery("user-login");
            getEventsRequest.AddPageQuery(0);
            getEventsRequest.AddSortQuery("timestamp");
            getEventsRequest.AddStartQuery(0);
            

            var getEventsResponse = await SkyManager.ApiClient.ExecuteRequestAsync(getEventsRequest);
    
            Assert.InRange(getEventsResponse.Content.Size.Value, 0, 100);
        }

    }
}
