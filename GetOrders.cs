using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.Cosmos;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using System.Collections.Generic;

public static class GetOrders
{
    private static readonly string KeyVaultName = Environment.GetEnvironmentVariable("KEYVAULT_NAME");
    private static readonly string KeyVaultUri = $"https://{KeyVaultName}.vault.azure.net/";

    [FunctionName("GetOrders")]
    public static async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = null)] HttpRequest req,
        ILogger log)
    {
        log.LogInformation("Fetching orders from Cosmos DB via Key Vault.");
        log.LogInformation($"KeyVaultName: {KeyVaultName}");
        log.LogInformation($"Key Vault URI: {KeyVaultUri}");


        // Hämta Cosmos DB Connection String från Key Vault
        var client = new SecretClient(new Uri(KeyVaultUri), new DefaultAzureCredential());
        string cosmosDbConnectionString = (await client.GetSecretAsync("PizzaOrderCosmos")).Value.Value;

        // Skapa Cosmos DB-klient
        using (CosmosClient cosmosClient = new CosmosClient(cosmosDbConnectionString))
        {
            var database = cosmosClient.GetDatabase("Resturant");
            var container = database.GetContainer("Orders");

            var orders = new List<dynamic>();
            var query = "SELECT * FROM c";
            var iterator = container.GetItemQueryIterator<dynamic>(query);

            while (iterator.HasMoreResults)
            {
                foreach (var item in await iterator.ReadNextAsync())
                {
                    orders.Add(item);
                }
            }

            return new OkObjectResult(orders);
        }
    }
}
