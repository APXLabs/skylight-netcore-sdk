using System;
using Xunit;
using Xunit.Priority;
using Xunit.Abstractions;
using System.Threading.Tasks;
using Skylight.Api.Workflow.V2.WorkflowRequests;
using Skylight.Api.Workflow.V2.Models;
using Skylight.Api.Assignments.V1.Models;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Skylight.Sdk.Tests
{
    public class WorkflowTests : APITest
    {
        public WorkflowTests(SkylightFixture fixture) : base(fixture) {
        }

        public class XUnitWorkflow : IXunitSerializable {
            
            public WorkflowNew Workflow { get; private set; }
            public XUnitWorkflow() {}

            public XUnitWorkflow(WorkflowNew Workflow) {
                this.Workflow = Workflow;
            }

            public void Deserialize(IXunitSerializationInfo info) {
                Workflow = JsonConvert.DeserializeObject<WorkflowNew>(info.GetValue<string>("workflow"));
            }

            public void Serialize(IXunitSerializationInfo info) {
                info.AddValue("workflow", Workflow.ToJson(), typeof(string));
            }

            public override string ToString(){
                return Workflow.Name;
            }

            
        }

        //This provides the data for our tests
        public static IEnumerable<object[]> GetWorkflows(){
            yield return new object[] {
                new XUnitWorkflow(
                    new WorkflowNew()    {
                        Name = "Test Workflow (Delete)"
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
                    }
                )
            };

            yield return new object[] {
                new XUnitWorkflow(
                    new WorkflowNew()    {
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
                    }
                )
            };
        }

        //Our tests
        [Theory]
        [MemberData(nameof(GetWorkflows))]
        public async Task CreateAndDeleteWorkflow(XUnitWorkflow xWorkflow) {
            
            WorkflowNew workflow = xWorkflow.Workflow;
            var createRequest = new CreateWorkflowRequest(workflow);
            var createResponse = await SkyManager.ApiClient.ExecuteRequestAsync(createRequest);
            var workflowId = createResponse.Content.WorkflowId;
            var assignWorkflowBody = new AssignWorkflowBody() {
                Name = "Assigned Workflow"
            };
            await SkyManager.ApiClient.ExecuteRequestAsync(new AssignWorkflowRequest(assignWorkflowBody, workflowId, TestUserId));

            await SkyManager.ApiClient.ExecuteRequestAsync(new DeleteWorkflowRequest(workflowId));

        }

    }
}
