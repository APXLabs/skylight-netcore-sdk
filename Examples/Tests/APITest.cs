using System.IO;
using System;
using Xunit;
using Skylight.Sdk;
using System.Threading.Tasks;

namespace Skylight.Sdk.Tests
{
    public class SkylightFixture : IAsyncLifetime {
        public Manager SkyManager;
        public SkylightFixture() {
            SkyManager = new Manager(Path.Join("..", "..", "..", "credentials.json"));
        } 
        
        public async Task InitializeAsync()
        {
            await SkyManager.Connect();
        }

        public Task DisposeAsync()
        {
            return Task.CompletedTask;
        }
    }

    public abstract class APITest : IClassFixture<SkylightFixture>
    {
        protected SkylightFixture fixture;
        public APITest(SkylightFixture fixture) {
            this.fixture = fixture;
        }

        protected Manager SkyManager {
            get {
                return this.fixture.SkyManager;
            }
        }
    }
}
