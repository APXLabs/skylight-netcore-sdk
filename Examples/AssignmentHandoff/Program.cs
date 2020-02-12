
using System.Runtime.InteropServices;
using System.IO;
using System;
using Microsoft.Extensions.Configuration;
using Skylight.Client;
using System.Threading.Tasks;
using System.Collections.Generic;
using Skylight.Sdk;
using Skylight.Mqtt;
using Skylight.Api.Assignments.V1.Models;
using Skylight.Api.Authentication.V1.Models;
using System.Threading;
using MQTTnet.Client;
using Newtonsoft.Json;

namespace AssignmentHandoff
{
    /*
        INFO: Throughout this example, there are comments that begin with @skydocs -- 
        these are tags used by the Skylight Developer Portal and are not necessary for
        this example to function.
     */
    class Program
    {
        public static Manager SkyManager;
        private static readonly string TEST_ACCOUNT_GROUP = "api.test.group";
        private static readonly string ROOT_SEQUENCE_ID = "rootSequence";
        private const string CARD_ID_DELIMITER = "-";
        private const string CHECKOUT_CARD_ID_PREFIX = "checkin";
        private const string CHECKIN_CARD_ID_PREFIX = "checkout";
        private const string SUPER_ASSIGNMENT_NAME = "View Available Assignments";
        private const string SUB_ASSIGNMENT_NAME_PREFIX ="Assignment ";
        private const int NUM_SUB_ASSIGNMENTS = 5;

        private static List<string> SubAssignmentIds = new List<string>();
        private static List<string> SuperAssignmentIds = new List<string>();
        static async Task Main(string[] args)
        {
            try {
                /*
                    Create our manager by passing it the path to our credentials.json file.
                    This json file has our API credentials copy-pasted from Skylight Web.
                    The path is also optional; the constructor for the SDK's manager can also take 0 arguments, in which case it will search for a file called `credentials.json` in the root directory of the extension.
                */
                SkyManager = new Manager();
            } catch { return; }

            //We'll have a simple event listener that listens for the user requesting the creation/deletion of cards and sequences
            SkyManager.MessagingClient.MessageReceived += async (object sender, MessageReceivedEventArgs args) => { 
                dynamic message = JsonConvert.DeserializeObject(args.Message);
                if(!((string)message["eventType"]).Equals("cards"))return;
                if(!((string)message["event"]).Equals("update"))return;
                Console.WriteLine(message);
                //var userId = args.Topic.Split("/")[3];
                var assignmentId = (string)message["assignmentId"];
                var sequenceId = (string)message["sequenceId"];
                var cardId = (string)message["cardId"];
                await CardUpdated(cardId, sequenceId, assignmentId);
                
            };
            await SkyManager.StartListening();

            await CreateSuperAssignments();

            //Create the low-level assignments
            await CreateSubAssignments();
            
            //Wait forever (at least, until the program is stopped)
            SpinWait.SpinUntil(() => false);
        }

        static async Task CardUpdated(string cardId, string sequenceId, string assignmentId) {

            //Handle the card update event based on the card's id
            var shouldResetCard = false;

            string[] cardIdSplit = cardId.Split(CARD_ID_DELIMITER);
            string cardAction = cardIdSplit[0];
            switch(cardAction){
                case CHECKIN_CARD_ID_PREFIX:
                    shouldResetCard = true;
                    int checkinAssignmentIndex = int.Parse(cardIdSplit[1]);//Given that we know the prefix, this will work assuming that we are within our assignment
                    await UnassignSubAssignment(checkinAssignmentIndex);
                    await CheckinAssignmentCard(checkinAssignmentIndex);
                    break;
                case CHECKOUT_CARD_ID_PREFIX:
                    var assignment = await GetAssignment(assignmentId);
                    var userId = assignment.AssignedTo;
                    int checkoutAssignmentIndex = int.Parse(cardIdSplit[1]);//Given that we know the prefix, this will work assuming that we are within our assignment
                    await AssignSubAssignment(checkoutAssignmentIndex, userId);

                    await CheckoutAssignmentCard(userId, checkoutAssignmentIndex);
                    break;
            }

            //Only progress forward if we're looking to reset the card
            if(!shouldResetCard)return;
            
            Card card = await GetCard(assignmentId, sequenceId, cardId);
            Console.WriteLine(card.Component.ComponentType);
            
            //Make sure we're working with the correct card type
            if(card.Component.ComponentType != ComponentType.Completion)return;
            Console.WriteLine("Resetting card");

            //Mark the card as not done, to allow the user to select the card again.
            await ResetCard(card);
        }

