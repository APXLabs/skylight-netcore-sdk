using System;
using Skylight.Sdk;
using System.Threading.Tasks;
using System.IO;

namespace Tests.Authentication
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
            try {
                //@skydocs.start(authentication.login)
                /*
                    Create our manager by passing it the path to our credentials.json file.
                    This json file has our API credentials copy-pasted from Skylight Web.
                    The path is also optional; the constructor for the SDK's manager can also take 0 arguments, in which case it will search for a file called `credentials.json` in the root directory of the extension.
                */
                SkyManager = new Manager(Path.Combine("..", "..", "..", "credentials.json"));
                //@skydocs.end()
            } catch { return; }

            await RunTests();
            Console.WriteLine("All authentication tests ran successfully.");
        }

        static async Task RunTests() {
            
        }
    }
}
