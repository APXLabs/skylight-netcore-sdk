using System;
using Xunit;
using Xunit.Priority;
using System.Threading.Tasks;
using Skylight.Api.Workflow.V2.WorkflowRequests;
using Skylight.Api.Workflow.V2.Models;
using Skylight.Api.Assignments.V1.Models;
using System.Collections.Generic;

namespace Skylight.Sdk.Tests
{
    [TestCaseOrderer(PriorityOrderer.Name, PriorityOrderer.Assembly)]
    public class WorkflowTests : APITest
    {
        public WorkflowTests(SkylightFixture fixture) : base(fixture) {
        }

        [Fact, Priority(0)]
        public async Task A_TestOne() {
            var requestBody = new WorkflowNew() {
                Name = "Test Workflow (Delete me)"
                , Description = "This was created using the SDK"
                , RootSequence = "rootSequence"
                , IntegrationId = SkyManager.IntegrationId
                , Fade = new Fade()
                , IsLocked = false
                , ConfirmCaptures = false
                , Template = new Template() {
                    new SequenceNew() {
                        Id = "rootSequence"
                        , Cards = new List<CardNew>() {
                            new CardNew() {
                                Id = "card1"
                                , Component = new ComponentDefault()
                                , Layout = new LayoutText() {
                                    Text = "Layout Text"
                                }
                                , Label = "Label"
                                , Position = 1
                                , Size = 1
                            }
                        }
                        , ViewMode = ViewMode.Native
                    }
                    , new SequenceNew() {
                        Id = "testSequence"
                        , Cards = new List<CardNew>() {
                            new CardNew() {
                                Id = "card1"
                                , Component = new ComponentDefault()
                                , Layout = new LayoutText() {
                                    Text = "Layout Text"
                                }
                                , Label = "Label"
                                , Position = 1
                                , Size = 1
                            }
                        }
                        , ViewMode = ViewMode.Native
                    }
                }
            };

            var request = new CreateWorkflowRequest(requestBody);
            await SkyManager.ApiClient.ExecuteRequestAsync(request);

        }

        [Fact, Priority(1)]
        public async Task B_TestTwo() {
            //var request = Skylight.Api.Workflow.V2.WorkflowRequests.
            //await SkyManager.ApiClient.ExecuteRequestAsync()
        }

        [Fact, Priority(2)]
        public async Task C_TestThree() {
            //var request = Skylight.Api.Workflow.V2.WorkflowRequests.
            //await SkyManager.ApiClient.ExecuteRequestAsync()
        }

    }
}