        static async Task CheckoutAssignmentCard(string userId, int assignmentIndex) {
            //First we'll get the username for this userId
            var userResult = await SkyManager.ApiClient.ExecuteRequestAsync(new Skylight.Api.Authentication.V1.UsersRequests.GetUserRequest(userId));
            var username = userResult.Content.Username;
            
            var cardPatch = new CardPatch();
            cardPatch.Add("isDone", false);
            cardPatch.Add("component", new ComponentCompletion() { Completed = false });
            cardPatch.Add("label", "Assignment " + assignmentIndex + " checked out by " + username);
            cardPatch.Add("selectable", false);
            cardPatch.Add("subdued", true);
            //For each of our super assignments, update the card
            foreach(var assignmentId in SuperAssignmentIds) {
                await SkyManager.ApiClient.ExecuteRequestAsync(new Skylight.Api.Assignments.V1.CardRequests.PatchCardRequest(cardPatch, assignmentId, ROOT_SEQUENCE_ID, CHECKOUT_CARD_ID_PREFIX + CARD_ID_DELIMITER + assignmentIndex));
                Console.WriteLine("Patching " + assignmentId);
            }
        }

        static async Task CheckinAssignmentCard(int assignmentIndex) {

            var cardPatch = new CardPatch();
            cardPatch.Add("isDone", false);
            cardPatch.Add("component", new ComponentCompletion() { Completed = false });
            cardPatch.Add("label", "Check out " + assignmentIndex);
            cardPatch.Add("selectable", true);
            cardPatch.Add("subdued", false);
            //For each of our super assignments, update the card
            foreach(var assignmentId in SuperAssignmentIds) {
                await SkyManager.ApiClient.ExecuteRequestAsync(new Skylight.Api.Assignments.V1.CardRequests.PatchCardRequest(cardPatch, assignmentId, ROOT_SEQUENCE_ID, CHECKOUT_CARD_ID_PREFIX + CARD_ID_DELIMITER + assignmentIndex));
            Console.WriteLine("Patching " + assignmentId);
            }
        }

        static async Task ResetCard(Card card) {
            
            var cardPatch = new CardPatch();
            cardPatch.Add("isDone", false); //Important note: Make sure that the keys (e.g. isDone) are lower case
            cardPatch.Add("component", new ComponentCompletion() { Completed = false });
            
            var cardUpdateBody = new Skylight.Api.Assignments.V1.CardRequests.PatchCardRequest(cardPatch, card.AssignmentId, card.SequenceId, card.Id);
            var cardUpdateResult = await SkyManager.ApiClient.ExecuteRequestAsync(cardUpdateBody);
            
            //Handle the resulting status code appropriately
            switch(cardUpdateResult.StatusCode) {
                case System.Net.HttpStatusCode.Forbidden:
                    Console.Error.WriteLine("Error updating card: Permission forbidden.");
                    throw new Exception("Error updating card.");
                case System.Net.HttpStatusCode.Unauthorized:
                    Console.Error.WriteLine("Error updating card: Method call was unauthenticated.");
                    throw new Exception("Error updating card.");
                case System.Net.HttpStatusCode.NotFound:
                    Console.Error.WriteLine("Error updating card: User not found.");
                    throw new Exception("Error updating card.");
                case System.Net.HttpStatusCode.OK:
                    Console.WriteLine("Successfully updated card.");
                    break;
                default:
                    Console.Error.WriteLine("Unhandled card update status code: " + cardUpdateResult.StatusCode);
                    throw new Exception("Error updating card.");
            }

        }


