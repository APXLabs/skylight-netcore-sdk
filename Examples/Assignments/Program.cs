
using System.Runtime.InteropServices;
using System.IO;
using System;
using Microsoft.Extensions.Configuration;
using Skylight.Client;
using System.Threading.Tasks;
using Skylight.Sdk;
using Skylight.Mqtt;
using Skylight.Api.Assignments.V1.Models;
using System.Threading;

namespace Assignments
{
    class Program
    {
        public static Manager SkyManager;
        private static readonly string TEST_ACCOUNT_USERNAME = "hello.world";
        private static readonly string ROOT_SEQUENCE_ID = "rootSequence";
        private const string CREATE_CARD_ID = "createCard";
        private const string DELETE_CARD_ID = "deleteCard";
        private static int numSequencesCreated = 0;
        static async Task Main(string[] args)
        {
            try {
                //@skydocs.start(authentication.login)
                /*
                    Create our manager by passing it the path to our credentials.json file.
                    This json file has our API credentials copy-pasted from Skylight Web.
                    The path is also optional; the constructor for the SDK's manager can also take 0 arguments, in which case it will search for a file called `credentials.json` in the root directory of the extension.
                */
                SkyManager = new Manager(Path.Combine("..", "..", "credentials.json"));
                //@skydocs.end()
            } catch { return; }

            //We'll have a simple event listener that listens for the user requesting the creation/deletion of cards and sequences
            SkyManager.MessagingClient.CardUpdated += async (object sender, CardUpdatedEventArgs args) => { await CardUpdated(sender, args);};
            await SkyManager.StartListening();

            //Remove all of the user's assignments to clean up for this example
            var userId = await GetUserIdForUsername(TEST_ACCOUNT_USERNAME);
            await RemoveAllAssignmentsForUser(userId);

            //Let's create the assignment.
            await CreateBulkAssignment();
            
            //Wait forever (at least, until the program is stopped)
            SpinWait.SpinUntil(() => false);
        }

        static async Task CardUpdated(object sender, CardUpdatedEventArgs args) {

            var shouldResetCard = false;

            switch(args.CardId) {
                case CREATE_CARD_ID:
                    await CreateCardSequence(args.AssignmentId, args.SequenceId);
                    shouldResetCard = true;
                    break;
                case DELETE_CARD_ID:
                    await DeleteCardSequence(args.AssignmentId, args.SequenceId);
                    break;
            }

            //Only progress forward if we're looking to reset the card
            if(!shouldResetCard)return;
            
            Card card = await GetCard(args.AssignmentId, args.SequenceId, args.CardId);
            Console.WriteLine(card.Component.ComponentType);
            
            //Make sure we're working with the correct card type
            if(card.Component.ComponentType != ComponentType.Completion)return;
            Console.WriteLine("Resetting card");

            //Mark the card as not done, to allow the user to select the card again.
            await ResetCard(card);
        }

        static async Task ResetCard(Card card) {
            
            //@skydocs.start(cards.update)
            var cardPatch = new CardPatch();
            cardPatch.Add("isDone", false); //Important note: Make sure that the keys (e.g. isDone) are lower case
            cardPatch.Add("component", new ComponentCompletion() { Completed = false });
            
            var cardUpdateBody = new Skylight.Api.Assignments.V1.CardRequests.UpdateCardPatchRequest(cardPatch, card.AssignmentId, card.SequenceId, card.Id);
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
            //@skydocs.end()

        }

