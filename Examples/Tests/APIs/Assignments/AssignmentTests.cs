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
    public class AssignmentTests : APITest
    {

        public AssignmentTests(SkylightFixture fixture) : base(fixture) {
        }

        
        private static AssignmentNew CreateSimpleAssignmentNew() {
            var assignment = AssignmentUtils.CreateAssignmentNew();
            var sequence = AssignmentUtils.AddSequenceToAssignment(AssignmentUtils.ROOT_SEQUENCE_ID, assignment);
            AssignmentUtils.AddCardToSequence("card1", sequence);
            return assignment;
        }

        private static AssignmentNew CreateComplexAssignmentNew() {
            var assignment = AssignmentUtils.CreateAssignmentNew();
            var sequenceOne = AssignmentUtils.AddSequenceToAssignment(AssignmentUtils.ROOT_SEQUENCE_ID, assignment);
            AssignmentUtils.AddCardToSequence("card1", sequenceOne);
            AssignmentUtils.AddCardToSequence("card2", sequenceOne);
            
            var sequenceTwo = AssignmentUtils.AddSequenceToAssignment("sequence2", assignment);
            AssignmentUtils.AddCardToSequence("card1", sequenceTwo);
            AssignmentUtils.AddCardToSequence("card2", sequenceTwo);
            return assignment;
        }

        //Our tests
        [Fact]
        public async Task TestCreateAndDeleteSimpleAssignment() {
            var assignment = CreateSimpleAssignmentNew();
            var assignmentId = await AssignmentUtils.CreateAssignment(assignment);
            await AssignmentUtils.DeleteAssignment(assignmentId);
        }

        [Fact]
        public async Task TestCreateAndDeleteComplexAssignment() {
            var assignment = CreateComplexAssignmentNew();
            var assignmentId = await AssignmentUtils.CreateAssignment(assignment);
            await AssignmentUtils.DeleteAssignment(assignmentId);
        }

        [Fact]
        public async Task TestGetAssignment() {
            var assignment = CreateSimpleAssignmentNew();
            var assignmentId = await AssignmentUtils.CreateAssignment(assignment);
            var returnedAssignment = await AssignmentUtils.GetAssignment(assignmentId);
            Assert.True(returnedAssignment.Id.Equals(assignmentId));
            await AssignmentUtils.DeleteAssignment(assignmentId);
        }

        [Fact]
        public async Task TestUserIdQueryAssignments() {
            var assignment = CreateSimpleAssignmentNew();
            assignment.AssignedTo = TestUserId;
            var assignmentId = await AssignmentUtils.CreateAssignment(assignment);
            
            var getAssignmentsRequest = new GetAssignmentsRequest();
            getAssignmentsRequest.AddUserIdsQuery(TestUserId);
            var getAssignmentsResponse = await SkyManager.ApiClient.ExecuteRequestAsync(getAssignmentsRequest);
            var queriedAssignments = getAssignmentsResponse.Content;
            
            //As the tests are run in parallel, there may be more than one assignment. At the very least, we'll have one assignment.
            Assert.InRange(queriedAssignments.Count, 1, Int16.MaxValue);

            await AssignmentUtils.DeleteAssignment(assignmentId);
        }

        [Fact]
        public async Task TestAllRealmQueryAssignments() {
            var assignment = CreateSimpleAssignmentNew();
            var assignmentId = await AssignmentUtils.CreateAssignment(assignment);
            
            var getAssignmentsRequest = new GetAssignmentsRequest();
            getAssignmentsRequest.AddAllRealmQuery(true);
            var getAssignmentsResponse = await SkyManager.ApiClient.ExecuteRequestAsync(getAssignmentsRequest);
            var queriedAssignments = getAssignmentsResponse.Content;
            
            //There should exist at least one assignment in the realm, as we just made one
            Assert.InRange(queriedAssignments.Count, 1, Int16.MaxValue);
            await AssignmentUtils.DeleteAssignment(assignmentId);
        }

        [Fact]
        public async Task TestCompletedQueryAssignments() {
            var assignment = CreateSimpleAssignmentNew();
            var assignmentId = await AssignmentUtils.CreateAssignment(assignment);

            var assignmentPatch = new AssignmentPatch();
            //When we set isComplete to true, Skylight will set the completedBy field to our integration id
            assignmentPatch.Add("completedAt", DateTime.Now.ToString("yyyy-MM-dd'T'HH:mm:ss.fffzzz"));
            assignmentPatch.Add("isComplete", true);
            
            var assignmentPatchRequest = new PatchAssignmentRequest(assignmentPatch, assignmentId);
            await SkyManager.ApiClient.ExecuteRequestAsync(assignmentPatchRequest);
            
            var getAssignmentsRequest = new GetAssignmentsRequest();
            getAssignmentsRequest.AddAllRealmQuery(true);
            getAssignmentsRequest.AddCompletedQuery(true);
            getAssignmentsRequest.AddCompletedByQuery(SkyManager.IntegrationId); //As noted above, this assignment will have been completed by the integration id
            var getAssignmentsResponse = await SkyManager.ApiClient.ExecuteRequestAsync(getAssignmentsRequest);
            var queriedAssignments = getAssignmentsResponse.Content;
            
            //There should exist at least one assignment in the realm, as we just made one
            Assert.InRange(queriedAssignments.Count, 1, Int16.MaxValue);
            await AssignmentUtils.DeleteAssignment(assignmentId);
        }

        [Fact]
        public async Task TestArchivedQueryAssignments() {
            var assignment = CreateSimpleAssignmentNew();
            var assignmentId = await AssignmentUtils.CreateAssignment(assignment);
            
            await AssignmentUtils.DeleteAssignment(assignmentId, false); //false sets the purge parameter to false, which means the assignment will be archived
            
            var getAssignmentsRequest = new GetAssignmentsRequest();
            getAssignmentsRequest.AddAllRealmQuery(true);
            getAssignmentsRequest.AddArchivedQuery(true);
            getAssignmentsRequest.AddArchivedOnlyQuery(true);
            var getAssignmentsResponse = await SkyManager.ApiClient.ExecuteRequestAsync(getAssignmentsRequest);
            var queriedAssignments = getAssignmentsResponse.Content;
            
            //There should exist at least one assignment in the realm, as we just made one
            Assert.InRange(queriedAssignments.Count, 1, Int16.MaxValue);

            await AssignmentUtils.DeleteAssignment(assignmentId); //Purge this assignment to clean up 
        }

        [Fact]
        public async Task TestNamedQueryAssignments() {
            var assignment = CreateSimpleAssignmentNew();
            assignment.Name = new Guid().ToString();
            var assignmentId = await AssignmentUtils.CreateAssignment(assignment);

            var getAssignmentsRequest = new GetAssignmentsRequest();
            getAssignmentsRequest.AddAllRealmQuery(true);
            getAssignmentsRequest.AddNameQuery(assignment.Name);
            var getAssignmentsResponse = await SkyManager.ApiClient.ExecuteRequestAsync(getAssignmentsRequest);
            var queriedAssignments = getAssignmentsResponse.Content;
            
            //There should exist at least one assignment in the realm, as we just made one
            Assert.InRange(queriedAssignments.Count, 1, Int16.MaxValue);

            await AssignmentUtils.DeleteAssignment(assignmentId); 
        }

        [Fact]
        public async Task TestUpdatedQueryAssignments() {
            var assignment = CreateSimpleAssignmentNew();
            var assignmentId = await AssignmentUtils.CreateAssignment(assignment);

            var assignmentUpdate = new AssignmentUpdate() {
                Name = new Guid().ToString()
            };
            
            var assignmentUpdateRequest = new UpdateAssignmentRequest(assignmentUpdate, assignmentId);
            await SkyManager.ApiClient.ExecuteRequestAsync(assignmentUpdateRequest);
            
            var getAssignmentsRequest = new GetAssignmentsRequest();
            getAssignmentsRequest.AddAllRealmQuery(true);
            getAssignmentsRequest.AddUpdatedByQuery(SkyManager.IntegrationId); //As noted above, this assignment will have been updated by the integration id
            var getAssignmentsResponse = await SkyManager.ApiClient.ExecuteRequestAsync(getAssignmentsRequest);
            var queriedAssignments = getAssignmentsResponse.Content;
            
            //There should exist at least one assignment in the realm, as we just made one
            Assert.InRange(queriedAssignments.Count, 1, Int16.MaxValue);
            await AssignmentUtils.DeleteAssignment(assignmentId);
        }

        [Fact]
        public async Task TestIntegrationIdQueryAssignments() {
            var assignment = CreateSimpleAssignmentNew();
            var assignmentId = await AssignmentUtils.CreateAssignment(assignment);

            var getAssignmentsRequest = new GetAssignmentsRequest();
            getAssignmentsRequest.AddAllRealmQuery(true);
            getAssignmentsRequest.AddIntegrationIdQuery(SkyManager.IntegrationId); //As noted above, this assignment will have been updated by the integration id
            var getAssignmentsResponse = await SkyManager.ApiClient.ExecuteRequestAsync(getAssignmentsRequest);
            var queriedAssignments = getAssignmentsResponse.Content;
            
            //There should exist at least one assignment in the realm, as we just made one
            Assert.InRange(queriedAssignments.Count, 1, Int16.MaxValue);
            await AssignmentUtils.DeleteAssignment(assignmentId);
        }

        [Fact]
        public async Task TestCreatedByQueryAssignments() {
            var assignment = CreateSimpleAssignmentNew();
            var assignmentId = await AssignmentUtils.CreateAssignment(assignment);
            
            var getAssignmentsRequest = new GetAssignmentsRequest();
            getAssignmentsRequest.AddAllRealmQuery(true);
            getAssignmentsRequest.AddCreatedByQuery(SkyManager.IntegrationId);
            var getAssignmentsResponse = await SkyManager.ApiClient.ExecuteRequestAsync(getAssignmentsRequest);
            var queriedAssignments = getAssignmentsResponse.Content;
            
            //There should exist at least one assignment in the realm, as we just made one
            Assert.InRange(queriedAssignments.Count, 1, Int16.MaxValue);
            await AssignmentUtils.DeleteAssignment(assignmentId);
        }

        [Fact]
        public async Task TestWorkflowIdQueryAssignments() {
            var assignment = CreateSimpleAssignmentNew();
            var assignmentId = await AssignmentUtils.CreateAssignment(assignment);
            
            var returnedAssignment = await AssignmentUtils.GetAssignment(assignmentId);
            
            var getAssignmentsRequest = new GetAssignmentsRequest();
            getAssignmentsRequest.AddAllRealmQuery(true);
            getAssignmentsRequest.AddWorkflowIdQuery(returnedAssignment.WorkflowId);
            var getAssignmentsResponse = await SkyManager.ApiClient.ExecuteRequestAsync(getAssignmentsRequest);
            var queriedAssignments = getAssignmentsResponse.Content;
            
            //There should exist at least one assignment in the realm, as we just made one
            Assert.InRange(queriedAssignments.Count, 1, Int16.MaxValue);
            await AssignmentUtils.DeleteAssignment(assignmentId);
        }

        [Fact]
        public async Task TestSortQueryAssignments() {
            var assignment = CreateSimpleAssignmentNew();
            assignment.AssignedTo = TestUserId;
            var assignmentId = await AssignmentUtils.CreateAssignment(assignment);
            
            var getAssignmentsRequest = new GetAssignmentsRequest();
            getAssignmentsRequest.AddUserIdsQuery(TestUserId);
            getAssignmentsRequest.AddSortQuery("created");//This sorts by ascending created date
            var getAssignmentsResponse = await SkyManager.ApiClient.ExecuteRequestAsync(getAssignmentsRequest);
            var queriedAssignments = getAssignmentsResponse.Content;
            
            //There should exist at least one assignment in the realm, as we just made one
            Assert.InRange(queriedAssignments.Count, 1, Int16.MaxValue);
            await AssignmentUtils.DeleteAssignment(assignmentId);
        }
        

    }
}
