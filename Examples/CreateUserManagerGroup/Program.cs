﻿
using System.IO;
using System;
using Skylight.Client;
using System.Threading.Tasks;
using Skylight.Sdk;
using Skylight.Api.Authentication.V1.Models;

namespace CreateUserManagerGroup
{
    /*
        INFO: Throughout this example, there are comments that begin with @skydocs -- 
        these are tags used by the Skylight Developer Portal and are not necessary for
        this example to function.
     */
    class Program
    {
        public static Manager SkyManager;
        public static string Username = "api.test.user";
        public static string Groupname = "API Test Group";
        static async Task Main(string[] args)
        {

            //Create our manager and point it to our credentials file
            SkyManager = new Manager(Path.Combine("..", "..", "credentials.json"));
            
            //Connect to Skylight
            await SkyManager.Connect();
            Console.WriteLine("Skylight connected");

            string userId;
            try {
                userId = await CreateUser("API Test", "User", Role.User, Username, "password");
            } catch (ApiException e){
                Console.WriteLine("User creation error: " + e.Message);
                //Try to get the userId, in case it already exists
                userId = await GetUserIdForUsername(Username);
            }
            if(String.IsNullOrEmpty(userId)) throw new Exception("User could not be created or found.");

            string groupId;
            try {
                groupId = await CreateGroup(Groupname, "A group created by the SDK examples.");
            } catch (ApiException e){
                Console.WriteLine("Group creation error: " + e.Message);
                groupId = await GetGroupIdForGroupname(Groupname);
            }
            if(String.IsNullOrEmpty(groupId)) throw new Exception("Group could not be created or found.");

            await AssignUserToGroup(userId, groupId);
            await SetUserPasswordAsTemporary(userId);
            await GetUserById(userId);
            await UpdateUserJobTitle(userId, "Developer");
            await DeleteUserById(userId);
            await DeleteGroupById(groupId);
        }

        static async Task<string> CreateUser(string first, string last, Role role, string username, string password) {
            //@skydocs.start(users.create)
            //This is the body of information we use to create a new user
            var newUserBody = new Skylight.Api.Authentication.V1.Models.UserNew
            {
                FirstName = first,
                LastName = last,
                Role = role,            //For role, the API accepts the string values "user", "manager", and "admin"
                Username = username,
                Password = password     //The password can be set as temporary by using the "change password" API call
            };

            //This is our API request for creating a new user
            var createUserRequest = new Skylight.Api.Authentication.V1.UsersRequests.CreateUserRequest(newUserBody);

            //Execute the request
            var result = await SkyManager.ApiClient.ExecuteRequestAsync(createUserRequest);

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
            Console.WriteLine("Created user with id: " + result.Content.Id);
            return result.Content.Id;
        }

        static async Task<string> CreateGroup(string name, string description) {
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
            var result = await SkyManager.ApiClient.ExecuteRequestAsync(createGroupRequest);

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
            Console.WriteLine("Created group with id: " + result.Content.Id);
            return result.Content.Id;
        }

        static async Task AssignUserToGroup(string userId, string groupId) {
            //@skydocs.start(groups.assign)
            //Create our API request for assigning a user to a group, specifying IDs for both
            var assignGroupRequest = new Skylight.Api.Authentication.V1.UsersRequests.AssignUserGroupRequest(userId, groupId);

            //Execute the API request
            var result = await SkyManager.ApiClient.ExecuteRequestAsync(assignGroupRequest);

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
                    Console.WriteLine("User successfully assigned to group.");
                    break;
                default:
                    Console.Error.WriteLine("Unhandled group assignment status code: " + result.StatusCode);
                    break;
            }
            //@skydocs.end()
        }

        
        static async Task UnassignUserFromGroup(string userId, string groupId) {
            //@skydocs.start(groups.unassign)
            //Create our API request for assigning a user to a group, specifying IDs for both
            var unassignGroupRequest = new Skylight.Api.Authentication.V1.UsersRequests.UnassignUserGroupRequest(userId, groupId);

            //Execute the API request
            var result = await SkyManager.ApiClient.ExecuteRequestAsync(unassignGroupRequest);

            //Handle the resulting status code appropriately
            switch(result.StatusCode) {
                case System.Net.HttpStatusCode.Forbidden:
                    Console.Error.WriteLine("Error unassigning group: Permission forbidden.");
                    break;
                case System.Net.HttpStatusCode.Unauthorized:
                    Console.Error.WriteLine("Error unassigning group: Method call was unauthenticated.");
                    break;
                case System.Net.HttpStatusCode.NotFound:
                    Console.Error.WriteLine("Error unassigning group: User or group not found.");
                    break;
                case System.Net.HttpStatusCode.NoContent:
                    Console.WriteLine("User successfully unassigned from group.");
                    break;
                default:
                    Console.Error.WriteLine("Unhandled group unassignment status code: " + result.StatusCode);
                    break;
            }
            //@skydocs.end()
        }

