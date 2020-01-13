
using System.IO;
using System;
using Skylight.Client;
using System.Threading.Tasks;
using Skylight.Sdk;

namespace Media
{
    class Program
    {
        public static Manager Manager;
        static async Task Main(string[] args)
        {
            try {
                //@skydocs.start(authentication.login)
                /*
                    Create our manager by passing it the path to our credentials.json file.
                    This json file has our API credentials copy-pasted from Skylight Web.
                    The path is also optional; the constructor for the SDK's manager can also take 0 arguments, in which case it will search for a file called `credentials.json` in the root directory of the extension.
                */
                Manager = new Manager(Path.Combine("..", "..", "credentials.json"));
                //@skydocs.end()
            } catch { return; }

            //@skydocs.start(media.upload)
            //We upload a file by specifying its file path, title, and description
            //The SDK takes care of deciding between whether to use a singlepart or multipart upload.
            await Manager.MediaClient.UploadFile(new FileInfo(Path.Join(".", "files", "test.png")), "SDK Upload Test", "This is a file uploaded using the Skylight C# SDK");
            //@skydocs.end()
        }
    }
}
