
using System.Security.Authentication;
using System.Security.Principal;
using System.IO;
using System;
using Microsoft.Extensions.Configuration;
using Skylight.Client;
using System.Threading.Tasks;
using Skylight.Sdk;

namespace CreateUserManagerGroup
{
    class Program
    {
        public static Manager Manager;
        static async Task Main(string[] args)
        {
            try {
                //Create our manager and point it to our credentials file
                Manager = new Manager(Path.Combine("..", "..", "credentials.json"));
            } catch { return; }

            try {
                await CreateUser("API Test", "User", "user", "api.test.user", "password");
            } catch (ApiException e){
                Console.WriteLine("User creation error: " + e.Message);
            }
            string userId = await GetUserIdForUsername("api.test.user");

            try {
                await CreateGroup("API Test Group", "A group created by the SDK examples.");
            } catch (ApiException e){
                Console.WriteLine("Group creation error: " + e.Message);
            }
            string groupId = await GetGroupIdForGroupname("API Test Group");

            await AddUserToGroup(userId, groupId);
            await SetUserPasswordAsTemporary(userId);
            await DeleteUserById(userId);
            await DeleteGroupById(groupId);
        }

        static async Task CreateUser(string first, string last, string role, string username, string password) {
            
            //@skydocs.start(users.create)
            var newUserBody = new Skylight.Api.Authentication.V1.Models.UserNew
            {
                FirstName = first,
                LastName = last,
                Role = role,            //For role, the API accepts the string values "user", "manager", and "admin"
                Username = username,
                Password = password     //The password can be set as temporary by using another user update
            };

            var createUserRequest = new Skylight.Api.Authentication.V1.UsersRequests.CreateUserRequest(newUserBody);
            var result = await Manager.ApiClient.ExecuteRequestAsync(createUserRequest);
            switch(result.StatusCode) {
                case System.Net.HttpStatusCode.Forbidden:
                    Console.Error.WriteLine("Error creating user: Permission forbidden.");
                    break;
                case System.Net.HttpStatusCode.Unauthorized:
                    Console.Error.WriteLine("Error creating user: Method call was unauthenticated.");
                    break;
                case System.Net.HttpStatusCode.Created:
                    Console.WriteLine("User successfully created.");
                    break;
                default:
                    Console.Error.WriteLine("Unhandled user creation status code: " + result.StatusCode);
                    break;
            }
            //@skydocs.end()
        }

        static async Task CreateGroup(string name, string description) {
            
            //@skydocs.start(groups.create)
            var newGroupBody = new Skylight.Api.Authentication.V1.Models.GroupNew
            {
                Name = name,
                Description = description
            };

            var createGroupRequest = new Skylight.Api.Authentication.V1.GroupsRequests.CreateGroupRequest(newGroupBody);
            var result = await Manager.ApiClient.ExecuteRequestAsync(createGroupRequest);
            switch(result.StatusCode) {
                case System.Net.HttpStatusCode.Forbidden:
                    Console.Error.WriteLine("Error creating group: Permission forbidden.");
                    break;
                case System.Net.HttpStatusCode.Unauthorized:
                    Console.Error.WriteLine("Error creating group: Method call was unauthenticated.");
                    break;
                case System.Net.HttpStatusCode.Created:
                    Console.WriteLine("Group successfully created.");
                    break;
                default:
                    Console.Error.WriteLine("Unhandled group creation status code: " + result.StatusCode);
                    break;
            }
            //@skydocs.end()
        }

        static async Task AddUserToGroup(string userId, string groupId) {
            
            //@skydocs.start(groups.assign)
            var assignGroupRequest = new Skylight.Api.Authentication.V1.GroupsRequests.AssignUserToGroupRequest(userId, groupId);
            var result = await Manager.ApiClient.ExecuteRequestAsync(assignGroupRequest);
            switch(result.StatusCode) {
                case System.Net.HttpStatusCode.Forbidden:
                    Console.Error.WriteLine("Error assigning group: Permission forbidden.");
                    break;
                case System.Net.HttpStatusCode.Unauthorized:
                    Console.Error.WriteLine("Error assigning group: Method call was unauthenticated.");
                    break;
                case System.Net.HttpStatusCode.NotFound:
                    Console.Error.WriteLine("Error assigning group: User or group not found.");
                    break;
                case System.Net.HttpStatusCode.NoContent:
                    Console.WriteLine("Group successfully assigned.");
                    break;
                default:
                    Console.Error.WriteLine("Unhandled group assignment status code: " + result.StatusCode);
                    break;
            }
            //@skydocs.end()
        }

