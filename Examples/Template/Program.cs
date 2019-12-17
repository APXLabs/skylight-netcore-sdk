
using System.IO;
using System;
using Skylight.Client;
using System.Threading.Tasks;
using Skylight.Sdk;

class Program
{
    public static Manager Manager;
    static async Task Main(string[] args)
    {
        //Create our manager and point it to our credentials file
        Manager = new Manager(Path.Combine("..", "..", "credentials.json"));
        
        //This is a sample GET request to list all the files in the domain
        var getRequest = new Skylight.Api.Media.V3.GetListFileInfosRequest();
        var result = await Manager.ApiClient.ExecuteRequestAsync(getRequest);
        foreach(Skylight.Api.Media.V3.Models.FileInfo info in result.Content){
            Console.WriteLine(info.Filename);
        }
    }
}

