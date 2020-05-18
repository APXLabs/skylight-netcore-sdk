using System;
using Xunit;
using Xunit.Priority;
using Xunit.Abstractions;
using System.Threading.Tasks;
using Skylight.Api.Notes.V1.Models;
using Skylight.Api.Notes.V1.NotesRequests;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Skylight.Sdk.Tests
{
    public class NotesTests : APITest
    {
        public NotesTests(SkylightFixture fixture) : base(fixture) {
        }

        //Our tests
        [Fact]
        public async Task TestCreateAndGetNote() {
            var TEST_TEXT = "Hi I'm a note!";
            var newNote = new NoteNew().SetDefaults();
            newNote.Data = new NoteTypeText() {
                Text = TEST_TEXT
            };
            Assert.NotNull(newNote.ToString());
            Assert.NotNull(newNote.ToJson());

            var createRequest = new CreateNoteRequest(newNote);//.SetDefaults();

            var noteResponse = await SkyManager.ApiClient.ExecuteRequestAsync(createRequest);
            var result = noteResponse.Content;
            Assert.NotNull(result.ToString());
            Assert.NotNull(result.ToJson());
            Assert.NotNull(result.Id);

            var newNoteId = result.Id;
            result.SetDefaults();

            var getRequest = new GetNoteRequest(newNoteId);
            var getNoteRespose = await SkyManager.ApiClient.ExecuteRequestAsync(getRequest);
            var getResult = getNoteRespose.Content;
            Assert.True(getResult.Data.Text.Equals(TEST_TEXT));

        }

    }
}
