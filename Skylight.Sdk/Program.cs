using System;
using System.IO;
using Newtonsoft.Json;
using Skylight.Client;

namespace Skylight.Sdk
{
    public class Manager
    {
        public ApiClient ApiClient;
        public Manager(string credentialsPath = "credentials.json") {
            
            //Read in our credentials
            StreamReader reader = new StreamReader(credentialsPath);
            string json = reader.ReadToEnd();
            reader.Close();
            dynamic credentials = JsonConvert.DeserializeObject(json);
            
            //Set up a new connection
            var connection = new ConnectionInfo((string)credentials.username, (string)credentials.password, (string)credentials.domain, (string)credentials.apiUrl);

            //Use the connection to create a client
            ApiClient = new ApiClient(connection);
        }
    }
}