        static async Task<Assignment> GetAssignment(string assignmentId) {
            var result = await SkyManager.ApiClient.ExecuteRequestAsync(new Skylight.Api.Assignments.V1.AssignmentRequests.GetAssignmentRequest(assignmentId));

            //Handle the resulting status code appropriately
            switch(result.StatusCode) {
                case System.Net.HttpStatusCode.Forbidden:
                    Console.Error.WriteLine("Error getting assignment: Permission forbidden.");
                    throw new Exception("Error getting assignment.");
                case System.Net.HttpStatusCode.Unauthorized:
                    Console.Error.WriteLine("Error getting assignment: Method call was unauthenticated.");
                    throw new Exception("Error getting assignment.");
                case System.Net.HttpStatusCode.NotFound:
                    Console.Error.WriteLine("Error retrieving assignment: Assignment not found.");
                    throw new Exception("Error getting assignment.");
                case System.Net.HttpStatusCode.OK:
                    Console.WriteLine("Successfully retrieved assignment.");
                    break;
                default:
                    Console.Error.WriteLine("Unhandled assignment retrieval status code: " + result.StatusCode);
                    throw new Exception("Error getting assignment.");
            }

            return result.Content; //result.Content has our Assignment object
        }

        static async Task<Card> GetCard(string assignmentId, string sequenceId, string cardId) {
            var result = await SkyManager.ApiClient.ExecuteRequestAsync(new Skylight.Api.Assignments.V1.CardRequests.GetCardRequest(assignmentId, sequenceId, cardId));

            //Handle the resulting status code appropriately
            switch(result.StatusCode) {
                case System.Net.HttpStatusCode.Forbidden:
                    Console.Error.WriteLine("Error getting card: Permission forbidden.");
                    throw new Exception("Error getting card.");
                case System.Net.HttpStatusCode.Unauthorized:
                    Console.Error.WriteLine("Error getting card: Method call was unauthenticated.");
                    throw new Exception("Error getting card.");
                case System.Net.HttpStatusCode.NotFound:
                    Console.Error.WriteLine("Error retrieving card: Assignment, sequence, or card not found.");
                    throw new Exception("Error getting card.");
                case System.Net.HttpStatusCode.OK:
                    Console.WriteLine("Successfully retrieved card.");
                    break;
                default:
                    Console.Error.WriteLine("Unhandled card retrieval status code: " + result.StatusCode);
                    throw new Exception("Error getting card.");
            }

            return result.Content; //result.Content has our Card object
        }

        static string GetAssignmentNameForIndex(int index) {
            return SUB_ASSIGNMENT_NAME_PREFIX + index;
        }
        static async Task CreateSubAssignments() {
            for(int i = 0; i < NUM_SUB_ASSIGNMENTS; i += 1) {
                var assignmentId = await CreateSubAssignment(GetAssignmentNameForIndex(i), i);
                SubAssignmentIds.Add(assignmentId);
            }
        }

