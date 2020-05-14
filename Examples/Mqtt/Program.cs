using System;
using System.IO;
using System.Threading.Tasks;
using MQTTnet.Client.Connecting;
using MQTTnet.Client.Disconnecting;
using Skylight.Mqtt;
using Skylight.Sdk;
using Skylight.Api.Assignments.V1.Models;

namespace Mqtt
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
            //@skydocs.start(authentication.login)
            /*
                Create our manager by passing it the path to our credentials.json file.
                This json file has our API credentials copy-pasted from Skylight Web.
                The path is also optional; the constructor for the SDK's manager can also take 0 arguments, in which case it will search for a file called `credentials.json` in the root directory of the extension.
            */
            SkyManager = new Manager(Path.Combine("..", "..", "credentials.json"));
            //@skydocs.end()
            
            //@skydocs.start(mqtt.lifecycle)
            //Connect to Skylight
            await SkyManager.Connect();
            Console.WriteLine("Skylight connected");
            
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

            //All messages will be routed to this callback, so use this for any messages that don't have a supported event handler
            SkyManager.MessagingClient.MessageReceived += (object sender, MessageReceivedEventArgs args) => {
                Console.WriteLine("Message received on topic " + args.Topic + " " + args.Message);
            };

            //This is an example of a supported event handler
            SkyManager.MessagingClient.CardUpdated += async (object sender, CardUpdatedEventArgs args) => { await CardUpdated(sender, args); };

            await SkyManager.StartListening(); //IMPORTANT: This line starts the MQTT client and is necessary for receiving MQTT messages
            Console.ReadLine(); //This line keeps our program alive so that it can listen to messages
            //@skydocs.end()
        }

        //@skydocs.start(mqtt.cardupdated.componenttype)
        static async Task CardUpdated(object sender, CardUpdatedEventArgs args) {
            var cardRequest = new Skylight.Api.Assignments.V1.CardRequests.GetCardRequest(args.AssignmentId, args.SequenceId, args.CardId);
            var response = await SkyManager.ApiClient.ExecuteRequestAsync(cardRequest);
            var card = response.Content;
            //We could use the card tags (card.Tags), card id (args.CardId), or the component type (see below) of the card to see what action we should take. See the Hello World example for how to use card tags.
            switch(card.Component.ComponentType){
                case ComponentType.CapturePhoto:
                    //For a more in-depth example of downloading a captured photo, see the Hello World example.
                    Console.WriteLine("Photo captured.");
                    break;
                case ComponentType.Completion:
                    Console.WriteLine("Completion card selected.");
                    break;
                case ComponentType.Scanning:
                    var scanComponent = (ComponentScanning)card.Component;
                    var scanResult = scanComponent.Result;
                    Console.WriteLine("Scan component updated with scan result: " + scanResult);
                    break;
                default:
                    break;
            }
        }
        //@skydocs.end()
    }
}
