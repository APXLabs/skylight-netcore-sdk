using System.Runtime.InteropServices;
using System.Collections.ObjectModel;
using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Net;
using System.Linq;
using Newtonsoft.Json;
using Skylight.Client;
using Skylight.Mqtt;
using System.Threading.Tasks;
using Skylight.FileClient;
using Skylight.Utilities;
using Skylight.Utilities.Extensions;
using Skylight.Utilities.Requests;
using Skylight.Utilities.Responses;
using Skylight.Api.Assignments.V1.AssignmentRequests;
using Skylight.Api.Assignments.V1.SequenceRequests;
using Skylight.Api.Assignments.V1.CardRequests;
using Skylight.Api.Assignments.V1.Models;

namespace Skylight.Sdk
{

    public class Manager
    {
        public class SkylightApiClient : ApiClient {

            private readonly int _maxApiPayloadSize;
            public SkylightApiClient(ConnectionInfo connectionInfo, int MaxApiPayloadSize) : base(connectionInfo)
            {
                _maxApiPayloadSize = MaxApiPayloadSize;   
            }

            
            private int GetEstimatedPayloadSize(object payload)
            {
                var serialized = JsonConvert.SerializeObject(payload);
                return serialized.Length * sizeof(char);
            }
            
            /// <summary>
            /// Create an assignment.
            /// </summary>
            /// <param name="assignment">assignment to create</param>
            /// <returns>Created assignment</returns>
            private async Task<Assignment> CreateAssignment(AssignmentNew assignment)
            {
                var request = new CreateAssignmentRequest(assignment);
                var response = await base.ExecuteRequestAsync(request);

                return response.Content;
            }

            /// <summary>
            /// This method should be used for creating assignment that exceeds API maximum payload size
            /// It will split creating of the assignment in multiple parts
            /// In order to use this method ApiHelper should be provided with maximum payload size that API supports
            /// </summary>
            /// <param name="assignment">Assignment model to create</param>
            /// <returns>Created assignment</returns>
            /// <exception cref="ArgumentNullException">Thrown when assignment model is null</exception>
            /// <exception cref="ArgumentException">Thrown when maximum payload size that API supports was not provided</exception>
            public async Task<Assignment> CreateAssignmentInMultipleRequests(AssignmentNew assignment)
            {
                if (assignment == null)
                    throw new ArgumentNullException(nameof(assignment));
                
                var payloadSize = GetEstimatedPayloadSize(assignment);

                if (payloadSize >= _maxApiPayloadSize)
                {
                    var sequences = assignment.Sequences.ToList();
                    assignment.Sequences = new List<SequenceNew>();
                    
                    var createdAssignment = await CreateAssignment(assignment);
                    
                    await ProcessSequencesCreation(createdAssignment.Id, sequences);
                    return createdAssignment;
                }

                return await CreateAssignment(assignment);
            }
                
                    
            private async Task ProcessSequencesCreation(string assignmentId, List<SequenceNew> sequences)
            {
                if (sequences == null)
                    throw new ArgumentNullException(nameof(sequences));

                // filter sequences that fit max payload size to create groups of them
                var payloadFitSequences = sequences.Where(sequence => GetEstimatedPayloadSize(sequence) < _maxApiPayloadSize).ToList();
                if (payloadFitSequences.Any())
                {
                    // split sequences into groups
                    var groups = PackFitSequencesIntoGroups(payloadFitSequences).ToList();
                    var groupsCreationTasks = groups.Select(group => CreateSequences(assignmentId, group));

                    await Task.WhenAll(groupsCreationTasks);
                }
                        
                var sequenceCreationTasks = sequences.Except(payloadFitSequences).Select(async sequence =>
                {
                    var sequenceCards = sequence.Cards.ToList();
                    sequence.Cards = new List<CardNew>();
                    var createdSequence = await CreateSequence(assignmentId, sequence);

                    await ProcessSequenceCardsCreation(assignmentId, createdSequence.Id, sequenceCards);
                });
                
                await Task.WhenAll(sequenceCreationTasks);
            }

            /// <summary>
            /// Create sequences
            /// </summary>
            /// <param name="assignmentId">Id of the assignment</param>
            /// <param name="sequences">List of sequences</param>
            public async Task<IEnumerable<Sequence>> CreateSequences(string assignmentId, List<SequenceNew> sequences)
            {
                var request = new CreateSequencesRequest(sequences, assignmentId);
                var response = await base.ExecuteRequestAsync(request);

                var createdSequences = response.Content;
                
                return createdSequences;
            }

            
            /// <summary>
            /// Create single sequence.
            /// </summary>
            /// <param name="assignmentId">Id of the assignment</param>
            /// <param name="sequence">sequence to create</param>
            /// <returns>Created sequence</returns>
            public async Task<Sequence> CreateSequence(string assignmentId, SequenceNew sequence)
            {
                var request = new CreateSequenceRequest(sequence, assignmentId);
                var response = await base.ExecuteRequestAsync(request);

                return response.Content;
            }

            private IEnumerable<List<SequenceNew>> PackFitSequencesIntoGroups(List<SequenceNew> sequences)
            {
                var maxGroupsCount = sequences.Count;
                var groupPayloadSizes = Enumerable.Repeat(_maxApiPayloadSize, maxGroupsCount).ToArray();
                
                // max groups count is equal to sequences count
                var groups = new List<List<SequenceNew>>();
                sequences.ForEach(_ => groups.Add(new List<SequenceNew>()));
                
                // sort sequences in descending payload size order by to get grouping much more effective
                sequences.Sort((seq1, seq2) => GetEstimatedPayloadSize(seq2).CompareTo(GetEstimatedPayloadSize(seq1)));

                for (var sequence = 0; sequence < sequences.Count; ++sequence)
                {
                    for (var group = 0; group < maxGroupsCount; ++group)
                    {
                        var payloadSize = GetEstimatedPayloadSize(sequences[sequence]);
                        
                        if (groupPayloadSizes[group] - payloadSize >= 0)
                        {
                            groups[group].Add(sequences[sequence]);
                            
                            groupPayloadSizes[group] -= payloadSize;
                            break;
                        }
                    }
                }

                return groups.Where(group => group.Count > 0);
            }
            