        static async Task<string> CreateSubAssignment(string assignmentName, int assignmentIndex) {

            //Create the assignment body
            var assignment = new AssignmentNew
            {
                Description = "This is " + assignmentName + " created by the SDK Assignment Handoff example.",
                IntegrationId = SkyManager.IntegrationId,
                Name = assignmentName
            };

            //Create a sequence -- theoretically, this would be better placed in another function
            //We have placed this inline within this function for clarity in this example
            var sequenceOne = new SequenceNew
            {
                Id = ROOT_SEQUENCE_ID,
                ViewMode = ViewMode.Native //This is the default view mode and will generally be used
            };

            var decisionComponent = new ComponentDecision(){
                MaxSelected = 1,
                Mutable = true,
                IncludeCapture = true
            };
            decisionComponent.Choices = new Dictionary<string, Choice>();
            decisionComponent.Choices.Add("0", new Choice(){
                Label = "Purple"
                , Position = 1
                , Selected = false
            });

            decisionComponent.Choices.Add("1", new Choice(){
                Label = "Blue"
                , Position = 2
                , Selected = false
            });

            decisionComponent.Choices.Add("2", new Choice(){
                Label = "Orange"
                , Position = 3
                , Selected = false
            });

            decisionComponent.Choices.Add("3", new Choice(){
                Label = "Other"
                , Position = 4
                , Selected = false
            });

            //Create a card for sequence1
            var sequenceOneCardOne = new CardNew
            {
                Label = "What is your favorite color?",
                Position = 1, //Position of cards is 1-indexed
                Size = 2, //Size can be 1, 2, or 3 and determines how much of the screen a card takes up (3 being fullscreen)
                Layout = new LayoutImage() {
                    Uri = "resource://image/ic_state_multiplechoice_01"
                },
                Selectable = true,
                Component = decisionComponent
            };

            //Create a card for sequence1
            var sequenceOneCardTwo = new CardNew
            {
                Footer = "Select this card to check this assignment back in.",
                Id = CHECKIN_CARD_ID_PREFIX + CARD_ID_DELIMITER + assignmentIndex, //As long as the ID is unique within the sequence, we're good to go
                Label = "Check in assignment",
                Position = 1, //Position of cards is 1-indexed
                Size = 2, //Size can be 1, 2, or 3 and determines how much of the screen a card takes up (3 being fullscreen)
                Layout = new LayoutText(),
                Selectable = true,
                Component = new ComponentCompletion() {   
                    Done = new DoneOnSelect()
                }
            };

            //Set the card to live in sequence1. We could create more cards and add them in a similar manner
            sequenceOne.Cards = new System.Collections.Generic.List<CardNew>
            {
                sequenceOneCardOne
                , sequenceOneCardTwo
            };

            //Add the sequence to the assignment
            assignment.Sequences = new System.Collections.Generic.List<SequenceNew>
            {
                sequenceOne
            };

            //Set the sequence to be the root sequence. This is especially important if we have more than one sequence
            assignment.RootSequence = sequenceOne.Id;

            //Create the request for the assignment creation API
            var request = new Skylight.Api.Assignments.V1.AssignmentRequests.CreateAssignmentRequest(assignment);

            //Now, the magic happens -- we make a single API call to create this assignment, sequences/cards and all.
            var result = await SkyManager.ApiClient.ExecuteRequestAsync(request);
            
            //Handle the resulting status code appropriately
            switch(result.StatusCode) {
                case System.Net.HttpStatusCode.Forbidden:
                    Console.Error.WriteLine("Error creating assignment: Permission forbidden.");
                    break;
                case System.Net.HttpStatusCode.Unauthorized:
                    Console.Error.WriteLine("Error creating assignment: Method call was unauthenticated.");
                    break;
                case System.Net.HttpStatusCode.Created:
                    Console.WriteLine("Assignment successfully created.");
                    break;
                default:
                    Console.Error.WriteLine("Unhandled assignment creation status code: " + result.StatusCode);
                    break;
            }

            return result.Content.Id;
        }
        
