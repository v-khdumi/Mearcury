﻿using CodeHollow.AzureBillingApi;
using Mearcury.Core;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Mearcury.Azure
{
    public static class Extensions
    {
        public static string Limit(this string source, int length)
        {
            if (string.IsNullOrWhiteSpace(source))
                return source;

            return source.Length < length ? source : source.Substring(0, length);
        }

        public static string Ellipsis(this string source, int length)
        {
            if (string.IsNullOrWhiteSpace(source))
                return source;

            return source.Length < length ? source : (source.Substring(0, length - 3) + "...");
        }

        public static async Task<Resources> GetExistingResources(this AzureClient client, Resources existing = null)
        {
            if (client == null)
                throw new ArgumentNullException(nameof(client));

            var resources = existing ?? new Resources();

            Type managementType = client.Management.GetType();
            System.Reflection.FieldInfo resourceManagerField = managementType.GetField("resourceManager", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var resourceManager = (IResourceManager)resourceManagerField.GetValue(client.Management);

            Console.WriteLine("Azure resources are being loaded...");

            var allResources = await resourceManager.GenericResources.ListAsync(true);

            foreach (var resource in allResources)
                resources.Add(resource.Id, resource.Name, resource.ResourceGroupName, resource.Type);

            Console.WriteLine("Azure resource load completed!");

            return resources;
        }

        public static async Task<Resources> GetBillingResources(this AzureClient client, DateTime startDate = default(DateTime), DateTime endDate = default(DateTime), Resources existing = null)
        {
            if (client == null)
                throw new ArgumentNullException(nameof(client));

            if (startDate == default(DateTime))
                startDate = DateTime.Now.Subtract(TimeSpan.FromHours(2));

            if (endDate == default(DateTime))
                endDate = DateTime.Now.Subtract(TimeSpan.FromHours(1));

            var subscription = client.Subscription;
            var resources = existing ?? new Resources();

            Console.WriteLine("Billing for Azure resources are being loaded...");

            var rateCardData = client.Billing.GetRateCardData(
                    subscription.OfferId,
                    subscription.Currency,
                    subscription.Locale,
                    subscription.Region,
                    await client.GetManagementTokenAsync()
                );

            var usageData =
                client.Billing.GetUsageData(
                    startDate,
                    endDate,
                    CodeHollow.AzureBillingApi.Usage.AggregationGranularity.Hourly,
                    true,
                    await client.GetManagementTokenAsync()
                );

            var resourceCostData =
                Client.Combine(rateCardData, usageData);

            foreach (var cost in resourceCostData.Costs.GetCostsByResourceName())
            {
                var resourceName = cost.Key;
                var resourceCost = cost.Value;

                if (resources.ExistsByName(resourceName))
                {
                    resources.GetByName(resourceName).First().Cost += resourceCost.GetTotalCosts();
                    continue;
                }

                resources.Add(Guid.NewGuid().ToString(), resourceName, "deleted", "deleted");
            }

            Console.WriteLine("Billing for Azure resource load completed!");

            return resources;
        }
    }
}
