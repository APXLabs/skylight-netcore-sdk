using System.Net;
using System.IO;
using System;
using Xunit;
using Xunit.Priority;
using Xunit.Abstractions;
using System.Threading.Tasks;
using Skylight.Api.Media.V3.FilesRequests;
using Skylight.Api.Media.V3.Models;
using Skylight.Api.Assignments.V1.Models;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Skylight.Sdk.Tests
{
    public class MediaTests : APITest
    {
        public MediaTests(SkylightFixture fixture) : base(fixture) {
        }

        //Our tests
        [Fact]
        public async Task TestUploadUpdateDownloadAndDeleteFile( ) {
            var fileInfo = new System.IO.FileInfo(Path.Join(".", "testFiles", "test.png"));
            var fileDescription = "File Description";
            var uploadFileResponse = await SkyManager.MediaClient.UploadFile(fileInfo, "File Title", fileDescription);
            var fileId = uploadFileResponse.FileId;

            var updateFileBody = new FileUpdate();
            updateFileBody.Filename = "File.png";
            updateFileBody.Title = "Updated File Title";
            updateFileBody.Description = "Updated File Description";
            updateFileBody.Tags = new List<string>();
            updateFileBody.Properties = new Properties();

            var updateFileRequest = new UpdateFileRequest(updateFileBody, fileId);
            updateFileRequest.AdditionalRequestHeaders.Add(new KeyValuePair<string, string>("If-Match", uploadFileResponse.ETag));
            await SkyManager.ApiClient.ExecuteRequestAsync(updateFileRequest);

            await SkyManager.MediaClient.DownloadFile(fileId);

            var getFileRequest = new GetFileRequest(fileId);
            var getFileResponse = await SkyManager.ApiClient.ExecuteRequestAsync(getFileRequest);
            Assert.Equal("Updated File Description", getFileResponse.Content.Description);

            await SkyManager.ApiClient.ExecuteRequestAsync(new DeleteFileRequest(fileId));
        }

        [Fact]
        public async Task TestGetFiles() {
            var fileInfo = new System.IO.FileInfo(Path.Join(".", "testFiles", "test.png"));
            var uploadFileResponse = await SkyManager.MediaClient.UploadFile(fileInfo, "File 1", "File 1 Description");
            var file1Id = uploadFileResponse.FileId;
            var file1Etag = uploadFileResponse.ETag;

            var fileUpdate = new FileUpdate().SetDefaults();
            fileUpdate.Description = "New File 1 Description";
            fileUpdate.Filename = "newfile.png";
            fileUpdate.Properties = new Properties();
            fileUpdate.Properties.Add("testProp", "testValue");
            fileUpdate.Tags = new List<string>(){"tag1"};
            fileUpdate.Title = "New File 1";

            Assert.NotNull(fileUpdate.ToJson());
            Assert.NotNull(fileUpdate.ToString());

            var updateFileRequest = new UpdateFileRequest(fileUpdate, file1Id);
            updateFileRequest.AdditionalRequestHeaders.Add(new KeyValuePair<string, string>("If-Match", file1Etag));
            var updateFileResponse = await SkyManager.ApiClient.ExecuteRequestAsync(updateFileRequest);
            Assert.Equal(HttpStatusCode.NoContent, updateFileResponse.StatusCode);

            var getFilesRequest = new GetFilesRequest();
            getFilesRequest.AddCreatedByQuery(SkyManager.IntegrationId);
            getFilesRequest.AddCreatedFromQuery(DateTime.MinValue.ToString("yyyy-MM-dd'T'HH:mm:ss.fffzzz"));
            getFilesRequest.AddCreatedToQuery(DateTime.MaxValue.ToString("yyyy-MM-dd'T'HH:mm:ss.fffzzz"));
            getFilesRequest.AddUpdatedFromQuery(DateTime.MinValue.ToString("yyyy-MM-dd'T'HH:mm:ss.fffzzz"));
            getFilesRequest.AddUpdatedToQuery(DateTime.MaxValue.ToString("yyyy-MM-dd'T'HH:mm:ss.fffzzz"));
            getFilesRequest.AddExcludeTagsQuery("");
            getFilesRequest.AddLimitQuery(1);
            getFilesRequest.AddPageQuery(0);
            getFilesRequest.AddPropQuery("testProp", "testValue");
            getFilesRequest.AddSortQuery("created");
            getFilesRequest.AddStartQuery(0);
            getFilesRequest.AddTagsQuery("tag1");
            getFilesRequest.AddTypeQuery("image");
            var getFilesResponse = await SkyManager.ApiClient.ExecuteRequestAsync(getFilesRequest);

            Assert.Single(getFilesResponse.Content);

            await SkyManager.ApiClient.ExecuteRequestAsync(new DeleteFileRequest(file1Id));
        }

    }
}
