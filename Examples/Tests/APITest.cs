using System;
using Xunit;

namespace Skylight.Sdk.Tests
{
    public class SkylightFixture : IDisposable {
        public string test = "lala";
        public SkylightFixture() {
        }
        public void Dispose(){
        }
    }

    public abstract class APITest : IClassFixture<SkylightFixture>
    {
        protected SkylightFixture fixture;
        public APITest(SkylightFixture fixture) {
            this.fixture = fixture;
        }
    }
}
