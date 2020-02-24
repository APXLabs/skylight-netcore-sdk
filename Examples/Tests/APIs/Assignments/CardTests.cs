using System.Reflection.Metadata;
using System.Text.RegularExpressions;
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

        private static CardNew CreateCardNewWithComponent(Component component) {
            return new CardNew() {
                Label = "Label"
                , Id = new Guid().ToString()
                , Size = 3
                , Layout = new LayoutText() {
                    Text = "Layout"
                    , TextSize = TextSize.Small
                }
                , Component = component
            };
        }

        private static CardNew CreateCardNewWithLayout(Layout layout) {
            return new CardNew() {
                Label = "Label"
                , Id = new Guid().ToString()
                , Size = 3
                , Layout = layout
                , Component = new ComponentDefault()
            };
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
            
            var page = 0;
            var start = 0;
            var getAssignmentCardsRequest = new GetAssignmentCardsRequest(assignmentId);
            getAssignmentCardsRequest.AddArchivedQuery(false);
            getAssignmentCardsRequest.AddLimitQuery(resultLimit);
            getAssignmentCardsRequest.AddPageQuery(page);
            getAssignmentCardsRequest.AddSortQuery("created");
            getAssignmentCardsRequest.AddStartQuery(start);
            getAssignmentCardsRequest.AddTypeQuery("default");

            var getAssignmentCardsResponse = await SkyManager.ApiClient.ExecuteRequestAsync(getAssignmentCardsRequest);

            PaginatedCardList cardList = getAssignmentCardsResponse.Content;
            Assert.True(cardList.Size.HasValue);
            Assert.InRange(cardList.Size.Value, 1, resultLimit);
            Assert.Equal(page, cardList.Page);
            Assert.Equal(start, cardList.Start);
            Assert.Equal(resultLimit, cardList.Limit);
            Assert.NotNull(cardList.UpdatedFrom);
            //Assert.NotNull(cardList.UpdatedTo); //TODO: figure out why this is null

            Assert.NotNull(cardList.ToString());
            Assert.NotNull(cardList.ToJson());
            cardList.SetDefaults();//This is purely for code coverage, this should not be used this way in practice

            await AssignmentUtils.DeleteAssignment(assignmentId);
        }

        [Fact]
        public async Task TestGetCard() {
            var assignment = CreateAssignmentNew();
            var assignmentId = await AssignmentUtils.CreateAssignment(assignment);
            
            var getCardRequest = new GetCardRequest(assignmentId, AssignmentUtils.ROOT_SEQUENCE_ID, CARD_ID);

            var getCardResponse = await SkyManager.ApiClient.ExecuteRequestAsync(getCardRequest);
            var returnedCard = getCardResponse.Content;
            
            Assert.Equal(returnedCard.Id, CARD_ID);
            Assert.Equal(returnedCard.AssignmentId, assignmentId);
            Assert.Equal(returnedCard.CreatedBy, SkyManager.IntegrationId);
            Assert.NotNull(returnedCard.Created);
            Assert.NotNull(returnedCard.Footer);
            Assert.NotNull(returnedCard.Header);
            Assert.False(returnedCard.HideLabel);
            Assert.Equal(returnedCard.IntegrationId, SkyManager.IntegrationId);
            Assert.False(returnedCard.IsDone);
            Assert.False(returnedCard.Locked);
            Assert.False(returnedCard.Required);
            Assert.NotNull(returnedCard.Notes);
            Assert.NotNull(returnedCard.Label);
            Assert.NotNull(returnedCard.Revision);
            Assert.Equal(AssignmentUtils.ROOT_SEQUENCE_ID, returnedCard.SequenceId);
            Assert.Equal(1, returnedCard.Size);
            Assert.False(returnedCard.Subdued);
            Assert.Empty(returnedCard.Tags);
            Assert.NotNull(returnedCard.TemplateId);
            Assert.Equal(1, returnedCard.Position);
            Assert.False(returnedCard.Selectable);
        

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
            var getCardRequest = new GetCardRequest(assignmentId, AssignmentUtils.ROOT_SEQUENCE_ID, CARD_ID);

            var getCardResponse = await SkyManager.ApiClient.ExecuteRequestAsync(getCardRequest);
            var returnedCard = getCardResponse.Content;
            Assert.Equal(SkyManager.IntegrationId, returnedCard.UpdatedBy);
            Assert.NotNull(returnedCard.Updated);
            

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
                , Footer = "Footer"
                , Header = "Header"
                , HideLabel = false
                , IsDone = false
                , Locked = false
                , Notes = new List<Guid?>()
                , Required = false
                , Selectable = false
                , Subdued = false
                , Tags = new List<string>()
                , Voice = new List<string>()
            };
            var updateCardRequest = new UpdateCardRequest(cardUpdate, assignmentId, AssignmentUtils.ROOT_SEQUENCE_ID, CARD_ID);
            await SkyManager.ApiClient.ExecuteRequestAsync(updateCardRequest); 

            await AssignmentUtils.DeleteAssignment(assignmentId);
        }

        /* Card Component Tests */

        private async Task TestComponentCard(Component component) {
            var assignment = CreateAssignmentNew();
            var assignmentId = await AssignmentUtils.CreateAssignment(assignment);

            var cardNew = CreateCardNewWithComponent(component);
            cardNew.Position = 1;
            
            var createCardRequest = new CreateCardsRequest(new List<CardNew>(){ cardNew }, assignmentId, AssignmentUtils.ROOT_SEQUENCE_ID);
            await SkyManager.ApiClient.ExecuteRequestAsync(createCardRequest);

            await AssignmentUtils.DeleteAssignment(assignmentId);

            //This is for code coverage
            var componentString = component.ToString();
            var componentJson = component.ToJson();
        }
        
        [Fact]
        public async Task TestCallingComponentCard() {
            var component = new ComponentCalling() {
                CallType = CallType.Video
            };

            await TestComponentCard(component);

            //For coverage
            component.SetDefaults();
        }

        [Fact]
        public async Task TestCaptureAudioComponentCard() {
            var component = new ComponentCaptureAudio() {
                Duration = 1
            };

            await TestComponentCard(component);
            
            //For coverage
            component.Captures = new List<string>();
            Assert.Empty(component.Captures);
            component.SetDefaults();
        }

        [Fact]
        public async Task TestCapturePhotoComponentCard() {
            var component = new ComponentCapturePhoto() {
                Delay = 1
            };

            await TestComponentCard(component);
            
            //For coverage
            component.Captures = new List<string>();
            Assert.Empty(component.Captures);
            component.SetDefaults();
        }

        [Fact]
        public async Task TestCaptureVideoComponentCard() {
            var component = new ComponentCaptureVideo() {
                Duration = 1
            };

            await TestComponentCard(component);
            
            //For coverage
            component.Captures = new List<string>();
            Assert.Empty(component.Captures);
            component.SetDefaults();
        }

        [Fact]
        public async Task TestCompletionComponentCard() {
            var component = new ComponentCompletion() {
                Completed = false
                , MoveDelay = 1
                , ReturnPrev = false
                , ScrollLock = true
            };

            await TestComponentCard(component);

            //For coverage
            component.SetDefaults();
        }

        [Fact]
        public async Task TestDecisionComponentCard() {
            var choice = new Choice() 
            {
                Label = "choice1"
                , Position = 1
                , Selected = false
            };
            var choices = new Dictionary<string, Choice>();
            choices.Add("id1", choice);

            var component = new ComponentDecision() {
                Choices = choices
                , Link = new ComponentDecisionLink() {
                    Text = "Link"
                    , SequenceId = AssignmentUtils.ROOT_SEQUENCE_ID
                    , CardId = CARD_ID
                }
            };

            await TestComponentCard(component);

            //For coverage
            component.SetDefaults();
            choice.ToString();
            choice.ToJson();
            choice.SetDefaults();
        }

        [Fact]
        public async Task TestDefaultComponentCard() {
            
            var component = new ComponentDefault();

            await TestComponentCard(component);

            //For coverage
            component.SetDefaults();
        }

        [Fact]
        public async Task TestOpenSequenceComponentCard() {
            
            var component = new ComponentOpenSequence() 
            {
                Done = new DoneOnSelect()
                , SequenceId = AssignmentUtils.ROOT_SEQUENCE_ID
            };

            await TestComponentCard(component);

            //For coverage
            component.SetDefaults();
        }

        [Fact]
        public async Task TestScanningComponentCard() {
            
            var component = new ComponentScanning();

            await TestComponentCard(component);

            //For coverage
            component.SetDefaults();
        }

        /* Card Done Tests */
        [Fact]
        public async Task TestDoneScanCard() {
            var done = new DoneScanSuccess();
            var component = new ComponentScanning() {
                Done = done
            };

            await TestComponentCard(component);

            //For coverage
            component.SetDefaults();
            done.SetDefaults();
            Assert.NotNull(done.ToJson());
            Assert.NotNull(done.ToString());
        }

        [Fact]
        public async Task TestDoneCallCard() {
            var done = new DoneCallConnected();
            var component = new ComponentCalling() {
                Done = done
            };

            await TestComponentCard(component);

            //For coverage
            component.SetDefaults();
            done.SetDefaults();
            Assert.NotNull(done.ToJson());
            Assert.NotNull(done.ToString());
        }

        [Fact]
        public async Task TestDoneDataCard() {
            var done = new DoneDataCommitted();
            var component = new ComponentDefault() {
                Done = done
            };

            await TestComponentCard(component); //In practice, we'd want to make sure this pairs with a text input or other form of data entry layout

            //For coverage
            component.SetDefaults();
            done.SetDefaults();
            Assert.NotNull(done.ToJson());
            Assert.NotNull(done.ToString());
        }

        [Fact]
        public async Task TestDoneMediaCard() {
            var done = new DoneMediaWatched();
            var component = new ComponentDefault() {
                Done = done
            };

            await TestComponentCard(component); //In practice, we'd want to make sure this pairs with a media card

            //For coverage
            component.SetDefaults();
            done.SetDefaults();
            Assert.NotNull(done.ToJson());
            Assert.NotNull(done.ToString());
        }

        [Fact]
        public async Task TestDoneMinCapturedCard() {
            var done = new DoneMinCaptured(){
                MinCaptured = 1
            };
            var component = new ComponentCapturePhoto() {
                Done = done
            };

            await TestComponentCard(component); 

            //For coverage
            component.SetDefaults();
            done.SetDefaults();
            Assert.NotNull(done.ToJson());
            Assert.NotNull(done.ToString());
        }

        [Fact]
        public async Task TestDoneMinChoicesCard() {
            var done = new DoneMinChoices(); //The number of min choices is specified on the decision component
            
            var choice = new Choice() 
            {
                Label = "choice1"
                , Position = 1
                , Selected = false
            };
            var choices = new Dictionary<string, Choice>();
            choices.Add("id1", choice);

            var component = new ComponentDecision() {
                Choices = choices
                , Done = done
            };

            await TestComponentCard(component); 

            //For coverage
            component.SetDefaults();
            done.SetDefaults();
            Assert.NotNull(done.ToJson());
            Assert.NotNull(done.ToString());
        }

        [Fact]
        public async Task TestDoneOnFocus() {
            var done = new DoneOnFocus();
            var component = new ComponentDefault() {
                Done = done
            };

            await TestComponentCard(component); 

            //For coverage
            component.SetDefaults();
            done.SetDefaults();
            Assert.NotNull(done.ToJson());
            Assert.NotNull(done.ToString());
        }

        [Fact]
        public async Task TestDoneOnSelect() {
            var done = new DoneOnSelect();
            var component = new ComponentDefault() {
                Done = done
            };

            await TestComponentCard(component); 

            //For coverage
            component.SetDefaults();
            done.SetDefaults();
            Assert.NotNull(done.ToJson());
            Assert.NotNull(done.ToString());
        }

        /* Card Layout Tests */
        private async Task TestLayoutCard(Layout layout) {
            var assignment = CreateAssignmentNew();
            var assignmentId = await AssignmentUtils.CreateAssignment(assignment);

            var cardNew = CreateCardNewWithLayout(layout);
            cardNew.Position = 1;
            
            var createCardRequest = new CreateCardsRequest(new List<CardNew>(){ cardNew }, assignmentId, AssignmentUtils.ROOT_SEQUENCE_ID);
            await SkyManager.ApiClient.ExecuteRequestAsync(createCardRequest);

            await AssignmentUtils.DeleteAssignment(assignmentId);

            //This is for code coverage
            Assert.NotNull(layout.ToString());
            Assert.NotNull(layout.ToJson());
            layout.SetDefaults();
        }

        
        [Fact]
        public async Task TestMediaLayoutCard() {
            
            var layout = new LayoutAvMedia()
            {
                AutoPlay = true
                , Loop = false
                , Uri = "resource://image/ic_mc_multiplechoice_01" //This is a filler resource, the URI is incorrect for this layout
            };

            await TestLayoutCard(layout);

            //For coverage
            layout.SetDefaults();
        }

        [Fact]
        public async Task TestDynamicLayoutCard() {
            
            var layout = new LayoutDynamic()
            {
                StateData = new Dictionary<string, object>()
                , Uri = "resource://image/ic_mc_multiplechoice_01" //This is a filler resource, the URI is incorrect for this layout
            };

            await TestLayoutCard(layout);

            //For coverage
            layout.SetDefaults();
        }
        
        [Fact]
        public async Task TestLayoutImageCard() {
            
            var layout = new LayoutImage()
            {
                Uri = "resource://image/ic_mc_multiplechoice_01" //This is a filler resource, the URI is incorrect for this layout
                , Scale = Scale.Center
            };

            await TestLayoutCard(layout);

            //For coverage
            layout.SetDefaults();
        }

        [Fact]
        public async Task TestPDFLayoutCard() {
            
            var layout = new LayoutPDF()
            {
                PageNumber = 1
                , Uri = "resource://image/ic_mc_multiplechoice_01" //This is a filler resource, the URI is incorrect for this layout
            };

            await TestLayoutCard(layout);

            //For coverage
            layout.SetDefaults();
        }

        [Fact]
        public async Task TestSVGLayoutCard() {
            
            var layout = new LayoutSVG()
            {
                StateData = new Dictionary<string, object>()
                , Uri = "resource://image/ic_mc_multiplechoice_01" //This is a filler resource, the URI is incorrect for this layout
            };

            await TestLayoutCard(layout);

            //For coverage
            layout.SetDefaults();
        }

        [Fact]
        public async Task TestTextLayoutCard() {
            
            var layout = new LayoutText()
            {
                Text = ""
                , TextSize = TextSize.Large
                , Alignment = Alignment.Center
                , AutoFit = true
                , BgColor = "000000"
                , FgColor = "ffffff"
            };

            await TestLayoutCard(layout);

            //For coverage
            layout.SetDefaults();
        }

        [Fact]
        public async Task TestTextInputLayoutCard() {
            var textInputFormat = new LayoutTextInputFormat().SetDefaults();
            textInputFormat.MaxChar = 5;
            textInputFormat.Type = LayoutTextInputFormatType.Generic;

            var layout = new LayoutTextInput()
            {
                Format = textInputFormat
                , HelpText = "Help"
                , Input = ""
                , Mutable = true
                , Placeholder = "Placeholder"
                , ShowIcon = true
            };

            await TestLayoutCard(layout);

            //For coverage
            layout.SetDefaults();
            Assert.NotNull(textInputFormat.ToJson());
            Assert.NotNull(textInputFormat.ToString());
        }

        [Fact]
        public async Task TestWebLayoutCard() {
            
            var layout = new LayoutWeb()
            {
                HtmlData = ""
            };

            await TestLayoutCard(layout);

            //For coverage
            layout.SetDefaults();
        }

        /* Card Action Tests */
        [Fact]
        public async Task TestCompleteAssignmentAction() {
            var doneAction = new ActionCompleteAssignment();
            var component = new ComponentCompletion() {
                Done = new DoneOnSelect(),
                DoneAction = doneAction
            };

            await TestComponentCard(component);

            //For coverage
            doneAction.ToString();
            doneAction.ToJson();
            doneAction.SetDefaults();
            component.SetDefaults();
        }

        
        [Fact]
        public async Task TestMoveToAction() {
            var doneAction = new ActionMoveTo() 
            {
                CardId = CARD_ID
                , SequenceId = AssignmentUtils.ROOT_SEQUENCE_ID
            };
            
            var component = new ComponentCompletion() {
                Done = new DoneOnSelect(),
                DoneAction = doneAction
            };

            await TestComponentCard(component);

            //For coverage
            doneAction.ToString();
            doneAction.ToJson();
            doneAction.SetDefaults();
            component.SetDefaults();
        }

        [Fact]
        public void TestCardCoverage() {
            //This is to test to ensure the properties exist; this would not be used in practice
            var card = new Card() {
                Label = ""
                , Locked = false
                , Position = 1
                , Required = false
                , Revision = 0
                , Selectable = false
                , SequenceId = AssignmentUtils.ROOT_SEQUENCE_ID
                , Size = 1
                , Subdued = false
                , TemplateId = ""
                , Updated = ""
                , UpdatedBy = SkyManager.IntegrationId

            };
            card.SetDefaults();

            Assert.NotNull(card.ToString());
            Assert.NotNull(card.ToJson());

            var cardNew = new CardNew().SetDefaults();
            cardNew.Tags = new List<string>() { "tag1" };
            cardNew.Voice = new List<string>() { "command" };
            Assert.Single(cardNew.Voice);
            Assert.Single(cardNew.Tags);
            Assert.NotNull(cardNew.ToJson());
            Assert.NotNull(cardNew.ToString());

            var cardPatch = new CardPatch().SetDefaults();
            Assert.NotNull(cardPatch.ToJson());
            Assert.NotNull(cardPatch.ToString());

            
            var cardUpdate = new CardUpdate().SetDefaults();
            Assert.NotNull(cardUpdate.ToJson());
            Assert.NotNull(cardUpdate.ToString());
        }

        [Fact]
        public void TestDecisionCaptures() {
            //This test checks for fields on the decision capture object.
            var decisionCapture = new ComponentDecisionCaptures().SetDefaults();
             
            decisionCapture.Id = "";
            decisionCapture.MimeType = "image/png";
            Assert.NotNull(decisionCapture.Id);
            Assert.NotNull(decisionCapture.MimeType);

            Assert.NotNull(decisionCapture.ToJson());
            Assert.NotNull(decisionCapture.ToString());

        }

        [Fact]
        public void TestDecisionLinks() {
            //This test checks for fields on the decision link object
            var decisionLink = new ComponentDecisionLink().SetDefaults();

            Assert.NotNull(decisionLink.ToJson());
            Assert.NotNull(decisionLink.ToString());
        }

        [Fact]
        public void TestFade() {
            //This test checks for fields on the fade object
            var fade = new Fade().SetDefaults();
            fade.Elements = new List<Element>(){ Element.Header };
            Assert.Single(fade.Elements);

            Assert.NotNull(fade.ToJson());
            Assert.NotNull(fade.ToString());
        }
    }
}