        static async Task SetUserPasswordAsTemporary(string userId) {
            
            //@skydocs.start(users.temporarypassword)
            var temporaryPasswordRequestBody = new Skylight.Api.Authentication.V1.Models.UsersIdChangePasswordBody
            {
                Temporary = true, //Setting this to true will force the user to change their password upon next login
                NewPassword = "temporary-password" //The user will use this as their password to login (until they change it themselves)
            };
            var temporaryPasswordRequest = new Skylight.Api.Authentication.V1.UsersRequests.UsersIdChangePasswordPutRequest(temporaryPasswordRequestBody, userId);
            var result = await Manager.ApiClient.ExecuteRequestAsync(temporaryPasswordRequest);
            switch(result.StatusCode) {
                case System.Net.HttpStatusCode.BadRequest:
                    Console.Error.WriteLine("Error setting temporary password: Bad request.");
                    break;
                case System.Net.HttpStatusCode.Forbidden:
                    Console.Error.WriteLine("Error setting temporary password: Permission forbidden.");
                    break;
                case System.Net.HttpStatusCode.Unauthorized:
                    Console.Error.WriteLine("Error setting temporary password: Method call was unauthenticated.");
                    break;
                case System.Net.HttpStatusCode.NotFound:
                    Console.Error.WriteLine("Error setting temporary password: User not found.");
                    break;
                case System.Net.HttpStatusCode.NoContent:
                    Console.WriteLine("Temporary password successfully set.");
                    break;
                default:
                    Console.Error.WriteLine("Unhandled temporary password set status code: " + result.StatusCode);
                    break;
            }
            //@skydocs.end()
        }

        static async Task DeleteUserById(string userId) {
            
            //@skydocs.start(users.delete)
            var deleteUserRequest = new Skylight.Api.Authentication.V1.UsersRequests.DeleteUserRequest(userId);
            var result = await Manager.ApiClient.ExecuteRequestAsync(deleteUserRequest);
            switch(result.StatusCode) {
                case System.Net.HttpStatusCode.Forbidden:
                    Console.Error.WriteLine("Error deleting user: Permission forbidden.");
                    break;
                case System.Net.HttpStatusCode.NotFound:
                    Console.Error.WriteLine("Error deleting user: User not found.");
                    break;
                case System.Net.HttpStatusCode.Unauthorized:
                    Console.Error.WriteLine("Error deleting user: Method call was unauthenticated.");
                    break;
                case System.Net.HttpStatusCode.NoContent:
                    Console.WriteLine("User successfully deleted.");
                    break;
                default:
                    Console.Error.WriteLine("Unhandled user deletion status code: " + result.StatusCode);
                    break;
            }
            //@skydocs.end()
        }

        static async Task DeleteGroupById(string groupId) {
            
            //@skydocs.start(groups.delete)
            var deleteGroupRequest = new Skylight.Api.Authentication.V1.GroupsRequests.DeleteGroupRequest(groupId);
            var result = await Manager.ApiClient.ExecuteRequestAsync(deleteGroupRequest);
            switch(result.StatusCode) {
                case System.Net.HttpStatusCode.Forbidden:
                    Console.Error.WriteLine("Error deleting froup: Permission forbidden.");
                    break;
                case System.Net.HttpStatusCode.NotFound:
                    Console.Error.WriteLine("Error deleting group: Group not found.");
                    break;
                case System.Net.HttpStatusCode.Unauthorized:
                    Console.Error.WriteLine("Error deleting group: Method call was unauthenticated.");
                    break;
                case System.Net.HttpStatusCode.NoContent:
                    Console.WriteLine("Group successfully deleted.");
                    break;
                default:
                    Console.Error.WriteLine("Unhandled group deletion status code: " + result.StatusCode);
                    break;
            }
            //@skydocs.end()
        }

        static async Task<string> GetUserIdForUsername(string username) {
            
            //@skydocs.start(users.getbyname)
            var getUsersRequest = new Skylight.Api.Authentication.V1.UsersRequests.GetUsersListRequest();
            var result = await Manager.ApiClient.ExecuteRequestAsync(getUsersRequest);
            foreach(var user in result.Content) {
                if(user.Username == username)return user.Id;
            }
            return null;
            //@skydocs.end()
        }

        static async Task<string> GetGroupIdForGroupname(string name) {
            
            //@skydocs.start(groups.getbyname)
            var getGroupsRequest = new Skylight.Api.Authentication.V1.GroupsRequests.GetGroupsListRequest();
            var result = await Manager.ApiClient.ExecuteRequestAsync(getGroupsRequest);
            foreach(var group in result.Content) {
                if(group.Name == name)return group.Id;
            }
            return null;
            //@skydocs.end()
        }
    }
}