            private async Task ProcessSequenceCardsCreation(string assignmentId, string sequenceId, List<CardNew> cards)
            {
                if (cards == null)
                    throw new ArgumentNullException(nameof(cards));

                var creationTasks = new List<Task>();
                
                await ProcessCardsCreation(assignmentId, sequenceId, cards, creationTasks);
                await Task.WhenAll(creationTasks);
            }

            private async Task ProcessCardsCreation(string assignmentId, string sequenceId, List<CardNew> cards, List<Task> cardCreationTasks)
            {
                var firstHalf = cards.Take((cards.Count + 1) / 2).ToList();
                var firstHalfSize = GetEstimatedPayloadSize(firstHalf);

                var secondHalf = cards.Skip((cards.Count + 1) / 2).ToList();
                var secondHalfSize = GetEstimatedPayloadSize(secondHalf);

                if (firstHalfSize < _maxApiPayloadSize)
                {
                    cardCreationTasks.Add(CreateCards(assignmentId, sequenceId, firstHalf));
                }
                else
                {
                    await ProcessCardsCreation(assignmentId, sequenceId, firstHalf, cardCreationTasks);
                }
                
                if (secondHalfSize < _maxApiPayloadSize)
                {
                    cardCreationTasks.Add(CreateCards(assignmentId, sequenceId, secondHalf));
                }
                else
                {
                    await ProcessCardsCreation(assignmentId, sequenceId, secondHalf, cardCreationTasks);
                }
            }

            
            /// <summary>
            /// Create cards.
            /// </summary>
            /// <param name="assignmentId">Id of the assignment</param>
            /// <param name="sequenceId">Id of the sequence</param>
            /// <param name="cards">List of cards</param>
            /// <returns>List of created cards</returns>
            public async Task<IEnumerable<Card>> CreateCards(string assignmentId, string sequenceId, List<CardNew> cards)
            {
                var request = new CreateCardsRequest(cards, assignmentId, sequenceId);
                var response = await base.ExecuteRequestAsync(request);

                return response.Content;
            }

            /// <summary>
            /// Executes an ApiRequest
            /// </summary>
            /// <typeparam name="TResult">The type of response to send back specified by the ApiRequest</typeparam>
            /// <param name="request">The ApiRequest to execute</param>
            /// <returns>An ApiResponse with Content of type TResult</returns>
            /// <exception cref="ApiException">Thrown if there was an error in the request</exception>
            public new async Task<ApiResponse<TResult>> ExecuteRequestAsync<TResult>(ApiRequest<TResult> request)
            {
                if(request.Payload != null && request.Payload.Content is AssignmentNew assignmentNew) {
                    var assignment = await CreateAssignmentInMultipleRequests(assignmentNew);
                    if(assignment is TResult assignmentResult) {
                        return new ApiResponse<TResult>(HttpStatusCode.Created, assignmentResult);
                    }
                }
                return await base.ExecuteRequestAsync(request);
            }

        }
        private SkylightApiClient _apiClient;
        public SkylightApiClient ApiClient {
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
        private static bool Connected = false;
        /// <summary>
        /// Maximum payload size that API supports (in bytes)
        /// </summary>
        private static int MaxApiPayloadSize = 7 * 1024 * 1024;
        public Manager(string credentialsPath = "credentials.json") {
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
            if(apiUrl.EndsWith("/"))apiUrl = apiUrl.Substring(0, apiUrl.Length-1);
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
            _apiClient = new SkylightApiClient(connection, MaxApiPayloadSize);
            
            //Test our connection
            await TestConnection();

            //Set up a new MQTT connection
            var mqttConnection = new MqttConnectionInfo(Username, Password, Domain, ApiUrl, MqttUrl, 30, MqttConnectionType);
        
            //Use the MQTT connection information to create a messaging client
            _messagingClient = new MessagingClient(mqttConnection);

            //Use our API client to create a media client
            _mediaClient = new FileTransferClient(_apiClient, MaxApiPayloadSize);

            Connected = true;

        }

        private async Task TestConnection() {
            try {
                await _apiClient.ExecuteRequestAsync(new Skylight.Api.Assignments.V1.APIRequests.GetApiRequest());
            } catch (Exception e) {
                throw e;//new Exception("Connection to Skylight Web API failed. Please check that the username, password, and API URL are valid and that the extension can reach the Skylight server.");
            }
        }

        public async Task StartListening() {
            var mqttSuccess = await MessagingClient.StartListeningAsync();
            if(!mqttSuccess) {
                throw new Exception("MQTT connection failed. Please check that the MQTT URL is valid.");
            }
        }

        public static void SetMqttConnectionType(ConnectionType type) {
            if(Connected) throw new Exception("Please call SetMqttConnectionType before calling Connect.");
            MqttConnectionType = type;
        }

        public static void SetMaxApiPayloadSize(int size) {
            if(Connected) throw new Exception("Please call SetMaxApiPayloadSize before calling Connect.");
            if (size <= 0)
                throw new ArgumentException("For creating assignment in multiple requests ApiHelper should be provided with correct maximum payload size that API supports");
            MaxApiPayloadSize = size;
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
