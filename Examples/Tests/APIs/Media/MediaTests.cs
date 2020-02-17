using System;
using Xunit;
using Xunit.Priority;
using Xunit.Abstractions;
using System.Threading.Tasks;
using Skylight.Api.Workflow.V2.WorkflowRequests;
using Skylight.Api.Workflow.V2.Models;
using Skylight.Api.Assignments.V1.Models;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Skylight.Sdk.Tests
{
    public class MediaTests : APITest
    {
        public MediaTests(SkylightFixture fixture) : base(fixture) {
        }

        //Our tests
        [Fact]
        public async Task QueryMedia( ) {
            Assert.True(true);
        }

    }
}