        static async Task SetUserPasswordAsTemporary(string userId) {
            //@skydocs.start(users.temporarypassword)
            //This is the body of information for changing a user's password
            var temporaryPasswordRequestBody = new Skylight.Api.Authentication.V1.Models.ChangeUserPasswordBody
            {
                Temporary = true, //Setting this to true will force the user to change their password upon next login
                NewPassword = "temporary" //The user will use this as their password to login (until they change it themselves)
            };

            //Create our password change API request
            var temporaryPasswordRequest = new Skylight.Api.Authentication.V1.UsersRequests.ChangeUserPasswordRequest(temporaryPasswordRequestBody, userId);

            //Execute the API request
            var result = await SkyManager.ApiClient.ExecuteRequestAsync(temporaryPasswordRequest);

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
            var result = await SkyManager.ApiClient.ExecuteRequestAsync(deleteUserRequest);

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
            var result = await SkyManager.ApiClient.ExecuteRequestAsync(deleteGroupRequest);
            
            //Handle the resulting status code appropriately
            switch(result.StatusCode) {
                case System.Net.HttpStatusCode.Forbidden:
                    Console.Error.WriteLine("Error deleting group: Permission forbidden.");
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

        static async Task UpdateUserJobTitle(string userId, string jobTitle) {
            //@skydocs.start(users.update)
            //This is the body of information for updating the user
            //In this example, we update the job title
            var replaceUserBody = new UserUpdate {
                JobTitle = jobTitle
            };

            //Create an API request for updating a user
            var updateUserRequest = new Skylight.Api.Authentication.V1.UsersRequests.UpdateUserRequest(replaceUserBody, userId);

            //Execute the API request
            var result = await SkyManager.ApiClient.ExecuteRequestAsync(updateUserRequest);

            //Handle the resulting status code appropriately
            switch(result.StatusCode) {
                case System.Net.HttpStatusCode.Forbidden:
                    Console.Error.WriteLine("Error updating user: Permission forbidden.");
                    break;
                case System.Net.HttpStatusCode.NotFound:
                    Console.Error.WriteLine("Error updating user: User not found.");
                    break;
                case System.Net.HttpStatusCode.Unauthorized:
                    Console.Error.WriteLine("Error updating user: Method call was unauthenticated.");
                    break;
                case System.Net.HttpStatusCode.NoContent:
                    Console.WriteLine("User successfully updated.");
                    break;
                default:
                    Console.Error.WriteLine("Unhandled user update status code: " + result.StatusCode);
                    break;
            }
            //@skydocs.end()
        }

        static async Task ChangeUserRole(string userId, Role role) {
            //@skydocs.start(users.updaterole)
            //This is the body of information for updating the user role
            var updateUserRoleBody = new ChangeUserRoleBody {
                Role = role
            };

            //Create an API request for updating a user
            var updateUserRoleRequest = new Skylight.Api.Authentication.V1.UsersRequests.ChangeUserRoleRequest(updateUserRoleBody, userId);

            //Execute the API request
            var result = await SkyManager.ApiClient.ExecuteRequestAsync(updateUserRoleRequest);

            //Handle the resulting status code appropriately
            switch(result.StatusCode) {
                case System.Net.HttpStatusCode.Forbidden:
                    Console.Error.WriteLine("Error updating user role: Permission forbidden.");
                    break;
                case System.Net.HttpStatusCode.NotFound:
                    Console.Error.WriteLine("Error updating user role: User not found.");
                    break;
                case System.Net.HttpStatusCode.Unauthorized:
                    Console.Error.WriteLine("Error updating user role: Method call was unauthenticated.");
                    break;
                case System.Net.HttpStatusCode.NoContent:
                    Console.WriteLine("User role successfully updated.");
                    break;
                default:
                    Console.Error.WriteLine("Unhandled user role update status code: " + result.StatusCode);
                    break;
            }
            //@skydocs.end()
        }

        static async Task LogoutUser(string userId) {
            //@skydocs.start(users.logout)
            //Create an API request for logging out a user
            var logoutUserRequest = new Skylight.Api.Authentication.V1.UsersRequests.LogoutUserRequest(userId);

            //Execute the API request
            var result = await SkyManager.ApiClient.ExecuteRequestAsync(logoutUserRequest);

            //Handle the resulting status code appropriately
            switch(result.StatusCode) {
                case System.Net.HttpStatusCode.Forbidden:
                    Console.Error.WriteLine("Error logging out user: Permission forbidden.");
                    break;
                case System.Net.HttpStatusCode.NotFound:
                    Console.Error.WriteLine("Error logging out user: User not found.");
                    break;
                case System.Net.HttpStatusCode.Unauthorized:
                    Console.Error.WriteLine("Error logging out user: Method call was unauthenticated.");
                    break;
                case System.Net.HttpStatusCode.BadRequest:
                    Console.Error.WriteLine("Error logging out user: Incorrect parameters.");
                    break;
                case System.Net.HttpStatusCode.NoContent:
                    Console.WriteLine("User logged out successfully.");
                    break;
                default:
                    Console.Error.WriteLine("Unhandled user role update status code: " + result.StatusCode);
                    break;
            }
            //@skydocs.end()
        }

        static async Task<UserWithGroups> GetUserById(string userId) {
            //@skydocs.start(users.getbyid)
            //Create an API request for retrieving the user by its id
            var getUserRequest = new Skylight.Api.Authentication.V1.UsersRequests.GetUserRequest(userId);

            //Execute the API request
            var result = await SkyManager.ApiClient.ExecuteRequestAsync(getUserRequest);

            //Handle the resulting status code appropriately
            switch(result.StatusCode) {
                case System.Net.HttpStatusCode.Forbidden:
                    Console.Error.WriteLine("Error retrieving user: Permission forbidden.");
                    break;
                case System.Net.HttpStatusCode.NotFound:
                    Console.Error.WriteLine("Error retrieving user: User not found.");
                    break;
                case System.Net.HttpStatusCode.Unauthorized:
                    Console.Error.WriteLine("Error retrieving user: Method call was unauthenticated.");
                    break;
                case System.Net.HttpStatusCode.OK:
                    Console.WriteLine("User successfully retrieved.");
                    break;
                default:
                    Console.Error.WriteLine("Unhandled user update status code: " + result.StatusCode);
                    break;
            }
            return result.Content;
            //@skydocs.end()
        }

        static async Task<string> GetUserIdForUsername(string username) {
            
            //@skydocs.start(users.getall)
            //Create an API request for retrieving all users
            var getUsersRequest = new Skylight.Api.Authentication.V1.UsersRequests.GetUsersRequest();

            //Execute the API request
            var result = await SkyManager.ApiClient.ExecuteRequestAsync(getUsersRequest);

            //The users will be stored as a list in the result's Content, so we can iterate through them
            foreach(var user in result.Content) {
                if(user.Username == username)return user.Id;
            }
            return null;
            //@skydocs.end()
        }

        
        static async Task UpdateGroupDescription(string groupId, string description) {
            //@skydocs.start(groups.update)
            //This is the body of information for updating the group
            //In this example, we update the job title
            var updateGroupBody = new GroupUpdate {
                Description = description
            };

            //Create an API request for updating a group
            var updateGroupRequest = new Skylight.Api.Authentication.V1.GroupsRequests.UpdateGroupRequest(updateGroupBody, groupId);

            //Execute the API request
            var result = await SkyManager.ApiClient.ExecuteRequestAsync(updateGroupRequest);

            //Handle the resulting status code appropriately
            switch(result.StatusCode) {
                case System.Net.HttpStatusCode.Forbidden:
                    Console.Error.WriteLine("Error updating group: Permission forbidden.");
                    break;
                case System.Net.HttpStatusCode.NotFound:
                    Console.Error.WriteLine("Error updating group: Group not found.");
                    break;
                case System.Net.HttpStatusCode.Unauthorized:
                    Console.Error.WriteLine("Error updating group: Method call was unauthenticated.");
                    break;
                case System.Net.HttpStatusCode.NoContent:
                    Console.WriteLine("Group successfully updated.");
                    break;
                default:
                    Console.Error.WriteLine("Unhandled group update status code: " + result.StatusCode);
                    break;
            }
            //@skydocs.end()
        }

        static async Task<GroupWithMembers> GetGroupById(string groupId) {
            //@skydocs.start(groups.getbyid)
            //Create an API request for retrieving the group by its id
            var getGroupRequest = new Skylight.Api.Authentication.V1.GroupsRequests.GetGroupRequest(groupId);

            //Execute the API request
            var result = await SkyManager.ApiClient.ExecuteRequestAsync(getGroupRequest);

            //Handle the resulting status code appropriately
            switch(result.StatusCode) {
                case System.Net.HttpStatusCode.Forbidden:
                    Console.Error.WriteLine("Error retrieving group: Permission forbidden.");
                    break;
                case System.Net.HttpStatusCode.NotFound:
                    Console.Error.WriteLine("Error retrieving group: Group not found.");
                    break;
                case System.Net.HttpStatusCode.Unauthorized:
                    Console.Error.WriteLine("Error retrieving group: Method call was unauthenticated.");
                    break;
                case System.Net.HttpStatusCode.OK:
                    Console.WriteLine("Group successfully retrieved.");
                    break;
                default:
                    Console.Error.WriteLine("Unhandled group update status code: " + result.StatusCode);
                    break;
            }
            return result.Content;
            //@skydocs.end()
        }

        static async Task<string> GetGroupIdForGroupname(string name) {
            
            //@skydocs.start(groups.getall)
            //Create an API request for retrieving all groups
            var getGroupsRequest = new Skylight.Api.Authentication.V1.GroupsRequests.GetGroupsRequest();
            
            //Execute the API request
            var result = await SkyManager.ApiClient.ExecuteRequestAsync(getGroupsRequest);

            //The list of groups visible using our API credentials will be returned in the result's Content
            foreach(var group in result.Content) {
                if(group.Name == name)return group.Id;
            }
            return null;
            //@skydocs.end()
        }
    }
}
