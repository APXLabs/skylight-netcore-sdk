using System.Net.Mime;
using System;
using Xunit;
using Xunit.Priority;
using Xunit.Abstractions;
using System.Threading.Tasks;
using Skylight.Api.Assignments.V1.CardRequests;
using Skylight.Api.Assignments.V1.Models;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Skylight.Sdk.Tests
{
    public class CardTests : APITest
    {

        private const string CARD_ID = "card1";
        public CardTests(SkylightFixture fixture) : base(fixture) {
        }

        private static AssignmentNew CreateAssignmentNew() {
            var assignment = AssignmentUtils.CreateAssignmentNew();
            var sequence = AssignmentUtils.AddSequenceToAssignment(AssignmentUtils.ROOT_SEQUENCE_ID, assignment);
            AssignmentUtils.AddCardToSequence(CARD_ID, sequence);
            return assignment;
        }
        
        [Fact]
        public async Task TestCreateCards() {
            var assignment = CreateAssignmentNew();
            var assignmentId = await AssignmentUtils.CreateAssignment(assignment);

            var cardNew = AssignmentUtils.CreateCardNew(new Guid().ToString());
            cardNew.Position = 1;
            
            var createCardRequest = new CreateCardsRequest(new List<CardNew>(){ cardNew }, assignmentId, AssignmentUtils.ROOT_SEQUENCE_ID);
            await SkyManager.ApiClient.ExecuteRequestAsync(createCardRequest);

            await AssignmentUtils.DeleteAssignment(assignmentId);
        }

        [Fact]
        public async Task TestDeleteCard() {
            var assignment = CreateAssignmentNew();
            var assignmentId = await AssignmentUtils.CreateAssignment(assignment);
            
            var deleteCardRequest = new DeleteCardRequest(assignmentId, AssignmentUtils.ROOT_SEQUENCE_ID, CARD_ID);
            deleteCardRequest.AddPurgeQuery(true);

            await SkyManager.ApiClient.ExecuteRequestAsync(deleteCardRequest);

            await AssignmentUtils.DeleteAssignment(assignmentId);
        }

        [Fact]
        public async Task TestGetAssignmentCards() {
            var assignment = CreateAssignmentNew();
            var assignmentId = await AssignmentUtils.CreateAssignment(assignment);

            var resultLimit = 1;
            
            var getAssignmentCardsRequest = new GetAssignmentCardsRequest(assignmentId);
            getAssignmentCardsRequest.AddArchivedQuery(false);
            getAssignmentCardsRequest.AddLimitQuery(resultLimit);
            getAssignmentCardsRequest.AddPageQuery(0);
            getAssignmentCardsRequest.AddSortQuery("created");
            getAssignmentCardsRequest.AddStartQuery(0);
            getAssignmentCardsRequest.AddTypeQuery("default");

            var getAssignmentCardsResponse = await SkyManager.ApiClient.ExecuteRequestAsync(getAssignmentCardsRequest);

            Assert.InRange(getAssignmentCardsResponse.Content.Cards.Count, 1, resultLimit);

            await AssignmentUtils.DeleteAssignment(assignmentId);
        }

        [Fact]
        public async Task TestGetCard() {
            var assignment = CreateAssignmentNew();
            var assignmentId = await AssignmentUtils.CreateAssignment(assignment);
            
            var getCardRequest = new GetCardRequest(assignmentId, AssignmentUtils.ROOT_SEQUENCE_ID, CARD_ID);

            var getCardResponse = await SkyManager.ApiClient.ExecuteRequestAsync(getCardRequest);

            Assert.Equal(getCardResponse.Content.Id, CARD_ID);

            await AssignmentUtils.DeleteAssignment(assignmentId);
        }
        
        [Fact]
        public async Task TestGetCards() {
            var assignment = CreateAssignmentNew();
            assignment.AssignedTo = TestUserId;
            var assignmentId = await AssignmentUtils.CreateAssignment(assignment);
            
            var resultLimit = 1;
            var getCardsRequest = new GetCardsRequest();
            getCardsRequest.AddLimitQuery(resultLimit);
            getCardsRequest.AddPageQuery(0);
            getCardsRequest.AddSortQuery("created");
            getCardsRequest.AddStartQuery(0);
            getCardsRequest.AddUserIdQuery(TestUserId);
            getCardsRequest.AddTypeQuery("default");

            var getCardsResponse = await SkyManager.ApiClient.ExecuteRequestAsync(getCardsRequest);
            Assert.InRange(getCardsResponse.Content.Cards.Count, 1, resultLimit);

            await AssignmentUtils.DeleteAssignment(assignmentId);
        }
        
        [Fact]
        public async Task TestGetSequenceCards() {
            var assignment = CreateAssignmentNew();
            var assignmentId = await AssignmentUtils.CreateAssignment(assignment);

            var getSequenceCardsRequest = new GetSequenceCardsRequest(assignmentId, AssignmentUtils.ROOT_SEQUENCE_ID);
            getSequenceCardsRequest.AddArchivedQuery(true);
            getSequenceCardsRequest.AddCompletedQuery(true);

            var getSequenceCardsResponse = await SkyManager.ApiClient.ExecuteRequestAsync(getSequenceCardsRequest);

            Assert.Single(getSequenceCardsResponse.Content);

            await AssignmentUtils.DeleteAssignment(assignmentId);
        }

        [Fact]
        public async Task TestPatchCard() {
            var assignment = CreateAssignmentNew();
            var assignmentId = await AssignmentUtils.CreateAssignment(assignment);

            var cardPatch = new CardPatch();
            cardPatch.Add("label", "New Label");
            var patchCardRequest = new PatchCardRequest(cardPatch, assignmentId, AssignmentUtils.ROOT_SEQUENCE_ID, CARD_ID);
            await SkyManager.ApiClient.ExecuteRequestAsync(patchCardRequest); //TODO: If we wanted to do a full integration test, we could get the card and make sure the label has updated

            await AssignmentUtils.DeleteAssignment(assignmentId);
        }

        [Fact]
        public async Task TestUpdateCard() {
            var assignment = CreateAssignmentNew();
            var assignmentId = await AssignmentUtils.CreateAssignment(assignment);

            var cardUpdate = new CardUpdate() {
                Label = "New Label"
                , Size = 1
                , Position = 1
                , Layout = new LayoutText() {
                    Text = "New Text"
                    , TextSize = TextSize.Large
                }
                , Component = new ComponentDefault()
            };
            var updateCardRequest = new UpdateCardRequest(cardUpdate, assignmentId, AssignmentUtils.ROOT_SEQUENCE_ID, CARD_ID);
            await SkyManager.ApiClient.ExecuteRequestAsync(updateCardRequest); 

            await AssignmentUtils.DeleteAssignment(assignmentId);
        }
        
        [Fact]
        public async Task TestCallingComponentCard() {
        }
    }
}
