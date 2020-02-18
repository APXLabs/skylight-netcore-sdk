using System.Net.Mime;
using System;
using Xunit;
using Xunit.Priority;
using Xunit.Abstractions;
using System.Threading.Tasks;
using Skylight.Api.Assignments.V1.AssignmentRequests;
using Skylight.Api.Assignments.V1.Models;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Skylight.Sdk.Tests
{
    public class AssignmentUtils : APITest
    {
        public const string ROOT_SEQUENCE_ID = "rootSequence";
        public AssignmentUtils(SkylightFixture fixture) : base(fixture) {
        }

        public static AssignmentNew CreateAssignmentNew(string name = "Test Assignment Delete Me") {
            return new AssignmentNew() {
                Name = name
                , Description = "This was created using the SDK"
                , RootSequence = ROOT_SEQUENCE_ID
                , IntegrationId = SkyManager.IntegrationId
                , Sequences = new List<SequenceNew>()
            };
        }

        public static SequenceNew CreateSequenceNew(string sequenceId) {
            return new SequenceNew() {
                Id = sequenceId
                , Cards = new List<CardNew>()
            };
        }

        public static CardNew CreateCardNew(string cardId) {
             return new CardNew() {
                Id = cardId
                , Component = new ComponentDefault()
                , Layout = new LayoutText() {
                    TextSize = TextSize.Large
                    , Text = ""
                }
                , Label = "Label"
                , Size = 1
            };
        }

        //Shared methods
        public static async Task<string> CreateAssignment(AssignmentNew assignment) {
            var createRequest = new CreateAssignmentRequest(assignment);
            var createResponse = await SkyManager.ApiClient.ExecuteRequestAsync(createRequest);
            return createResponse.Content.Id;
        }

        public static async Task<Assignment> GetAssignment(string assignmentId)  {
            var getAssignmentRequest = new GetAssignmentRequest(assignmentId);
            var getAssignmentResponse = await SkyManager.ApiClient.ExecuteRequestAsync(getAssignmentRequest);
            return getAssignmentResponse.Content;
        }

        public static async Task DeleteAssignment(string assignmentId, bool purge = true) {
            var deleteAssignmentRequest = new DeleteAssignmentRequest(assignmentId);
            deleteAssignmentRequest.AddPurgeQuery(purge);
            await SkyManager.ApiClient.ExecuteRequestAsync(deleteAssignmentRequest);
        }

        //We position the cards by their position in the array
        public static void PositionCardsInSequence(SequenceNew sequence) {
            var cards = sequence.Cards;
            for(var index = 0; index < cards.Count; index += 1) {
                var card = cards[index];
                card.Position = index + 1;//Position is 1-indexed
            }
        }


        public static SequenceNew AddSequenceToAssignment(string sequenceId, AssignmentNew assignment) {
            var sequence = CreateSequenceNew(sequenceId);
            assignment.Sequences.Add(sequence);
            return sequence;
        }

        public static CardNew AddCardToSequence(string cardId, SequenceNew sequence) {
            var card = CreateCardNew(cardId);
            sequence.Cards.Add(card);

            //Position cards after adding this card to the sequence -- technically this is unoptimized (the sort should occur after adding all cards), but we're working with a small set of cards and this leads to cleaner code for tests
            PositionCardsInSequence(sequence);
            return card;
        } 

    }

}