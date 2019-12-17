﻿using System.Collections.ObjectModel;
using System;
using System.IO;
using Newtonsoft.Json;
using Skylight.Client;

namespace Skylight.Sdk
{
    public class Manager
    {
        public ApiClient ApiClient;

        private dynamic Credentials;
        public Manager(string credentialsPath = "credentials.json" ) {
            if(!this.ReadCredentials(credentialsPath)) throw new Exception("Credentials Error");
            
            //Set up a new connection
            var connection = new ConnectionInfo((string)Credentials.username, (string)Credentials.password, (string)Credentials.domain, (string)Credentials.apiUrl);

            //Use the connection to create a client
            ApiClient = new ApiClient(connection);
        }

        private bool ReadCredentials(string credentialsPath) {
            try {
                //Read in our credentials
                using(StreamReader reader = new StreamReader(credentialsPath)){
                    string json = reader.ReadToEnd();
                    if(String.IsNullOrWhiteSpace(json))throw new Exception();
                    Credentials = JsonConvert.DeserializeObject(json);
                    return true;
                }
            } catch {
                //Either the file doesn't exist, or the file's contents are corrupted
                Console.Error.WriteLine("Please ensure credentials.json path points to a file with valid Skylight API credentials. If using the Skytool CLI, copy the API credentials to the credentials.json file in the root working directory.");
                return false;
            }
        }
    }
}
