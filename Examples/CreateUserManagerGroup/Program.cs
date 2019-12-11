
using System.IO;
using System;
using Microsoft.Extensions.Configuration;
using Skylight.Client;
using System.Threading.Tasks;
using Skylight.Api.Assignments.V1.Models;

namespace CreateUserManagerGroup
{
    class Program
    {
        public static ApiClient ApiClient;
        static async Task Main(string[] args)
        {
            var path = Directory.GetCurrentDirectory();

            var config = new ConfigurationBuilder()
                .SetBasePath(Path.Combine(path, ".."))
                .AddJsonFile("exampleSettings.json")
                .Build();
            
            var username = config["username"];
            var password = config["password"];
            var realm = config["domain"];
            var apiUri = config["apiUrl"];
            var mqttUrl = config["mqttUrl"];
            
            var connection = new ConnectionInfo(username, password, realm, apiUri);
            
            ApiClient = new ApiClient(connection);

            var getRequest = new Skylight.Api.Media.V3.GetListFileInfosRequest();
            var result = await ApiClient.ExecuteRequestAsync(getRequest);
            foreach(Skylight.Api.Media.V3.Models.FileInfo info in result.Content){
                Console.WriteLine(info.Filename);
            }
            Console.WriteLine("End of example");
            
        }
    }
}
