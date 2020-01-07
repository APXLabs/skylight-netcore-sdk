
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
            //This is the body of information we use to create a new user
            var newUserBody = new Skylight.Api.Authentication.V1.Models.UserNew
            {
                FirstName = first,
                LastName = last,
                Role = role,            //For role, the API accepts the string values "user", "manager", and "admin"
                Username = username,
                Password = password     //The password can be set as temporary by using another user update
            };

            //This is our API request for creating a new user
            var createUserRequest = new Skylight.Api.Authentication.V1.UsersRequests.CreateUserRequest(newUserBody);

            //Execute the request
            var result = await Manager.ApiClient.ExecuteRequestAsync(createUserRequest);

            //Handle the resulting status code appropriately
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
            //This is the body of information we use to create a new group
            var newGroupBody = new Skylight.Api.Authentication.V1.Models.GroupNew
            {
                Name = name,
                Description = description
            };

            //This is our API request for creating a new group
            var createGroupRequest = new Skylight.Api.Authentication.V1.GroupsRequests.CreateGroupRequest(newGroupBody);

            //Execute our API request
            var result = await Manager.ApiClient.ExecuteRequestAsync(createGroupRequest);

            //Handle the resulting status code appropriately
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
            //Create our API request for assigning a user to a group, specifying IDs for both
            var assignGroupRequest = new Skylight.Api.Authentication.V1.GroupsRequests.AssignUserToGroupRequest(userId, groupId);

            //Execute the API request
            var result = await Manager.ApiClient.ExecuteRequestAsync(assignGroupRequest);

            //Handle the resulting status code appropriately
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
            //This is the body of information for changing a user's password
            var temporaryPasswordRequestBody = new Skylight.Api.Authentication.V1.Models.UsersIdChangePasswordBody
            {
                Temporary = true, //Setting this to true will force the user to change their password upon next login
                NewPassword = "temporary-password" //The user will use this as their password to login (until they change it themselves)
            };

            //Create our password change API request
            var temporaryPasswordRequest = new Skylight.Api.Authentication.V1.UsersRequests.UsersIdChangePasswordPutRequest(temporaryPasswordRequestBody, userId);

            //Execute the API request
            var result = await Manager.ApiClient.ExecuteRequestAsync(temporaryPasswordRequest);

            //Handle the resulting status code appropriately
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
            //Create our user deletion API request by specifying the user's ID
            var deleteUserRequest = new Skylight.Api.Authentication.V1.UsersRequests.DeleteUserRequest(userId);
            
            //Execute the API request
            var result = await Manager.ApiClient.ExecuteRequestAsync(deleteUserRequest);

            //Handle the resulting status code appropriately
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
            //Create a group deletion request by specifying the group's ID
            var deleteGroupRequest = new Skylight.Api.Authentication.V1.GroupsRequests.DeleteGroupRequest(groupId);

            //Execute our API request
            var result = await Manager.ApiClient.ExecuteRequestAsync(deleteGroupRequest);
            
            //Handle the resulting status code appropriately
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
            //Create an API request for retrieving all users
            var getUsersRequest = new Skylight.Api.Authentication.V1.UsersRequests.GetUsersListRequest();

            //Execute the API request
            var result = await Manager.ApiClient.ExecuteRequestAsync(getUsersRequest);

            //The users will be stored as a list in the result's Content, so we can iterate through them
            foreach(var user in result.Content) {
                if(user.Username == username)return user.Id;
            }
            return null;
            //@skydocs.end()
        }

        static async Task<string> GetGroupIdForGroupname(string name) {
            
            //@skydocs.start(groups.getbyname)
            //Create an API request for retrieving all groups
            var getGroupsRequest = new Skylight.Api.Authentication.V1.GroupsRequests.GetGroupsListRequest();
            
            //Execute the API request
            var result = await Manager.ApiClient.ExecuteRequestAsync(getGroupsRequest);

            //The list of groups visible using our API credentials will be returned in the result's Content
            foreach(var group in result.Content) {
                if(group.Name == name)return group.Id;
            }
            return null;
            //@skydocs.end()
        }
    }
}
