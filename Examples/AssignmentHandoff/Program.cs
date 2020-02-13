
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
using System.Linq;

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
        private static readonly string WORKER_ACCOUNT_GROUP = "assignment.workers";
        private static readonly string COORDINATOR_ACCOUNT_GROUP = "assignment.coordinators";
        private static readonly string ROOT_SEQUENCE_ID = "rootSequence";
        private const string CARD_ID_DELIMITER = "-";
        private const string REVIEW_SECTION_CARD_ID = "reviewSection";
        private const string REVIEWED_SECTION_CARD_ID = "reviewedSection";
        private const string IN_PROGRESS_SECTION_CARD_ID = "inProgressSection";
        private const string CHECKOUT_CARD_ID_PREFIX = "checkin";
        private const string CHECKIN_CARD_ID_PREFIX = "checkout";
        private const string REVIEW_CARD_ID_PREFIX = "review";
        private const string INFO_CARD_ID_PREFIX = "info";
        private const string CREATE_ASSIGNMENTS_NAME = "Create Assignment";
        private const string COORDINATOR_VIEW_ASSIGNMENTS_NAME = "Review Assignments";
        private const string WORKER_VIEW_ASSIGNMENTS_NAME = "View Assignments";
        private const string SUB_ASSIGNMENT_NAME_PREFIX ="Assignment ";
        private static int CurrentAssignmentCount = 0;
        private static List<string> CoordinatorAssignmentIds = new List<string>();
        private static List<string> WorkerAssignmentIds = new List<string>();

        class HandoffAssignment {
            public string Id;
            public string AssignmentId;
            public string UserId;
            public string UserName;
            public float Progress = 0;
            public string Name;
            public string ReviewedBy;
            public bool Reviewed = false;

            public HandoffAssignment(string name, string id) {
                Name = name;
                Id = id;
            }
        }

        private static List<HandoffAssignment> Assignments = new List<HandoffAssignment>();

        static async Task Main(string[] args)
        {
            /*
                Create our manager by passing it the path to our credentials.json file.
                This json file has our API credentials copy-pasted from Skylight Web.
                The path is also optional; the constructor for the SDK's manager can also take 0 arguments, in which case it will search for a file called `credentials.json` in the root directory of the extension.
            */
            SkyManager = new Manager();

            //Connect to Skylight
            await SkyManager.Connect();
            Console.WriteLine("Skylight connected");

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

            //Create our local in-memory cache of assignments (in production, this would live in another system or database)
            Assignments.Add(new HandoffAssignment("Assignment A", CurrentAssignmentCount + ""));
            CurrentAssignmentCount += 1;

            Assignments.Add(new HandoffAssignment("Assignment B", CurrentAssignmentCount + ""));
            CurrentAssignmentCount += 1;

            Assignments.Add(new HandoffAssignment("Assignment C", CurrentAssignmentCount + ""));
            CurrentAssignmentCount += 1;
            

            await CreateCoordinatorAssignments();
            await CreateWorkerAssignments();

            //Create the low-level assignments
            await CreateSubAssignments();
            
            //Wait forever (at least, until the program is stopped)
            SpinWait.SpinUntil(() => false);
        }

        static async Task CardUpdated(string cardId, string sequenceId, string assignmentId) {
            //Handle the card update event based on the card's id
            var shouldResetCard = false;
            var cardSplit = cardId.Split(CARD_ID_DELIMITER);
            if(cardSplit.Length < 2) return;
            var cardAction = cardSplit[0];
            var handoffAssignmentId = cardSplit[1];
            var handoffAssignment = Assignments.Find((x) => { return x.Id == handoffAssignmentId; });
            Assignment assignment;
            string userId;
            switch(cardAction){
                case CHECKIN_CARD_ID_PREFIX:
                    shouldResetCard = true;
                    await UnassignSubAssignment(handoffAssignment);
                    await UpdateWorkerAssignments();
                    await UpdateCoordinatorAssignments();
                    break;
                case CHECKOUT_CARD_ID_PREFIX:
                case INFO_CARD_ID_PREFIX:
                    assignment = await GetAssignment(assignmentId);
                    userId = assignment.AssignedTo;
                    await AssignSubAssignment(handoffAssignment, userId);
                    await UpdateWorkerAssignments();
                    await UpdateCoordinatorAssignments();
                    break;
                case REVIEW_CARD_ID_PREFIX:
                    handoffAssignment.Reviewed = true;
                    assignment = await GetAssignment(assignmentId);
                    userId = assignment.AssignedTo;
                    var userResult = await SkyManager.ApiClient.ExecuteRequestAsync(new Skylight.Api.Authentication.V1.UsersRequests.GetUserRequest(userId));
                    handoffAssignment.ReviewedBy = userResult.Content.Username;
                    
                    await UnassignSubAssignment(handoffAssignment);
                    await UpdateWorkerAssignments();
                    await UpdateCoordinatorAssignments();
                    break;
            }

            //Only progress forward if we're looking to reset the card
            if(!shouldResetCard)return;
            
            Card card = await GetCard(assignmentId, sequenceId, cardId);
            
            //Make sure we're working with the correct card type
            if(card.Component.ComponentType != ComponentType.Completion)return;
            Console.WriteLine("Resetting card");

            //Mark the card as not done, to allow the user to select the card again.
            await ResetCard(card);
        }

        static async Task UpdateWorkerAssignments() {
            //Sort our assignments for how they appear to workers
            Assignments.Sort((assignment1, assignment2) => { 
                
                //If one is reviewed and the other isn't, we have a clear winner
                if(assignment1.Reviewed && !assignment2.Reviewed) return 1;
                if(!assignment1.Reviewed && assignment2.Reviewed) return -1;
                
                //If one is up for review
                if(assignment1.Progress >= 1 && assignment2.Progress < 1) return 1;
                if(assignment2.Progress >= 1 && assignment1.Progress < 1) return -1;

                //If one is in progress and the other isn't
                if(!String.IsNullOrEmpty(assignment1.UserId) && String.IsNullOrEmpty(assignment2.UserId)) return 1;
                if(String.IsNullOrEmpty(assignment1.UserId) && !String.IsNullOrEmpty(assignment2.UserId)) return -1;

                //If the progress of one is greater
                if(assignment1.Progress > assignment2.Progress) return -1;
                if(assignment2.Progress > assignment1.Progress) return 1;

                //Otherwise sort by name
                return assignment1.Name.CompareTo(assignment2.Name);
            });

            List<string> assignmentIdsInOrder = Assignments.Select((a) => a.Id).ToList();


            //We'll go through the workers' "View Assignments" assignments and reorder based on progress and if it's currently being worked on
            foreach(var assignmentId in WorkerAssignmentIds) {
                var sequenceCardsResponse = await SkyManager.ApiClient.ExecuteRequestAsync(new Skylight.Api.Assignments.V1.CardRequests.GetSequenceCardsRequest(assignmentId, ROOT_SEQUENCE_ID));
                var sequenceCards = sequenceCardsResponse.Content;
                foreach(var card in sequenceCards) {
                    var cardPatch = new CardPatch();
                    var cardIdSplit = card.Id.Split(CARD_ID_DELIMITER);
                    if(cardIdSplit.Length != 2)continue;
                    var cardAssignmentId = cardIdSplit[1];
                    var assignmentIndex = assignmentIdsInOrder.IndexOf(cardAssignmentId);
                    cardPatch.Add("position", assignmentIndex+1);
                    var handoffAssignment = Assignments[assignmentIndex];

                    var layoutText = "";
                    cardPatch.Add("label", handoffAssignment.Name);

                    if(handoffAssignment.Reviewed) {
                        layoutText = "Reviewed by " + handoffAssignment.ReviewedBy;
                        cardPatch.Add("footer", "This assignment has been reviewed.");
                        cardPatch.Add("subdued", true);
                        cardPatch.Add("component", null);
                    }

                    else if(handoffAssignment.Progress >= 1) {
                        layoutText = "Waiting for review";
                        cardPatch.Add("footer", "This assignment is up for review.");
                        cardPatch.Add("subdued", true);
                        cardPatch.Add("component", null);
                    }
                    
                    else if(!String.IsNullOrEmpty(handoffAssignment.UserId)) {
                        cardPatch.Add("footer", "This assignment is in progress.");
                        layoutText = "Assigned to " + handoffAssignment.UserName;
                        cardPatch.Add("subdued", true);
                        cardPatch.Add("component", null);
                    }
                    
                    else {
                        layoutText = Math.Round(handoffAssignment.Progress * 100) + "% Complete";
                        cardPatch.Add("subdued", false);
                        cardPatch.Add("footer", "Select to checkout this assignment.");
                        cardPatch.Add("isDone", false);
                        cardPatch.Add("component", new ComponentCompletion() { Completed = false, Done = new DoneOnSelect() });
                    }

                    cardPatch.Add("layout", new LayoutText() {
                        TextSize = TextSize.Small
                        , Text = layoutText
                        , Alignment = Alignment.Center
                    });


        
                    await SkyManager.ApiClient.ExecuteRequestAsync(new Skylight.Api.Assignments.V1.CardRequests.PatchCardRequest(cardPatch, assignmentId, ROOT_SEQUENCE_ID, card.Id));
                    
                }


            }
        }

        static async Task UpdateCoordinatorAssignments() {
            //Sort our assignments for how they appear to coordinators
            Assignments.Sort((assignment1, assignment2) => { 
                
                //If one is reviewed and the other isn't, we have a clear winner
                if(assignment1.Reviewed && !assignment2.Reviewed) return 1;
                if(!assignment1.Reviewed && assignment2.Reviewed) return -1;

                //If they're both up for review, the rules change a tad
                if(assignment1.Progress >= 1 && assignment2.Progress >= 1) {

                    //If one is in progress and the other isn't
                    if(!String.IsNullOrEmpty(assignment1.UserId) && String.IsNullOrEmpty(assignment2.UserId)) return 1;
                    if(String.IsNullOrEmpty(assignment1.UserId) && !String.IsNullOrEmpty(assignment2.UserId)) return -1;
                }

                //If one is in progress and the other isn't
                if(!String.IsNullOrEmpty(assignment1.UserId) && String.IsNullOrEmpty(assignment2.UserId)) return -1;
                if(String.IsNullOrEmpty(assignment1.UserId) && !String.IsNullOrEmpty(assignment2.UserId)) return 1;

                //If the the progress of one is greater
                if(assignment1.Progress > assignment2.Progress) return -1;
                if(assignment2.Progress > assignment1.Progress) return 1;

                //Otherwise sort by id
                return assignment1.Name.CompareTo(assignment2.Name);
            });

            List<string> assignmentIdsInOrder = Assignments.Select((a) => a.Id).ToList();

            int numReadyForReview = Assignments.FindAll((a) => { return !a.Reviewed && a.Progress >= 1; }).Count;
            int numReviewed = Assignments.FindAll((a) => { return a.Reviewed; }).Count;
            int numInProgress = Assignments.Count - numReadyForReview - numReviewed;


            //We'll go through the workers' "View Assignments" assignments and reorder based on progress and if it's currently being worked on
            foreach(var assignmentId in CoordinatorAssignmentIds) {
                var sequenceCardsResponse = await SkyManager.ApiClient.ExecuteRequestAsync(new Skylight.Api.Assignments.V1.CardRequests.GetSequenceCardsRequest(assignmentId, ROOT_SEQUENCE_ID));
                var sequenceCards = sequenceCardsResponse.Content;
                foreach(var card in sequenceCards) {
                    var cardPatch = new CardPatch();
                    var cardIdSplit = card.Id.Split(CARD_ID_DELIMITER);
                    if(cardIdSplit.Length != 2){
                        switch(cardIdSplit[0]){
                            case REVIEWED_SECTION_CARD_ID:
                                cardPatch.Add("position", numReadyForReview + numInProgress + 3);
                                break;
                            
                            case IN_PROGRESS_SECTION_CARD_ID:
                                cardPatch.Add("position", numReadyForReview + 2);
                                break;
                            case REVIEW_SECTION_CARD_ID:
                                continue;
                        }

                        
                        await SkyManager.ApiClient.ExecuteRequestAsync(new Skylight.Api.Assignments.V1.CardRequests.PatchCardRequest(cardPatch, assignmentId, ROOT_SEQUENCE_ID, card.Id));
                    

                        continue;
                    }
                    var cardAssignmentId = cardIdSplit[1];
                    var assignmentIndex = assignmentIdsInOrder.IndexOf(cardAssignmentId);
                    var handoffAssignment = Assignments[assignmentIndex];

                    var layoutText = "";
                    cardPatch.Add("label", handoffAssignment.Name);

                    if(handoffAssignment.Reviewed) {
                        layoutText = "Reviewed by " + handoffAssignment.ReviewedBy;
                        cardPatch.Add("footer", "This assignment has been reviewed");
                        cardPatch.Add("position", assignmentIndex+4);
                        cardPatch.Add("subdued", true);
                        cardPatch.Add("component", null);
                    }

                    else if(handoffAssignment.Progress >= 1) {
                        if(!String.IsNullOrEmpty(handoffAssignment.UserId)){
                            layoutText = "Being reviewed by " + handoffAssignment.UserName;
                            cardPatch.Add("footer", "This assignment is being reviewed");
                            cardPatch.Add("subdued", true);
                            cardPatch.Add("isDone", false);
                            cardPatch.Add("component", null);
                        } else {
                            layoutText = "Ready for review";
                            cardPatch.Add("footer", "This assignment is ready for review");
                            cardPatch.Add("subdued", false);
                            cardPatch.Add("isDone", false);
                            cardPatch.Add("component", new ComponentCompletion() { Completed = false, Done = new DoneOnSelect() });
                        }
                        cardPatch.Add("position", assignmentIndex+2);
                    }
                    
                    else if(!String.IsNullOrEmpty(handoffAssignment.UserId)) {
                        cardPatch.Add("footer", "This assignment is in progress");
                        layoutText = "Assigned to " + handoffAssignment.UserName;
                        cardPatch.Add("position", assignmentIndex+3);
                        cardPatch.Add("subdued", true);
                        cardPatch.Add("component", null);
                    }
                    
                    else {
                        layoutText = Math.Round(handoffAssignment.Progress * 100) + "% Complete";
                        cardPatch.Add("footer", "This assignment is in progress");
                        cardPatch.Add("position", assignmentIndex+3);
                        cardPatch.Add("subdued", true);
                        cardPatch.Add("component", null);
                        
                    }

                    cardPatch.Add("layout", new LayoutText() {
                        TextSize = TextSize.Small
                        , Text = layoutText
                        , Alignment = Alignment.Center
                    });


        
                    await SkyManager.ApiClient.ExecuteRequestAsync(new Skylight.Api.Assignments.V1.CardRequests.PatchCardRequest(cardPatch, assignmentId, ROOT_SEQUENCE_ID, card.Id));
                    
                }


            }
        }

        static async Task ResetCard(Card card) {
            try {
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
            } catch(Exception e){}

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
            foreach(var handoffAssignment in Assignments){
                var assignmentId = await CreateSubAssignment(handoffAssignment);
                handoffAssignment.AssignmentId = assignmentId;
            }
        }

        static async Task<string> CreateSubAssignment(HandoffAssignment handoffAssignment) {

            //Create the assignment body
            var assignment = new AssignmentNew
            {
                Description = "This is " + handoffAssignment.Name + " created by the SDK Assignment Handoff example.",
                IntegrationId = SkyManager.IntegrationId,
                Name = handoffAssignment.Name
            };

            //Create a sequence -- theoretically, this would be better placed in another function
            //We have placed this inline within this function for clarity in this example
            var sequenceOne = new SequenceNew
            {
                Id = ROOT_SEQUENCE_ID,
                ViewMode = ViewMode.Native //This is the default view mode and will generally be used
            };


            //Create a card for sequence1
            var sequenceOneCardTwo = new CardNew
            {
                Footer = "Select this card to check this assignment back in.",
                Id = CHECKIN_CARD_ID_PREFIX + CARD_ID_DELIMITER + handoffAssignment.Id, //As long as the ID is unique within the sequence, we're good to go
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
            List<CardNew> sequenceCards = new List<CardNew>
            {
                CreateDecisionCard("What is your favorite color?", new List<string>(){"Blue", "Orange", "Purple", "Green", "Other"}, "card1")
                , CreateDecisionCard("How many inspection points are there?", new List<string>(){"0", "1", "2", "3", "4+"}, "card2")
                , CreateDecisionCard("How many bolts are in the sheet?", new List<string>(){"0", "1", "2", "3", "4+"}, "card3")
                , sequenceOneCardTwo
            };

            var index = 1;
            foreach(var card in sequenceCards) {
                card.Position = index;
                index += 1;
            }

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

            return result.Content.Id;
        }

        static CardNew CreateDecisionCard(string prompt, List<string> choices, string id) {
            
            var decisionComponent = new ComponentDecision(){
                MaxSelected = 1,
                Mutable = true,
                IncludeCapture = true
            };
            decisionComponent.Choices = new Dictionary<string, Choice>();

            var index = 0;
            foreach(var choice in choices) {
                decisionComponent.Choices.Add((index + 1) + "", new Choice(){
                    Label = choice
                    , Position = index + 1
                    , Selected = false
                });
                index += 1;
            }

            //Create a card for sequence1
            var sequenceOneCardOne = new CardNew
            {
                Label = prompt,
                Id = id,
                Size = 2, //Size can be 1, 2, or 3 and determines how much of the screen a card takes up (3 being fullscreen)
                Layout = new LayoutImage() {
                    Uri = "resource://image/ic_state_multiplechoice_01"
                },
                Selectable = true,
                Component = decisionComponent
            };
            return sequenceOneCardOne;
        }
        
        static async Task AssignSubAssignment(HandoffAssignment handoffAssignment, string userId){
            handoffAssignment.UserId = userId;
            var userResult = await SkyManager.ApiClient.ExecuteRequestAsync(new Skylight.Api.Authentication.V1.UsersRequests.GetUserRequest(userId));
            handoffAssignment.UserName = userResult.Content.Username;
            
            var assignmentPatch = new AssignmentPatch();
            assignmentPatch.Add("assignedTo", userId);
            
            var assignmentPatchRequest = new Skylight.Api.Assignments.V1.AssignmentRequests.PatchAssignmentRequest(assignmentPatch, handoffAssignment.AssignmentId);

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

        static async Task UpdateProgressForAssignment(HandoffAssignment handoffAssignment) {
            var assignmentRootSequenceCardsResponse = await SkyManager.ApiClient.ExecuteRequestAsync(new Skylight.Api.Assignments.V1.CardRequests.GetSequenceCardsRequest(handoffAssignment.AssignmentId, ROOT_SEQUENCE_ID));
            var assignmentRootSequenceCards = assignmentRootSequenceCardsResponse.Content;
            var totalDecisions = 0.0f;
            var totalDecisionsCompleted = 0.0f;
            var reviewCardIsIn = false;
            foreach(var card in assignmentRootSequenceCards) {
                if(card.Component.ComponentType != ComponentType.Decision){
                    if(card.Id.StartsWith(REVIEW_CARD_ID_PREFIX))reviewCardIsIn = true;
                    continue;
                }
                var decisionComponent = (ComponentDecision)(card.Component);
                var componentCompleted = false;
                foreach(var choice in decisionComponent.Choices) {
                    if(!choice.Value.Selected.HasValue || !choice.Value.Selected.Value)continue;
                    componentCompleted = true;
                    break;
                };
                if(componentCompleted) totalDecisionsCompleted += 1;
                totalDecisions += 1;
            }

            handoffAssignment.Progress = totalDecisionsCompleted / totalDecisions;
            if(handoffAssignment.Progress >= 1 && !reviewCardIsIn) {
                //Delete the checkin card
                await SkyManager.ApiClient.ExecuteRequestAsync(new Skylight.Api.Assignments.V1.CardRequests.DeleteCardRequest(handoffAssignment.AssignmentId, ROOT_SEQUENCE_ID, CHECKIN_CARD_ID_PREFIX + CARD_ID_DELIMITER + handoffAssignment.Id));

                //Mark everything else as subdued
                foreach(var card in assignmentRootSequenceCards) {
                    if(card.Component.ComponentType != ComponentType.Decision)continue;
                    var cardPatch = new CardPatch();
                    cardPatch.Add("subdued", true);
                    await SkyManager.ApiClient.ExecuteRequestAsync(new Skylight.Api.Assignments.V1.CardRequests.PatchCardRequest(cardPatch, handoffAssignment.AssignmentId, ROOT_SEQUENCE_ID, card.Id));
                }
                
                //Create a card for sequence1
                var completeReview = new CardNew
                {
                    Footer = "Select this card to complete the review.",
                    Id = REVIEW_CARD_ID_PREFIX + CARD_ID_DELIMITER + handoffAssignment.Id, //As long as the ID is unique within the sequence, we're good to go
                    Label = "Complete review",
                    Position = assignmentRootSequenceCards.Count, //Position of cards is 1-indexed
                    Size = 2, //Size can be 1, 2, or 3 and determines how much of the screen a card takes up (3 being fullscreen)
                    Layout = new LayoutText(),
                    Selectable = true,
                    Component = new ComponentCompletion() {
                        Done = new DoneOnSelect()
                    }
                };
                await SkyManager.ApiClient.ExecuteRequestAsync(new Skylight.Api.Assignments.V1.CardRequests.CreateCardsRequest(new List<CardNew>() { completeReview }, handoffAssignment.AssignmentId, ROOT_SEQUENCE_ID));

            }
        }
        
        static async Task UnassignSubAssignment(HandoffAssignment handoffAssignment){
            //Update worker info
            handoffAssignment.UserId = null;
            handoffAssignment.UserName = null;

            //Update progress
            await UpdateProgressForAssignment(handoffAssignment);

            var assignmentPatch = new AssignmentPatch();
            assignmentPatch.Add("assignedTo", "");
            
            var assignmentPatchRequest = new Skylight.Api.Assignments.V1.AssignmentRequests.PatchAssignmentRequest(assignmentPatch, handoffAssignment.AssignmentId);

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

        static async Task CreateCoordinatorAssignments() {
            var groupId = await GetGroupIdForGroupname(COORDINATOR_ACCOUNT_GROUP);
            var group = await GetGroupById(groupId);
            foreach(var member in group.Members) {
                //Remove all of the user's assignments to clean up for this example
                var userId = member.Id;
                await RemoveAllAssignmentsForUser(userId);
                await CreateCoordinatorAssignment(userId);
            }
        }
        static async Task CreateCoordinatorAssignment(string userId) {
            //Create the assignment body
            var assignment = new AssignmentNew
            {
                AssignedTo = userId,
                Description = "Manage assignments",
                IntegrationId = SkyManager.IntegrationId,
                Name = COORDINATOR_VIEW_ASSIGNMENTS_NAME
            };

            //Create a sequence -- theoretically, this would be better placed in another function
            //We have placed this inline within this function for clarity in this example
            var sequenceOne = new SequenceNew
            {
                Id = ROOT_SEQUENCE_ID,
                ViewMode = ViewMode.Native //This is the default view mode and will generally be used
            };

            var sequenceCards = new List<CardNew>();

            //Create a review label card for sequence1
            var reviewLabelCard = new CardNew
            {
                Id = REVIEW_SECTION_CARD_ID, //As long as the ID is unique within the sequence, we're good to go
                Label = "Ready for review:",
                Size = 1, //Size can be 1, 2, or 3 and determines how much of the screen a card takes up (3 being fullscreen)
                Layout = new LayoutText(),
                Selectable = false
            };
            sequenceCards.Add(reviewLabelCard);

            //Create a in progress label card for sequence1
            var inProgressLabelCard = new CardNew
            {
                Id = IN_PROGRESS_SECTION_CARD_ID, //As long as the ID is unique within the sequence, we're good to go
                Label = "In progress:",
                Size = 1, //Size can be 1, 2, or 3 and determines how much of the screen a card takes up (3 being fullscreen)
                Layout = new LayoutText(),
                Selectable = false
            };
            sequenceCards.Add(inProgressLabelCard);

            //Create cards for each assignment
            foreach(var handoffAssignment in Assignments) {
                //Create a card for sequence1
                var sequenceOneCard = new CardNew
                {
                    Id = INFO_CARD_ID_PREFIX + CARD_ID_DELIMITER + handoffAssignment.Id, //As long as the ID is unique within the sequence, we're good to go
                    Label = handoffAssignment.Name,
                    Size = 1, //Size can be 1, 2, or 3 and determines how much of the screen a card takes up (3 being fullscreen)
                    Layout = new LayoutText() {
                        TextSize = TextSize.Small
                        , Text = "0% Complete"
                        , Alignment = Alignment.Center
                    },
                    Subdued = true,
                    Selectable = true
                };
                sequenceCards.Add(sequenceOneCard);
            }

            //Create a reviewed label card for sequence1
            var reviewedLabelCard = new CardNew
            {
                Id = REVIEWED_SECTION_CARD_ID, //As long as the ID is unique within the sequence, we're good to go
                Label = "Completed and reviewed:",
                Size = 1, //Size can be 1, 2, or 3 and determines how much of the screen a card takes up (3 being fullscreen)
                Layout = new LayoutText(),
                Selectable = false
            };
            sequenceCards.Add(reviewedLabelCard);

            //Set positions of all cards
            var index = 1;
            foreach(var card in sequenceCards) {
                card.Position = index;
                index += 1;
            }

            //Set the card to live in sequence1. We could create more cards and add them in a similar manner
            sequenceOne.Cards = sequenceCards;

            //Add the sequence to the assignment
            assignment.Sequences = new List<SequenceNew>
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

            CoordinatorAssignmentIds.Add(result.Content.Id);
        }

        static async Task CreateWorkerAssignments() {
            var groupId = await GetGroupIdForGroupname(WORKER_ACCOUNT_GROUP);
            var group = await GetGroupById(groupId);
            foreach(var member in group.Members) {
                //Remove all of the user's assignments to clean up for this example
                var userId = member.Id;
                await RemoveAllAssignmentsForUser(userId);
                await CreateWorkerAssignment(userId);
            }
        }
        static async Task CreateWorkerAssignment(string userId) {
            //Create the assignment body
            var assignment = new AssignmentNew
            {
                AssignedTo = userId,
                Description = "View and check out assignments",
                IntegrationId = SkyManager.IntegrationId,
                Name = WORKER_VIEW_ASSIGNMENTS_NAME
            };

            //Create a sequence -- theoretically, this would be better placed in another function
            //We have placed this inline within this function for clarity in this example
            var sequenceOne = new SequenceNew
            {
                Id = ROOT_SEQUENCE_ID,
                ViewMode = ViewMode.Native //This is the default view mode and will generally be used
            };

            var sequenceCards = new List<CardNew>();

            foreach(var handoffAssignment in Assignments){
                //Create a card for sequence1
                var sequenceOneCard = new CardNew
                {
                    Footer = "Select to start this assignment",
                    Id = CHECKOUT_CARD_ID_PREFIX + CARD_ID_DELIMITER + handoffAssignment.Id, //As long as the ID is unique within the sequence, we're good to go
                    Label = handoffAssignment.Name,
                    Position = 1, //Position of cards is 1-indexed
                    Size = 1, //Size can be 1, 2, or 3 and determines how much of the screen a card takes up (3 being fullscreen)
                    Selectable = true,
                    HideLabel = false,
                    Layout = new LayoutText() {
                        TextSize = TextSize.Small
                        , Text = "0% Complete"
                        , Alignment = Alignment.Center
                    },
                    Component = new ComponentCompletion() {   
                        Done = new DoneOnSelect()
                    }
                };
                Console.WriteLine(sequenceOneCard.ToJson());
                sequenceCards.Add(sequenceOneCard);
            }

            //Set positions of all cards
            var index = 1;
            foreach(var card in sequenceCards) {
                card.Position = index;
                index += 1;
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

            WorkerAssignmentIds.Add(result.Content.Id);
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
