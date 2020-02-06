using System.Runtime.InteropServices;
using System.Collections.ObjectModel;
using System;
using System.IO;
using Newtonsoft.Json;
using Skylight.Client;
using Skylight.Mqtt;
using System.Threading.Tasks;
using Skylight.FileClient;

namespace Skylight.Sdk
{
    public class Manager
    {
        public ApiClient ApiClient;
        public MessagingClient MessagingClient;
        public FileClient.FileTransferClient MediaClient;
        public string IntegrationId;
        public string Domain;

        private dynamic Credentials;
        public Manager(string credentialsPath = "credentials.json", ConnectionType mqttConnectionType = ConnectionType.Auto ) {
            var potentialCredentialsPaths = new String[]{credentialsPath, "credentials.json", Path.Combine("config", "credentials.json")};
            var successfullyReadCredentials = false;
            foreach(var potentialCredentialPath in potentialCredentialsPaths){
                successfullyReadCredentials = this.ReadCredentials(potentialCredentialPath);
                if(successfullyReadCredentials)break;
            } 
            if(!successfullyReadCredentials) {
                Console.Error.WriteLine("Please ensure the credentials.json path points to a file with valid Skylight API credentials.");
                throw new Exception("Credentials Error");
            }
            var mqttUrl = (string)Credentials.mqttUrl;
            mqttUrl = mqttUrl.Substring(mqttUrl.IndexOf("://") + 3);
            this.Setup((string)Credentials.id, (string)Credentials.username, (string)Credentials.password, (string)Credentials.domain, (string)Credentials.apiUrl, mqttUrl, mqttConnectionType);
        }

        public Manager(string integrationId, string username, string password, string domain, string apiUrl, string mqttUrl) {
            this.Setup(integrationId, username, password, domain, apiUrl, mqttUrl);
        }

        private void Setup(string integrationId, string username, string password, string domain, string apiUrl, string mqttUrl, ConnectionType mqttConnectionType = ConnectionType.Auto) {
            //Set up a new connection
            var connection = new ConnectionInfo(username, password, domain, apiUrl);

            //Use the connection to create a client
            ApiClient = new ApiClient(connection);

            //Set up a new MQTT connection
            var mqttConnection = new MqttConnectionInfo(username, password, domain, apiUrl, mqttUrl, 30, mqttConnectionType);
        
            //Use the MQTT connection information to create a messaging client
            MessagingClient = new MessagingClient(mqttConnection);

            //Use our API client to create a media client
            MediaClient = new FileTransferClient(ApiClient);

            //Set our integration id
            IntegrationId = integrationId;

            //Set our domain
            Domain = domain;
        }

        public async Task StartListening() {
            await MessagingClient.StartListeningAsync();
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
                //Console.Error.WriteLine("Please ensure credentials.json path points to a file with valid Skylight API credentials. If using the Skytool CLI, copy the API credentials to the credentials.json file in the root working directory.");
                return false;
            }
        }
    }
}