        static async Task AssignSubAssignment(int assignmentIndex, string userId){
            var assignmentPatch = new AssignmentPatch();
            assignmentPatch.Add("assignedTo", userId);
            
            var assignmentPatchRequest = new Skylight.Api.Assignments.V1.AssignmentRequests.PatchAssignmentRequest(assignmentPatch, SubAssignmentIds[assignmentIndex]);

            //Now, the magic happens -- we make a single API call to create this assignment, sequences/cards and all.
            var result = await SkyManager.ApiClient.ExecuteRequestAsync(assignmentPatchRequest);
            
            //Handle the resulting status code appropriately
            switch(result.StatusCode) {
                case System.Net.HttpStatusCode.Forbidden:
                    Console.Error.WriteLine("Error patching assignment: Permission forbidden.");
                    break;
                case System.Net.HttpStatusCode.Unauthorized:
                    Console.Error.WriteLine("Error patching assignment: Method call was unauthenticated.");
                    break;
                case System.Net.HttpStatusCode.OK:
                    Console.WriteLine("Assignment successfully patched to assign.");
                    break;
                default:
                    Console.Error.WriteLine("Unhandled assignment patch status code: " + result.StatusCode);
                    break;
            }

        }

        
        static async Task UnassignSubAssignment(int assignmentIndex){
            var assignmentPatch = new AssignmentPatch();
            assignmentPatch.Add("assignedTo", "");
            
            var assignmentPatchRequest = new Skylight.Api.Assignments.V1.AssignmentRequests.PatchAssignmentRequest(assignmentPatch, SubAssignmentIds[assignmentIndex]);

            //Now, the magic happens -- we make a single API call to create this assignment, sequences/cards and all.
            var result = await SkyManager.ApiClient.ExecuteRequestAsync(assignmentPatchRequest);
            
            //Handle the resulting status code appropriately
            switch(result.StatusCode) {
                case System.Net.HttpStatusCode.Forbidden:
                    Console.Error.WriteLine("Error patching assignment: Permission forbidden.");
                    break;
                case System.Net.HttpStatusCode.Unauthorized:
                    Console.Error.WriteLine("Error patching assignment: Method call was unauthenticated.");
                    break;
                case System.Net.HttpStatusCode.OK:
                    Console.WriteLine("Assignment successfully patched to unassign.");
                    break;
                default:
                    Console.Error.WriteLine("Unhandled assignment patch status code: " + result.StatusCode);
                    break;
            }

        }

        static async Task CreateSuperAssignments() {
            var groupId = await GetGroupIdForGroupname(TEST_ACCOUNT_GROUP);
            var group = await GetGroupById(groupId);
            foreach(var member in group.Members) {
                //Remove all of the user's assignments to clean up for this example
                var userId = member.Id;
                await RemoveAllAssignmentsForUser(userId);
                await CreateSuperAssignment(userId);
            }
        }
        static async Task CreateSuperAssignment(string userId) {
            //Create the assignment body
            var assignment = new AssignmentNew
            {
                AssignedTo = userId,
                Description = "This is an assignment created by the SDK example.",
                IntegrationId = SkyManager.IntegrationId,
                Name = SUPER_ASSIGNMENT_NAME
            };

            //Create a sequence -- theoretically, this would be better placed in another function
            //We have placed this inline within this function for clarity in this example
            var sequenceOne = new SequenceNew
            {
                Id = ROOT_SEQUENCE_ID,
                ViewMode = ViewMode.Native //This is the default view mode and will generally be used
            };

            var sequenceCards = new System.Collections.Generic.List<CardNew>();

            for(var i = 0; i < NUM_SUB_ASSIGNMENTS; i += 1) {
                //Create a card for sequence1
                var sequenceOneCard = new CardNew
                {
                    Footer = "Select to check out this assignment",
                    Id = CHECKOUT_CARD_ID_PREFIX + CARD_ID_DELIMITER + i, //As long as the ID is unique within the sequence, we're good to go
                    Label = "Check out " + i,
                    Position = 1, //Position of cards is 1-indexed
                    Size = 1, //Size can be 1, 2, or 3 and determines how much of the screen a card takes up (3 being fullscreen)
                    Layout = new LayoutText(),
                    Selectable = true,
                    Component = new ComponentCompletion() {   
                        Done = new DoneOnSelect()
                    }
                };
                sequenceCards.Add(sequenceOneCard);
            }


            //Set the card to live in sequence1. We could create more cards and add them in a similar manner
            sequenceOne.Cards = sequenceCards;

            //Add the sequence to the assignment
            assignment.Sequences = new System.Collections.Generic.List<SequenceNew>
            {
                sequenceOne
            };

            //Set the sequence to be the root sequence. This is especially important if we have more than one sequence
            assignment.RootSequence = sequenceOne.Id;

            //Create the request for the assignment creation API
            var request = new Skylight.Api.Assignments.V1.AssignmentRequests.CreateAssignmentRequest(assignment);

            //Now, the magic happens -- we make a single API call to create this assignment, sequences/cards and all.
            var result = await SkyManager.ApiClient.ExecuteRequestAsync(request);
            
            //Handle the resulting status code appropriately
            switch(result.StatusCode) {
                case System.Net.HttpStatusCode.Forbidden:
                    Console.Error.WriteLine("Error creating assignment: Permission forbidden.");
                    break;
                case System.Net.HttpStatusCode.Unauthorized:
                    Console.Error.WriteLine("Error creating assignment: Method call was unauthenticated.");
                    break;
                case System.Net.HttpStatusCode.Created:
                    Console.WriteLine("Assignment successfully created.");
                    break;
                default:
                    Console.Error.WriteLine("Unhandled assignment creation status code: " + result.StatusCode);
                    break;
            }

            SuperAssignmentIds.Add(result.Content.Id);
        }