        static async Task CreateCardSequence(string assignmentId, string sequenceId) {
            var newSequenceId = "sequence"+ numSequencesCreated;

            //@skydocs.start(sequences.create)
            var newSequence = new SequenceNew() {
                Id = newSequenceId
            };

            var newSequenceCard = new CardNew() {
                Footer = "Select to delete this sequence",
                Id = DELETE_CARD_ID, //As long as the ID is unique within the sequence, we're good to go
                Label = "Delete Sequence",
                Position = 1, //Position of cards is 1-indexed
                Size = 2, //Size can be 1, 2, or 3 and determines how much of the screen a card takes up (3 being fullscreen)
                Layout = new LayoutText(),
                Selectable = true,
                Component = new ComponentCompletion() {   
                    Done = new DoneOnSelect()
                }
            };

            newSequence.Cards = new System.Collections.Generic.List<CardNew> {
                newSequenceCard
            };

            //Create our sequence
            var sequenceCreateResult = await SkyManager.ApiClient.ExecuteRequestAsync(new Skylight.Api.Assignments.V1.SequenceRequests.CreateSequenceRequest(new System.Collections.Generic.List<SequenceNew>{ newSequence }, assignmentId));

            //Handle the resulting status code appropriately
            switch(sequenceCreateResult.StatusCode) {
                case System.Net.HttpStatusCode.Forbidden:
                    Console.Error.WriteLine("Error creating sequence: Permission forbidden.");
                    throw new Exception("Error creating sequence.");
                case System.Net.HttpStatusCode.Unauthorized:
                    Console.Error.WriteLine("Error creating sequence: Method call was unauthenticated.");
                    throw new Exception("Error creating sequence.");
                case System.Net.HttpStatusCode.NotFound:
                    Console.Error.WriteLine("Error creating sequence: Assignment not found.");
                    throw new Exception("Error creating sequence.");
                case System.Net.HttpStatusCode.Created:
                    Console.WriteLine("Successfully created sequence.");
                    break;
                default:
                    Console.Error.WriteLine("Unhandled sequence creation status code: " + sequenceCreateResult.StatusCode);
                    throw new Exception("Error creating sequence.");
            }
            //@skydocs.end()

            //Get our current sequence's cards information so we can add a new card to it
            var sequenceInfoRequest = await SkyManager.ApiClient.ExecuteRequestAsync(new Skylight.Api.Assignments.V1.CardRequests.GetCardListRequest(assignmentId, sequenceId));

            //Handle the resulting status code appropriately
            switch(sequenceInfoRequest.StatusCode) {
                case System.Net.HttpStatusCode.Forbidden:
                    Console.Error.WriteLine("Error retrieving sequence: Permission forbidden.");
                    throw new Exception("Error creating sequence.");
                case System.Net.HttpStatusCode.Unauthorized:
                    Console.Error.WriteLine("Error retrieving sequence: Method call was unauthenticated.");
                    throw new Exception("Error retrieving sequence.");
                case System.Net.HttpStatusCode.NotFound:
                    Console.Error.WriteLine("Error retrieving sequence: Assignment or sequence not found.");
                    throw new Exception("Error retrieving sequence.");
                case System.Net.HttpStatusCode.OK:
                    Console.WriteLine("Successfully retrieved sequence.");
                    break;
                default:
                    Console.Error.WriteLine("Unhandled sequence retrieval status code: " + sequenceInfoRequest.StatusCode);
                    throw new Exception("Error retrieving sequence.");
            }

            var numCardsInSequence = sequenceInfoRequest.Content.Count;

            //@skydocs.start(cards.create)
            var openSequenceCard = new CardNew() {
                Footer = "Enter " + newSequenceId,
                Id = "card"+numSequencesCreated, //As long as the ID is unique within the sequence, we're good to go
                Label = "Sequence " + numSequencesCreated,
                Position = numCardsInSequence + 1, //Position of cards is 1-indexed
                Size = 1, //Size can be 1, 2, or 3 and determines how much of the screen a card takes up (3 being fullscreen)
                Layout = new LayoutText(),
                Selectable = true,
                Component = new ComponentOpenSequence() {   
                    SequenceId = newSequenceId
                }
            };

            var cardCreateResult = await SkyManager.ApiClient.ExecuteRequestAsync(new Skylight.Api.Assignments.V1.CardRequests.CreateCardsRequest(new System.Collections.Generic.List<CardNew>{ openSequenceCard }, assignmentId, ROOT_SEQUENCE_ID));

            //Handle the resulting status code appropriately
            switch(cardCreateResult.StatusCode) {
                case System.Net.HttpStatusCode.Forbidden:
                    Console.Error.WriteLine("Error creating card: Permission forbidden.");
                    throw new Exception("Error creating card.");
                case System.Net.HttpStatusCode.Unauthorized:
                    Console.Error.WriteLine("Error creating card: Method call was unauthenticated.");
                    throw new Exception("Error creating card.");
                case System.Net.HttpStatusCode.NotFound:
                    Console.Error.WriteLine("Error creating card: Assignment or sequence not found.");
                    throw new Exception("Error creating card.");
                case System.Net.HttpStatusCode.Created:
                    Console.WriteLine("Successfully created card.");
                    break;
                default:
                    Console.Error.WriteLine("Unhandled card creation status code: " + cardCreateResult.StatusCode);
                    throw new Exception("Error creating card.");
            }
            //@skydocs.end()

            //Increment how many sequences we've created
            numSequencesCreated += 1;
        }

