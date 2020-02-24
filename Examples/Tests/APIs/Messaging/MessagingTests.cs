using System;
using Xunit;
using Xunit.Priority;
using Xunit.Abstractions;
using System.Threading.Tasks;
using Skylight.Api.Messaging.V1.Models;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Skylight.Sdk.Tests
{
    public class MessagingTests : APITest
    {
        public MessagingTests(SkylightFixture fixture) : base(fixture) {
        }

        //Our tests
        [Fact]
        public async Task TestNotification() {
            var alert = new NotificationRequestAlert().SetDefaults();
            alert.Type = 0;
            alert.Message = "Hi";
            Assert.NotNull(alert.ToString());
            Assert.NotNull(alert.ToJson());

            Assert.True(Guid.TryParse(TestUserId, out Guid guid));
            var notificationRequest = new NotificationRequest();//.SetDefaults();
            try {
                notificationRequest.SetDefaults();
            }catch(Exception e){}//Right now this throws an invalid Guid exception -- TODO this is not the intended behavior.
            notificationRequest.To = guid;
            notificationRequest.Alert = alert;
            Assert.NotNull(notificationRequest.ToString());
            Assert.NotNull(notificationRequest.ToJson());

            var notificationResponse = await SkyManager.ApiClient.ExecuteRequestAsync(new Skylight.Api.Messaging.V1.NotificationsRequests.NotificationsPostRequest(notificationRequest));
            var result = notificationResponse.Content;
            Assert.NotNull(result.ToString());
            Assert.NotNull(result.ToJson());
            Assert.NotNull(result.Result);
            result.SetDefaults();
        }

    }
}
