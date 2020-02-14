using System;
using Xunit;

namespace Skylight.Sdk.Tests
{
    public class WorkflowTests : APITest
    {
        public WorkflowTests(SkylightFixture fixture) : base(fixture) {
        }

        [Fact]
        public void GenericTest() {
            Assert.True(this.fixture.test.Equals("lala"));
        }

    }
}
