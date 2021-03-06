﻿
using System.Net.Http.Headers;
using System.IO;
using System;
using Skylight.Client;
using System.Threading.Tasks;
using Skylight.Sdk;
using MQTTnet.Client.Connecting;
using MQTTnet.Client.Disconnecting;
using Skylight.Mqtt;
using System.Threading;
using System.Configuration;  
using log4net;
using log4net.Config;
using System.Reflection;
using Skylight.Api.Integrations.V1.IntegrationsRequests;
using Skylight.Api.Integrations.V1.Models;

class Program
{
    public static Manager SkyManager;
    private static readonly log4net.ILog Logger = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
    
    static async Task Main(string[] args)
    {

        SetupLogConfig();
        SetupAppConfig();

        //Create our manager and point it to our credentials file
        //We leave the parameter blank, so that it looks for the `credentials.json` in the `config` directory.
        SkyManager = new Manager();
        
        //Connect to Skylight
        await SkyManager.Connect();
    
        //Subscribe to MQTT events
        await SubscribeToSkylightEvents();

        //Start listening to MQTT events
        await SkyManager.StartListening();

        //Example of using a setting from App.config
        var extensionName = ConfigurationManager.AppSettings["ExtensionName"];
        Logger.Info(extensionName + " is now running and connected to " + SkyManager.Domain);
        
        await GetAndListIntegrations();

        //Wait forever (at least, until the program is stopped)
        SpinWait.SpinUntil(() => false);

    }

    static void SetupLogConfig() {
        var logRepository = LogManager.GetRepository(Assembly.GetEntryAssembly());
        XmlConfigurator.Configure(logRepository, new FileInfo(Path.Join("config", "log4net.config")));
        Logger.Info("Logging framework configured.");  
    }

    static void SetupAppConfig() {
        //Open config
        System.Configuration.Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);

        //Update appconfig file path
        config.AppSettings.File = Path.Join("config", "App.config");

        //Save the configuration file.
        config.Save(ConfigurationSaveMode.Modified);

        //Force a reload in memory of the changed section.
        ConfigurationManager.RefreshSection("appSettings");
    }

    //In this method, we subscribe to Skylight events. In particular for this example, we're most interested in listening for the `Mark Complete` event.
    static async Task SubscribeToSkylightEvents() {
        SkyManager.MessagingClient.Connected += (object sender, MqttClientConnectedEventArgs args) => {
            Logger.Info("Skylight MQTT client connected.");
        };

        SkyManager.MessagingClient.Disconnected += (object sender, MqttClientDisconnectedEventArgs args) => {
            Logger.Info("Skylight MQTT client disconnected.");
            Logger.Info(args.Exception.Message);
        };

        SkyManager.MessagingClient.TopicSubscribed += (object sender, MqttMsgSubscribedEventArgs args) => {
            Logger.Info("Skylight MQTT client subscribed to: " + args.Topic);
        };

        SkyManager.MessagingClient.MessageReceived += (object sender, MessageReceivedEventArgs args) => {
            //Logger.Info("Skylight Message received on topic " + args.Topic + " " + args.Message); //Uncomment this for more verbose logging of Skylight event messages
        };

        SkyManager.MessagingClient.CardUpdated += async (object sender, CardUpdatedEventArgs args) => { await CardUpdated(sender, args); };

    }

    static async Task CardUpdated(object sender, CardUpdatedEventArgs args) { }

    static async Task GetAndListIntegrations() {
        Console.WriteLine("Retrieving integrations");
        var getIntegrationsRequest = new GetIntegrationsRequest();
            
        var getIntegrationsResponse = await SkyManager.ApiClient.ExecuteRequestAsync(getIntegrationsRequest);
        Logger.Info("Found " + getIntegrationsResponse.Content.Count + " integrations.");
        foreach(var integration in getIntegrationsResponse.Content){
            Logger.Info("Integration:");
            Logger.Info(integration.Name + " " + integration.Id);
        }
    }
}

