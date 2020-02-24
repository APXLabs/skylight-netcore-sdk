using System.Reflection.Metadata;
using System.Text.RegularExpressions;
using System.Net.Mime;
using System;
using Xunit;
using Xunit.Priority;
using Xunit.Abstractions;
using System.Threading.Tasks;
using Skylight.Api.Assignments.V1.SequenceRequests;
using Skylight.Api.Assignments.V1.Models;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Skylight.Sdk.Tests
{
    public class SequenceTests : APITest
    {

        private const string CARD_ID = "card1";
        private const string SEQUENCE_ID = "seq1";
        public SequenceTests(SkylightFixture fixture) : base(fixture) {
        }

        private static AssignmentNew CreateAssignmentNew() {
            var assignment = AssignmentUtils.CreateAssignmentNew();
            var sequence = AssignmentUtils.AddSequenceToAssignment(AssignmentUtils.ROOT_SEQUENCE_ID, assignment);
            AssignmentUtils.AddCardToSequence(CARD_ID, sequence);
            return assignment;
        }
        
        [Fact]
        public async Task TestCreateSequence() {
            var assignment = CreateAssignmentNew();
            var assignmentId = await AssignmentUtils.CreateAssignment(assignment);

            var sequenceNew = AssignmentUtils.CreateSequenceNew(new Guid().ToString());
            
            var createCardRequest = new CreateSequenceRequest(sequenceNew, assignmentId);
            await SkyManager.ApiClient.ExecuteRequestAsync(createCardRequest);

            await AssignmentUtils.DeleteAssignment(assignmentId);
        }
        
        [Fact]
        public async Task TestCreateSequences() {
            var assignment = CreateAssignmentNew();
            var assignmentId = await AssignmentUtils.CreateAssignment(assignment);

            var sequenceNew = AssignmentUtils.CreateSequenceNew(new Guid().ToString());
            
            var createSequenceRequest = new CreateSequencesRequest(new List<SequenceNew>(){ sequenceNew }, assignmentId);
            await SkyManager.ApiClient.ExecuteRequestAsync(createSequenceRequest);

            await AssignmentUtils.DeleteAssignment(assignmentId);
        }

        [Fact]
        public async Task TestDeleteSequence() {
            var assignment = CreateAssignmentNew();
            var assignmentId = await AssignmentUtils.CreateAssignment(assignment);
            
            var deleteSequenceRequest = new DeleteSequenceRequest(assignmentId, AssignmentUtils.ROOT_SEQUENCE_ID);
            deleteSequenceRequest.AddPurgeQuery(true);

            await SkyManager.ApiClient.ExecuteRequestAsync(deleteSequenceRequest);

            await AssignmentUtils.DeleteAssignment(assignmentId);
        }

        [Fact]
        public async Task TestGetAssignmentSequences() {
            var assignment = CreateAssignmentNew();
            var assignmentId = await AssignmentUtils.CreateAssignment(assignment);

            var getAssignmentSequencesRequest = new GetAssignmentSequencesRequest(assignmentId);
            getAssignmentSequencesRequest.AddArchivedQuery(false);

            var getAssignmentSequencesResponse = await SkyManager.ApiClient.ExecuteRequestAsync(getAssignmentSequencesRequest);

            List<Sequence> sequenceList = getAssignmentSequencesResponse.Content;

            Assert.NotEmpty(sequenceList);

            await AssignmentUtils.DeleteAssignment(assignmentId);
        }

        [Fact]
        public async Task TestGetSequence() {
            var assignment = CreateAssignmentNew();
            var assignmentId = await AssignmentUtils.CreateAssignment(assignment);
            
            var getSequenceRequest = new GetSequenceRequest(assignmentId, AssignmentUtils.ROOT_SEQUENCE_ID);

            var getSequenceResponse = await SkyManager.ApiClient.ExecuteRequestAsync(getSequenceRequest);
            var returnedSequence = getSequenceResponse.Content;
            
            Assert.Equal(returnedSequence.Id, AssignmentUtils.ROOT_SEQUENCE_ID);
            Assert.Equal(returnedSequence.AssignmentId, assignmentId);
        
            await AssignmentUtils.DeleteAssignment(assignmentId);
        }
        
        [Fact]
        public async Task TestGetSequences() {
            var assignment = CreateAssignmentNew();
            assignment.AssignedTo = TestUserId;
            var assignmentId = await AssignmentUtils.CreateAssignment(assignment);
            
            var resultLimit = 1;
            var page = 0;
            var start = 0;
            var getSequencesRequest = new GetSequencesRequest();
            getSequencesRequest.AddLimitQuery(resultLimit);
            getSequencesRequest.AddPageQuery(page);
            getSequencesRequest.AddStartQuery(start);
            getSequencesRequest.AddUserIdQuery(TestUserId);
            getSequencesRequest.AddArchivedQuery(true);
            getSequencesRequest.AddCompletedQuery(true);
            getSequencesRequest.AddUpdatedFromQuery(DateTime.MinValue.ToString("yyyy-MM-dd'T'HH:mm:ss.fffzzz"));

            var getSequencesResponse = await SkyManager.ApiClient.ExecuteRequestAsync(getSequencesRequest);

            PaginatedSequenceList sequenceList = getSequencesResponse.Content;
            Assert.True(sequenceList.Size.HasValue);
            Assert.InRange(sequenceList.Size.Value, 1, resultLimit);
            Assert.Equal(page, sequenceList.Page);
            Assert.Equal(start, sequenceList.Start);
            Assert.Equal(resultLimit, sequenceList.Limit);
            Assert.Null(sequenceList.Revision);
            //Because revision is null, we have to set it for code coverage
            sequenceList.Revision = 0;
            
            Assert.NotNull(sequenceList.UpdatedFrom);
            Assert.NotNull(sequenceList.UpdatedTo); //TODO: figure out why this is null

            Assert.NotNull(sequenceList.ToString());
            Assert.NotNull(sequenceList.ToJson());
            sequenceList.SetDefaults();//This is purely for code coverage, this should not be used this way in practice


            await AssignmentUtils.DeleteAssignment(assignmentId);
        }
        
        [Fact]
        public async Task TestPatchSequence() {
            var assignment = CreateAssignmentNew();
            var assignmentId = await AssignmentUtils.CreateAssignment(assignment);

            var sequencePatch = new SequencePatch();
            sequencePatch.Add("viewMode", "gallery");
            var patchSequenceRequest = new PatchSequenceRequest(sequencePatch, assignmentId, AssignmentUtils.ROOT_SEQUENCE_ID);
            await SkyManager.ApiClient.ExecuteRequestAsync(patchSequenceRequest); //TODO: If we wanted to do a full integration test, we could get the card and make sure the label has updated
            var getSequenceRequest = new GetSequenceRequest(assignmentId, AssignmentUtils.ROOT_SEQUENCE_ID);

            var getSequenceResponse = await SkyManager.ApiClient.ExecuteRequestAsync(getSequenceRequest);
            var returnedSequence = getSequenceResponse.Content;
            Assert.Equal(SkyManager.IntegrationId, returnedSequence.UpdatedBy);
            Assert.NotNull(returnedSequence.Updated);
            
            await AssignmentUtils.DeleteAssignment(assignmentId);
        }

        [Fact]
        public async Task TestUpdateSequence() {
            var assignment = CreateAssignmentNew();
            var assignmentId = await AssignmentUtils.CreateAssignment(assignment);

            var sequenceUpdate = new SequenceUpdate() {
                ViewMode = ViewMode.Gallery
            };
            var updateSequenceRequest = new UpdateSequenceRequest(sequenceUpdate, assignmentId, AssignmentUtils.ROOT_SEQUENCE_ID);
            await SkyManager.ApiClient.ExecuteRequestAsync(updateSequenceRequest); 

            await AssignmentUtils.DeleteAssignment(assignmentId);
        }


        [Fact]
        public void TestCardCoverage() {
            //This is to test to ensure the properties exist; this would not be used in practice
            var sequence = new Sequence() {
                ViewMode = ViewMode.Gallery

            };
            sequence.SetDefaults();

            Assert.NotNull(sequence.ToString());
            Assert.NotNull(sequence.ToJson());

            
            var sequenceNew = new SequenceNew() 
            {
                TemplateId = ""
            };
            Assert.NotNull(sequenceNew.TemplateId);
            sequenceNew.SetDefaults();
            Assert.NotNull(sequenceNew.ToJson());
            Assert.NotNull(sequenceNew.ToString());

            var sequencePatch = new SequencePatch().SetDefaults();
            Assert.NotNull(sequencePatch.ToJson());
            Assert.NotNull(sequencePatch.ToString());

            
            var sequenceUpdate = new SequenceUpdate().SetDefaults();
            Assert.NotNull(sequenceUpdate.ToJson());
            Assert.NotNull(sequenceUpdate.ToString());
        }

    }
}