        static async Task<string> GetUserIdForUsername(string username) {
            //Create an API request for retrieving all users
            var getUsersRequest = new Skylight.Api.Authentication.V1.UsersRequests.GetUsersRequest();

            //Execute the API request
            var result = await SkyManager.ApiClient.ExecuteRequestAsync(getUsersRequest);

            //The users will be stored as a list in the result's Content, so we can iterate through them
            foreach(var user in result.Content) {
                if(user.Username == username)return user.Id;
            }
            return null;
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
        
        static async Task RemoveAllAssignmentsForUser(string userId) {
            //First, get a list of all the user's assignments.
            var assignmentsRequest = new Skylight.Api.Assignments.V1.AssignmentRequests.GetAssignmentsRequest();

            //Make sure we only get assignments for our user
            assignmentsRequest.AddUserIdsQuery(userId);

            var result = await SkyManager.ApiClient.ExecuteRequestAsync(assignmentsRequest);
            
            //Handle the resulting status code appropriately
            switch(result.StatusCode) {
                case System.Net.HttpStatusCode.Forbidden:
                    Console.Error.WriteLine("Error retrieving assignments for user: Permission forbidden.");
                    throw new Exception("Error retrieving assignments for user.");
                case System.Net.HttpStatusCode.Unauthorized:
                    Console.Error.WriteLine("Error retrieving assignments for user: Method call was unauthenticated.");
                    throw new Exception("Error retrieving assignments for user.");
                case System.Net.HttpStatusCode.NotFound:
                    Console.Error.WriteLine("Error retrieving assignments for user: User not found.");
                    throw new Exception("Error retrieving assignments for user.");
                case System.Net.HttpStatusCode.OK:
                    Console.WriteLine("User assignments successfully retrieved");
                    break;
                default:
                    Console.Error.WriteLine("Unhandled user creation status code: " + result.StatusCode);
                    throw new Exception("Error retrieving assignments for user.");
            }

            foreach(var assignment in result.Content) {
                await DeleteAssignment(assignment.Id);
            }
        }

        static async Task DeleteAssignment(string assignmentId) {
            
            //@skydocs.start(assignments.delete)
            var result = await SkyManager.ApiClient.ExecuteRequestAsync(new Skylight.Api.Assignments.V1.AssignmentRequests.DeleteAssignmentRequest(assignmentId));
            
            //Handle the resulting status code appropriately
            switch(result.StatusCode) {
                case System.Net.HttpStatusCode.Forbidden:
                    Console.Error.WriteLine("Error deleting assignment: Permission forbidden.");
                    throw new Exception("Error deleting assignment.");
                case System.Net.HttpStatusCode.Unauthorized:
                    Console.Error.WriteLine("Error deleting assignment: Method call was unauthenticated.");
                    throw new Exception("Error deleting assignment.");
                case System.Net.HttpStatusCode.NotFound:
                    Console.Error.WriteLine("Error deleting assignment: Assignment not found.");
                    throw new Exception("Error deleting assignment.");
                case System.Net.HttpStatusCode.OK:
                    Console.WriteLine("Assignment successfully deleted");
                    break;
                default:
                    Console.Error.WriteLine("Unhandled user creation status code: " + result.StatusCode);
                    throw new Exception("Error deleting assignment.");
            }
            //@skydocs.end()
        }
    }
}