        static async Task DeleteCardSequence(string assignmentId, string sequenceId) {
            //First, delete the openSequence card that points to this sequence
            
            //Get the root sequence's cards to see which one points to this sequence. Theoretically we could also delete the proper card based on the sequenceId.
            var sequenceInfoRequest = await SkyManager.ApiClient.ExecuteRequestAsync(new Skylight.Api.Assignments.V1.CardRequests.GetCardListRequest(assignmentId, ROOT_SEQUENCE_ID));

            //Handle the resulting status code appropriately
            switch(sequenceInfoRequest.StatusCode) {
                case System.Net.HttpStatusCode.Forbidden:
                    Console.Error.WriteLine("Error retrieving sequence: Permission forbidden.");
                    throw new Exception("Error creating sequence.");
                case System.Net.HttpStatusCode.Unauthorized:
                    Console.Error.WriteLine("Error retrieving sequence: Method call was unauthenticated.");
                    throw new Exception("Error retrieving sequence.");
                case System.Net.HttpStatusCode.NotFound:
                    Console.Error.WriteLine("Error retrieving sequence: Assignment or sequence not found.");
                    throw new Exception("Error retrieving sequence.");
                case System.Net.HttpStatusCode.OK:
                    Console.WriteLine("Successfully retrieved sequence.");
                    break;
                default:
                    Console.Error.WriteLine("Unhandled sequence retrieval status code: " + sequenceInfoRequest.StatusCode);
                    throw new Exception("Error retrieving sequence.");
            }

            var deleteCardId = "";

            foreach(var card in sequenceInfoRequest.Content) {
                if(card.Component.ComponentType != ComponentType.OpenSequence)continue;
                if(((ComponentOpenSequence)card.Component).SequenceId == sequenceId) deleteCardId = card.Id;
            }

            if(!String.IsNullOrEmpty(deleteCardId)){
                //@skydocs.start(cards.delete)
                var deleteCardRequest = await SkyManager.ApiClient.ExecuteRequestAsync(new Skylight.Api.Assignments.V1.CardRequests.DeleteCardRequest(assignmentId, ROOT_SEQUENCE_ID, deleteCardId));
                
                //Handle the resulting status code appropriately
                switch(deleteCardRequest.StatusCode) {
                    case System.Net.HttpStatusCode.Forbidden:
                        Console.Error.WriteLine("Error deleting card: Permission forbidden.");
                        throw new Exception("Error deleting card.");
                    case System.Net.HttpStatusCode.Unauthorized:
                        Console.Error.WriteLine("Error deleting card: Method call was unauthenticated.");
                        throw new Exception("Error deleting card.");
                    case System.Net.HttpStatusCode.NotFound:
                        Console.Error.WriteLine("Error deleting card: Assignment, sequence, or card not found.");
                        throw new Exception("Error deleting card.");
                    case System.Net.HttpStatusCode.OK:
                        Console.WriteLine("Successfully deleted card.");
                        break;
                    default:
                        Console.Error.WriteLine("Unhandled card deletion status code: " + deleteCardRequest.StatusCode);
                        throw new Exception("Error deleting card.");
                }
                //@skydocs.end()
            }

            
            //@skydocs.start(sequences.delete)
            //Then, delete the sequence. This will also delete the card that's in the sequence.
            var deleteSequenceRequest = await SkyManager.ApiClient.ExecuteRequestAsync(new Skylight.Api.Assignments.V1.SequenceRequests.DeleteSequenceRequest(assignmentId, sequenceId));

            //Handle the resulting status code appropriately
            switch(deleteSequenceRequest.StatusCode) {
                case System.Net.HttpStatusCode.Forbidden:
                    Console.Error.WriteLine("Error deleting sequence: Permission forbidden.");
                    throw new Exception("Error deleting sequence.");
                case System.Net.HttpStatusCode.Unauthorized:
                    Console.Error.WriteLine("Error deleting sequence: Method call was unauthenticated.");
                    throw new Exception("Error deleting sequence.");
                case System.Net.HttpStatusCode.NotFound:
                    Console.Error.WriteLine("Error deleting sequence: Assignment or sequence not found.");
                    throw new Exception("Error deleting sequence.");
                case System.Net.HttpStatusCode.OK:
                    Console.WriteLine("Successfully deleted sequence.");
                    break;
                default:
                    Console.Error.WriteLine("Unhandled sequence deletion status code: " + deleteSequenceRequest.StatusCode);
                    throw new Exception("Error deleting sequence.");
            }
            
            //@skydocs.end()
        }

