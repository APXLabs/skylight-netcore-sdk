using System.Net;
using System.IO;
using System;
using Xunit;
using Skylight.Sdk;
using System.Threading.Tasks;
using Skylight.Api.Authentication.V1.UsersRequests;

namespace Skylight.Sdk.Tests
{
    public class SkylightFixture : IAsyncLifetime {
        public static Manager SkyManager;
        public static string TestUserId;

        public SkylightFixture() {
            SkyManager = new Manager(Path.Join("..", "..", "..", "credentials.json"));
        } 
        
        public async Task InitializeAsync()
        {
            await SkyManager.Connect();
            //Create a default user for our tests to use
            var userNew = new Skylight.Api.Authentication.V1.Models.UserNew() {
                Username = "sdk.test.user." + Guid.NewGuid().ToString()
                , Password = "password"
            };
            var userResponse = await SkyManager.ApiClient.ExecuteRequestAsync(new CreateUserRequest(userNew));
            TestUserId = userResponse.Content.Id;
            

        }

        public async Task DisposeAsync()
        {
            //Delete our default user
            await SkyManager.ApiClient.ExecuteRequestAsync(new DeleteUserRequest(TestUserId));
        }
    }

    [CollectionDefinition("Skylight collection")]
    public class SkylightCollectionFixture : ICollectionFixture<SkylightFixture> {
        // This class has no code, and is never created. Its purpose is simply
        // to be the place to apply [CollectionDefinition] and all the
        // ICollectionFixture<> interfaces.
    }

    [Collection("Skylight collection")]
    public abstract class APITest
    {
        protected SkylightFixture fixture;
        public APITest(SkylightFixture fixture) {
            this.fixture = fixture;
        }

        protected static Manager SkyManager {
            get {
                return SkylightFixture.SkyManager;
            }
        }

        protected static string TestUserId {
            get {
                return SkylightFixture.TestUserId;
            }
        }

    }
}
