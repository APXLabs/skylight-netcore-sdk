﻿using System;
using System.Threading.Tasks;
using Skylight.Client;

class Program
{
    static async Task Main(string[] args)
    {
        var connection = new ConnectionInfo(username, password, realm, apiUri);
        
        var ApiClient = new ApiClient(connection);

        var getRequest = new Skylight.Api.Media.V3.GetListFileInfosRequest();
        var result = await ApiClient.ExecuteRequestAsync(getRequest);
        foreach(Skylight.Api.Media.V3.Models.FileInfo info in result.Content){
            Console.WriteLine(info.Filename);
        }
        Console.WriteLine("End of example");
    }
}

