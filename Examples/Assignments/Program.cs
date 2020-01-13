
using System.IO;
using System;
using Microsoft.Extensions.Configuration;
using Skylight.Client;
using System.Threading.Tasks;
using Skylight.Sdk;

namespace Assignments
{
    class Program
    {
        public static Manager Manager;
        static async Task Main(string[] args)
        {
            try {
                //@skydocs.start(authentication.login)
                /*
                    Create our manager by passing it the path to our credentials.json file.
                    This json file has our API credentials copy-pasted from Skylight Web.
                    The path is also optional; the constructor for the SDK's manager can also take 0 arguments, in which case it will search for a file called `credentials.json` in the root directory of the extension.
                */
                Manager = new Manager(Path.Combine("..", "..", "credentials.json"));
                //@skydocs.end()
            } catch { return; }

            await CreateBulkAssignment();
        }

        static async Task CreateBulkAssignment() {

            //@skydocs.start(assignments.bulkcreate)
            //Retrieve the user to whom we'd like to assign this assignment
            var assignUser = await GetUserIdForUsername("api.test.user");
            if(assignUser == null) {
                Console.Error.WriteLine("User does not exist for bulk assignment creation");
                return;
            }

            //Create the assignment body
            var assignment = new Skylight.Api.Assignments.V1.Models.AssignmentNew
            {
                AssignedTo = assignUser,
                Description = "This is an assignment created by the SDK example.",
                IntegrationId = Manager.IntegrationId,
                Name = "SDK Example Assignment"
            };

            //Create a sequence -- theoretically, this would be better placed in another function
            //We have placed this inline within this function for clarity in this example
            var sequenceOne = new Skylight.Api.Assignments.V1.Models.SequenceNew
            {
                Id = "sequence1",
                ViewMode = Skylight.Api.Assignments.V1.Models.ViewMode.Native //This is the default view mode and will generally be used
            };

            //Create a card for sequence1
            var sequenceOneCardOne = new Skylight.Api.Assignments.V1.Models.CardNew
            {
                Header = "Card One Header",
                Footer = "Card One Footer",
                Id = "card1",
                Label = "Card One",
                Position = 1, //Position of cards is 1-indexed
                Size = 1, //Size can be 1, 2, or 3 and determines how much of the screen a card takes up (3 being fullscreen)
                Layout = new Skylight.Api.Assignments.V1.Models.LayoutText()
            };

            //Set the card to live in sequence1. We could create more cards and add them in a similar manner
            sequenceOne.Cards = new System.Collections.Generic.List<Skylight.Api.Assignments.V1.Models.CardNew>
            {
                sequenceOneCardOne
            };

            //Create another sequence
            var sequenceTwo = new Skylight.Api.Assignments.V1.Models.SequenceNew
            {
                Id = "sequence2",
                ViewMode = Skylight.Api.Assignments.V1.Models.ViewMode.Native
            };

            //Create a card for sequence2
            var sequenceTwoCardOne = new Skylight.Api.Assignments.V1.Models.CardNew
            {
                Id = "card1", //As this card lives in a different sequence, it can share the same ID as the card created earlier
                Label = "Card One",
                Position = 1,
                Size = 1,
                Layout = new Skylight.Api.Assignments.V1.Models.LayoutText()
            };

            //Add the card to the second sequence
            sequenceTwo.Cards = new System.Collections.Generic.List<Skylight.Api.Assignments.V1.Models.CardNew>
            {
                sequenceTwoCardOne
            };

            //Add both sequences to the assignment
            assignment.Sequences = new System.Collections.Generic.List<Skylight.Api.Assignments.V1.Models.SequenceNew>
            {
                sequenceOne,
                sequenceTwo
            };

            //Set the first sequence to be the root sequence
            assignment.RootSequence = sequenceOne.Id;

            //Create the request for the assignment creation API
            var request = new Skylight.Api.Assignments.V1.AssignmentRequests.CreateAssignmentRequest(assignment);

            //Now, the magic happens -- we make a single API call to create this assignment, sequences/cards and all.
            var result = await Manager.ApiClient.ExecuteRequestAsync(request);
            
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
            var result = await Manager.ApiClient.ExecuteRequestAsync(getUsersRequest);

            //The users will be stored as a list in the result's Content, so we can iterate through them
            foreach(var user in result.Content) {
                if(user.Username == username)return user.Id;
            }
            return null;
            //@skydocs.end()
        }
    }
}
