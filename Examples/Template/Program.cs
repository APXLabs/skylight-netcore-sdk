using System;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Skylight.Client;

class Program
{
    static async Task Main(string[] args)
    {
        //Read in our credentials
        StreamReader reader = new StreamReader("credentials.json");
        string json = reader.ReadToEnd();
        reader.Close();
        dynamic credentials = JsonConvert.DeserializeObject(json);
        
        //Set up a new connection
        var connection = new ConnectionInfo((string)credentials.username, (string)credentials.password, (string)credentials.domain, (string)credentials.apiUrl);

        //Use the connection to create a client
        var ApiClient = new ApiClient(connection);

        //This is a sample GET request to list all the files in the domain
        var getRequest = new Skylight.Api.Media.V3.GetListFileInfosRequest();
        var result = await ApiClient.ExecuteRequestAsync(getRequest);
        foreach(Skylight.Api.Media.V3.Models.FileInfo info in result.Content){
            Console.WriteLine(info.Filename);
        }
    }
}

