
using System.IO;
using System;
using Skylight.Client;
using System.Threading.Tasks;
using Skylight.Sdk;

namespace Media
{
    /*
        INFO: Throughout this example, there are comments that begin with @skydocs -- 
        these are tags used by the Skylight Developer Portal and are not necessary for
        this example to function.
     */
    class Program
    {
        public static Manager SkyManager;
        static async Task Main(string[] args)
        {
            //@skydocs.start(authentication.login)
            /*
                Create our manager by passing it the path to our credentials.json file.
                This json file has our API credentials copy-pasted from Skylight Web.
                The path is also optional; the constructor for the SDK's manager can also take 0 arguments, in which case it will search for a file called `credentials.json` in the root directory of the extension.
            */
            SkyManager = new Manager(Path.Combine("..", "..", "credentials.json"));
            //@skydocs.end()
            
            //Connect to Skylight
            await SkyManager.Connect();
            Console.WriteLine("Skylight connected");

            //Upload a file
            await UploadFile();

            //See the Hello World extension for an example of downloading a file that captured on a device
        }

        static async Task UploadFile() {
            //@skydocs.start(media.upload)
            //We upload a file by specifying its file path, title, and description
            //The SDK takes care of deciding between whether to use a singlepart or multipart upload.
            await SkyManager.MediaClient.UploadFile(new FileInfo(Path.Join(".", "files", "test.png")), "SDK Upload Test", "This is a file uploaded using the Skylight C# SDK");
            //@skydocs.end()
        }
    }
}
