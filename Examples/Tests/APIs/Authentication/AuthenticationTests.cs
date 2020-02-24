using System;
using Xunit;
using Xunit.Priority;
using Xunit.Abstractions;
using System.Threading.Tasks;
using Skylight.Api.Authentication.V1;
using Skylight.Api.Authentication.V1.Models;
using Skylight.Api.Authentication.V1.GroupsRequests;
using Skylight.Api.Authentication.V1.UsersRequests;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Skylight.Sdk.Tests
{
    public class AuthenticationTests : APITest
    {
        public AuthenticationTests(SkylightFixture fixture) : base(fixture) {
        }

        //Our tests
        [Fact]
        public async Task TestCreateAndDeleteGroup() {
            var groupNew = new GroupNew().SetDefaults();
            groupNew.Name = "api.test.group." + new Guid().ToString();
            groupNew.Description = "This is a test group for the API tests.";

            Assert.NotNull(groupNew.ToJson());
            Assert.NotNull(groupNew.ToString());

            var createGroupRequest = new CreateGroupRequest(groupNew);
            var createGroupResponse = await SkyManager.ApiClient.ExecuteRequestAsync(createGroupRequest);
            var groupId = createGroupResponse.Content.Id;

            var groups = await SkyManager.ApiClient.ExecuteRequestAsync(new GetGroupsRequest());
            Assert.InRange(groups.Content.Count, 1, int.MaxValue);

            var deleteGroupRequest = new DeleteGroupRequest(groupId);
            await SkyManager.ApiClient.ExecuteRequestAsync(deleteGroupRequest);
        }

        [Fact]
        public async Task TestAssignAndUnassignGroup() {
            var groupNew = new GroupNew().SetDefaults();
            groupNew.Name = "api.test.group." + new Guid().ToString();
            groupNew.Description = "This is a test group for the API tests.";

            var createGroupRequest = new CreateGroupRequest(groupNew);
            var createGroupResponse = await SkyManager.ApiClient.ExecuteRequestAsync(createGroupRequest);
            var groupId = createGroupResponse.Content.Id;

            await SkyManager.ApiClient.ExecuteRequestAsync(new Skylight.Api.Authentication.V1.GroupsRequests.AssignUserGroupRequest(TestUserId, groupId));
            var group = await SkyManager.ApiClient.ExecuteRequestAsync(new GetGroupRequest(groupId));
            Assert.Single(group.Content.Members.FindAll(m => { return m.Id.Equals(TestUserId);}));
            await SkyManager.ApiClient.ExecuteRequestAsync(new Skylight.Api.Authentication.V1.GroupsRequests.UnassignUserGroupRequest(TestUserId, groupId));

            
            await SkyManager.ApiClient.ExecuteRequestAsync(new Skylight.Api.Authentication.V1.UsersRequests.AssignUserGroupRequest(TestUserId, groupId));
            var userGroups = await SkyManager.ApiClient.ExecuteRequestAsync(new Skylight.Api.Authentication.V1.UsersRequests.GetUserGroupsRequest(TestUserId));
            Assert.Single(userGroups.Content);
            userGroups = await SkyManager.ApiClient.ExecuteRequestAsync(new Skylight.Api.Authentication.V1.GroupsRequests.GetUserGroupsRequest(TestUserId));
            Assert.Single(userGroups.Content);
            await SkyManager.ApiClient.ExecuteRequestAsync(new Skylight.Api.Authentication.V1.UsersRequests.UnassignUserGroupRequest(TestUserId, groupId));

            var deleteGroupRequest = new DeleteGroupRequest(groupId);
            await SkyManager.ApiClient.ExecuteRequestAsync(deleteGroupRequest);
        }

        [Fact]
        public async Task TestUpdateGroup() {
            var groupNew = new GroupNew().SetDefaults();
            groupNew.Name = "api.test.group." + new Guid().ToString();
            groupNew.Description = "This is a test group for the API tests.";

            var createGroupRequest = new CreateGroupRequest(groupNew);
            var createGroupResponse = await SkyManager.ApiClient.ExecuteRequestAsync(createGroupRequest);
            var groupId = createGroupResponse.Content.Id;

            var groupUpdate = new GroupUpdate().SetDefaults();
            groupUpdate.Description = "Updated decsription";
            groupUpdate.Name = "Update group name";
            Assert.NotNull(groupUpdate.ToJson());
            Assert.NotNull(groupUpdate.ToString());

            var updateGroupRequest = new UpdateGroupRequest(groupUpdate, groupId);
            var updateGroupResponse = await SkyManager.ApiClient.ExecuteRequestAsync(updateGroupRequest);

            var deleteGroupRequest = new DeleteGroupRequest(groupId);
            await SkyManager.ApiClient.ExecuteRequestAsync(deleteGroupRequest);
        }

        [Fact]
        public async Task TestCreateAndDeleteUser() {
            var userPassword = "password";
            var userNew = new UserNew().SetDefaults();
            userNew.Avatar = "";
            userNew.City = "City";
            userNew.Email = "testuser@upskill.io";
            userNew.FirstName = "Test";
            userNew.JobTitle = "Tester";
            userNew.LastName = "User";
            userNew.Locale = "eng";
            userNew.Location = "Test Center";
            userNew.MobilePhone = "(123) 456-7890";
            userNew.OfficePhone = "(123) 456-7890";
            userNew.Password = userPassword;
            userNew.Role = Role.User;
            userNew.State = "State";
            userNew.Street = "Street";
            userNew.Username =  "test.user" + new Guid().ToString();
            userNew.Zipcode = 12345;
            
            Assert.NotNull(userNew.ToJson());
            Assert.NotNull(userNew.ToString());

            var createUserRequest = new CreateUserRequest(userNew);
            var createUserResponse = await SkyManager.ApiClient.ExecuteRequestAsync(createUserRequest);
            var userId = createUserResponse.Content.Id;

            var getUsersResponse = await SkyManager.ApiClient.ExecuteRequestAsync(new GetUsersRequest());
            Assert.InRange(getUsersResponse.Content.Count, 1, int.MaxValue);

            await SkyManager.ApiClient.ExecuteRequestAsync(new DeleteUserRequest(userId));
        }

        [Fact]
        public async Task TestChangePassword() {
            var userPassword = "password";
            var userNew = new UserNew().SetDefaults();
            userNew.Username =  "test.user" + new Guid().ToString();
            userNew.Password = userPassword;
            userNew.Role = Role.User;

            var createUserRequest = new CreateUserRequest(userNew);
            var createUserResponse = await SkyManager.ApiClient.ExecuteRequestAsync(createUserRequest);
            var userId = createUserResponse.Content.Id;

            var changeUserPasswordBody = new ChangeUserPasswordBody().SetDefaults();
            changeUserPasswordBody.NewPassword = "newPassword";
            changeUserPasswordBody.Temporary = false;
            Assert.NotNull(changeUserPasswordBody.ToJson());
            Assert.NotNull(changeUserPasswordBody.ToString());
            await SkyManager.ApiClient.ExecuteRequestAsync(new ChangeUserPasswordRequest(changeUserPasswordBody, userId));

            await SkyManager.ApiClient.ExecuteRequestAsync(new DeleteUserRequest(userId));
        
        }

        [Fact]
        public async Task TestUpdateUser() {
            var userNew = new UserNew().SetDefaults();
            userNew.Password = "password";
            userNew.Role = Role.User;
            userNew.Username =  "test.user" + new Guid().ToString();
            
            var createUserRequest = new CreateUserRequest(userNew);
            var createUserResponse = await SkyManager.ApiClient.ExecuteRequestAsync(createUserRequest);
            var userId = createUserResponse.Content.Id;

            var userUpdate = new UserUpdate().SetDefaults();
            userUpdate.Avatar = "";
            userUpdate.City = "City";
            userUpdate.Email = "testuser@upskill.io";
            userUpdate.FirstName = "Test";
            userUpdate.JobTitle = "Tester";
            userUpdate.LastName = "User";
            userUpdate.Locale = "eng";
            userUpdate.Location = "Test Center";
            userUpdate.MobilePhone = "(123) 456-7890";
            userUpdate.OfficePhone = "(123) 456-7890";
            userUpdate.State = "State";
            userUpdate.Street = "Street";
            userUpdate.Username =  "test.user" + new Guid().ToString();
            userUpdate.Zipcode = 12345;
            
            Assert.NotNull(userUpdate.ToJson());
            Assert.NotNull(userUpdate.ToString());
            await SkyManager.ApiClient.ExecuteRequestAsync(new UpdateUserRequest(userUpdate, userId));

            var getUserResponse = await SkyManager.ApiClient.ExecuteRequestAsync(new GetUserRequest(userId));
            Assert.Equal("City", getUserResponse.Content.City);

            await SkyManager.ApiClient.ExecuteRequestAsync(new DeleteUserRequest(userId));
        }

        [Fact]
        public async Task TestChangeUserRole() {
            var userNew = new UserNew().SetDefaults();
            userNew.Password = "password";
            userNew.Role = Role.User;
            userNew.Username =  "test.user" + new Guid().ToString();
            
            var createUserRequest = new CreateUserRequest(userNew);
            var createUserResponse = await SkyManager.ApiClient.ExecuteRequestAsync(createUserRequest);
            var userId = createUserResponse.Content.Id;

            var userRoleChange = new ChangeUserRoleBody().SetDefaults();
            userRoleChange.Role = Role.Manager;
            
            Assert.NotNull(userRoleChange.ToJson());
            Assert.NotNull(userRoleChange.ToString());
            await SkyManager.ApiClient.ExecuteRequestAsync(new ChangeUserRoleRequest(userRoleChange, userId));

            var getUserResponse = await SkyManager.ApiClient.ExecuteRequestAsync(new GetUserRequest(userId));
            Assert.Equal(Role.Manager, getUserResponse.Content.Role);

            await SkyManager.ApiClient.ExecuteRequestAsync(new DeleteUserRequest(userId));

        }


    }
}
