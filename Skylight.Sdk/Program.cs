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
        private ApiClient _apiClient;
        public ApiClient ApiClient {
            get {
                if(_apiClient == null) throw new Exception("ApiClient is null, please make sure Connect() is called right after instantiating the Manager.");
                return _apiClient;
            }
        }
        private MessagingClient _messagingClient;
        public MessagingClient MessagingClient {
            get {
                if(_messagingClient == null) throw new Exception("MessagingClient is null, please make sure Connect() is called right after instantiating the Manager.");
                return _messagingClient;
            }
        }

        private FileClient.FileTransferClient _mediaClient;
        public FileClient.FileTransferClient MediaClient {
            get {
                if(_mediaClient == null) throw new Exception("MediaClient is null, please make sure Connect() is called right after instantiating the Manager.");
                return _mediaClient;
            }
        }
        public string IntegrationId;
        public string Domain;
        public string ApiUrl;
        public string MqttUrl;
        public string Username;
        public string Password;

        private dynamic Credentials;
        private static ConnectionType MqttConnectionType = ConnectionType.Auto;
        public Manager(string credentialsPath = "credentials.json" ) {
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
            this.Setup((string)Credentials.id, (string)Credentials.username, (string)Credentials.password, (string)Credentials.domain, (string)Credentials.apiUrl, mqttUrl);
        }

        public Manager(string integrationId, string username, string password, string domain, string apiUrl, string mqttUrl) {
            this.Setup(integrationId, username, password, domain, apiUrl, mqttUrl);
        }

        private void Setup(string integrationId, string username, string password, string domain, string apiUrl, string mqttUrl) {
            //Set our integration id
            IntegrationId = integrationId;

            //Set our API Url
            ApiUrl = apiUrl;

            //Set our Mqtt Url
            MqttUrl = mqttUrl;

            //Set our username
            Username = username;

            //Set our password
            Password = password;

            //Set our domain
            Domain = domain;
        }

        public async Task Connect() {
            //Set up a new connection
            var connection = new ConnectionInfo(Username, Password, Domain, ApiUrl);

            //Use the connection to create a client
            _apiClient = new ApiClient(connection);
            
            //Test our connection
            await TestConnection();

            //Set up a new MQTT connection
            var mqttConnection = new MqttConnectionInfo(Username, Password, Domain, ApiUrl, MqttUrl, 30, MqttConnectionType);
        
            //Use the MQTT connection information to create a messaging client
            _messagingClient = new MessagingClient(mqttConnection);

            //Use our API client to create a media client
            _mediaClient = new FileTransferClient(_apiClient);

        }

        private async Task TestConnection() {
            try {
                await ApiClient.ExecuteRequestAsync(new Skylight.Api.Assignments.V1.APIRequests.GetApiRequest());
            } catch (Exception e) {
                throw new Exception("Connection to Skylight Web API failed. Please check that the username, password, and API URL are valid and that the extension can reach the Skylight server.");
            }
        }

        public async Task StartListening() {
            var mqttSuccess = await MessagingClient.StartListeningAsync();
            if(!mqttSuccess) {
                throw new Exception("MQTT connection failed. Please check that the MQTT URL is valid.");
            }
        }

        public static void SetMqttConnectionType(ConnectionType type) {
            MqttConnectionType = type;
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

        
        public string GetFileIdFromUri(string fileUri) {
            var fileUriSplit = fileUri.Split('/');
            if(fileUriSplit.Length < 2) throw new Exception("Error in getting file id from URI. URI is malformed.");
            return fileUriSplit[fileUriSplit.Length-2];
        }

        public string GetFileUriFromId(string fileId) {
            return $"{ApiUrl}{Skylight.Api.Media.V3.Constants.BaseEndpointPath}/files/{fileId}/content";
        }
    }
}