        static async Task<Card> GetCard(string assignmentId, string sequenceId, string cardId) {
            //@skydocs.start(cards.get)
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
                    Console.Error.WriteLine("Error retrieving card: User not found.");
                    throw new Exception("Error getting card.");
                case System.Net.HttpStatusCode.OK:
                    Console.WriteLine("Successfully retrieved card.");
                    break;
                default:
                    Console.Error.WriteLine("Unhandled card retrieval status code: " + result.StatusCode);
                    throw new Exception("Error getting card.");
            }

            return result.Content; //result.Content has our Card object
            //@skydocs.end()
        }

        static async Task CreateBulkAssignment() {
            //@skydocs.start(assignments.bulkcreate)
            //Retrieve the user to whom we'd like to assign this assignment
            var assignUser = await GetUserIdForUsername(TEST_ACCOUNT_USERNAME);
            if(assignUser == null) {
                Console.Error.WriteLine("User does not exist for bulk assignment creation");
                return;
            }

            //Create the assignment body
            var assignment = new AssignmentNew
            {
                AssignedTo = assignUser,
                Description = "This is an assignment created by the SDK example.",
                IntegrationId = SkyManager.IntegrationId,
                Name = "SDK Example Assignment"
            };

            //Create a sequence -- theoretically, this would be better placed in another function
            //We have placed this inline within this function for clarity in this example
            var sequenceOne = new SequenceNew
            {
                Id = ROOT_SEQUENCE_ID,
                ViewMode = ViewMode.Native //This is the default view mode and will generally be used
            };

            //Create a card for sequence1
            var sequenceOneCardOne = new CardNew
            {
                Footer = "Select to create a card that points to a new sequence",
                Id = CREATE_CARD_ID, //As long as the ID is unique within the sequence, we're good to go
                Label = "Append Card",
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
            //@skydocs.end()
        }

        static async Task<string> GetUserIdForUsername(string username) {
            //@skydocs.start(users.getbyname)
            //Create an API request for retrieving all users
            var getUsersRequest = new Skylight.Api.Authentication.V1.UsersRequests.GetUsersListRequest();

            //Execute the API request
            var result = await SkyManager.ApiClient.ExecuteRequestAsync(getUsersRequest);

            //The users will be stored as a list in the result's Content, so we can iterate through them
            foreach(var user in result.Content) {
                if(user.Username == username)return user.Id;
            }
            return null;
            //@skydocs.end()
        }

        
    static async Task RemoveAllAssignmentsForUser(string userId) {
        //First, get a list of all the user's assignments.
        var assignmentsRequest = new Skylight.Api.Assignments.V1.AssignmentRequests.GetAssignmentListRequest();

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
