using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace PizzaFunctions
{
    public static class updateOrderStatus
    {
        private static readonly string KeyVaultName = Environment.GetEnvironmentVariable("KEYVAULT_NAME");
        private static readonly string KeyVaultUri = $"https://{KeyVaultName}.vault.azure.net/";

        [FunctionName("updateOrderStatus")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("Processing order status");

            var client = new SecretClient(new Uri(KeyVaultUri), new DefaultAzureCredential());
            string cosmosDbConnectionString = (await client.GetSecretAsync("PizzaOrderCosmos")).Value.Value;

            try
            {

            using (CosmosClient cosmosClient = new CosmosClient(cosmosDbConnectionString))
            {
                var database = cosmosClient.GetDatabase("Resturant");
                var container = database.GetContainer("Orders");

                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                dynamic data = JsonConvert.DeserializeObject(requestBody);

                if (data?.OrderId == null || data?.OrderStatus == null)
                {
                    log.LogWarning("Missing orderId or status in request");
                    return new BadRequestObjectResult(new { message = "orderNo and status is required" });
                }

                    string orderId = data.OrderId.ToString().Trim();
                    string newStatus = data.OrderStatus.ToString().Trim();

                    log.LogInformation($"{orderId} {newStatus}");

                try
                {
                        // 🔍 Hämta order med SQL-query (GAMLA metoden)
                        //var query = $"SELECT * FROM c WHERE c.OrderId = '{orderId}'";
                        //var iterator = container.GetItemQueryIterator<dynamic>(query);

                        //var response = await iterator.ReadNextAsync();
                        //var order = response.Resource.FirstOrDefault();
                        var response = await container.ReadItemAsync<dynamic>(orderId, new PartitionKey(orderId));
                        var order = response.Resource;

                        if (order == null)
                        {
                            log.LogWarning($"❌ Order {orderId} not found.");
                            return new NotFoundObjectResult(new { message = "Order not found." });
                        }

                        order.OrderStatus = newStatus;

                    await container.UpsertItemAsync(order, new PartitionKey(orderId));

                    return new OkObjectResult(new
                    {
                        message = "Order status updated successfully",
                        orderId = orderId,
                        status = newStatus
                    });
                }
                    catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        log.LogWarning($"Order {orderId} not found in CosmosDb.");
                        return new NotFoundObjectResult(new { message = "Order not found." });
                    }

                }
            }
            catch (Exception ex)
            {
                log.LogError($"Error updating order: {ex.Message}");
                return new StatusCodeResult(500);
            }
        }
    }
}
