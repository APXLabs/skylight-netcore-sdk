using System;
using System.IO;
using System.Threading.Tasks;
using MQTTnet.Client.Connecting;
using MQTTnet.Client.Disconnecting;
using Skylight.Mqtt;
using Skylight.Sdk;

namespace mqtt
{
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
                SkyManager = new Manager(Path.Combine("..", "..", "credentials.json"));
                //@skydocs.end()
            } catch { return; }
            
            //@skydocs.start(mqtt.lifecycle)
            SkyManager.MessagingClient.Connected += (object sender, MqttClientConnectedEventArgs args) => {
                Console.WriteLine("MQTT client connected.");
            };

            SkyManager.MessagingClient.Disconnected += (object sender, MqttClientDisconnectedEventArgs args) => {
                Console.WriteLine("MQTT client disconnected.");
                Console.WriteLine(args.Exception.Message);
            };

            SkyManager.MessagingClient.TopicSubscribed += (object sender, MqttMsgSubscribedEventArgs args) => {
                Console.WriteLine("MQTT client subscribed to: " + args.Topic);
            };

            SkyManager.MessagingClient.MessageReceived += (object sender, MessageReceivedEventArgs args) => {
                Console.WriteLine("Message received on topic " + args.Topic + " " + args.Message);
            };

            SkyManager.MessagingClient.CardUpdated += async (object sender, CardUpdatedEventArgs args) => { await CardUpdated(sender, args); };


            await SkyManager.StartListening();
            Console.ReadLine();
            //@skydocs.end()
        }

        static async Task CardUpdated(object sender, CardUpdatedEventArgs args) {
            var cardRequest = new Skylight.Api.Assignments.V1.CardRequests.GetCardRequest(args.AssignmentId, args.SequenceId, args.CardId);
            var response = await SkyManager.ApiClient.ExecuteRequestAsync(cardRequest);
            var card = response.Content;
            var cardComponentType = card.Component.GetType();
            if(cardComponentType == typeof(Skylight.Api.Assignments.V1.Models.ComponentCompletion)){

            }
        }
    }
}
