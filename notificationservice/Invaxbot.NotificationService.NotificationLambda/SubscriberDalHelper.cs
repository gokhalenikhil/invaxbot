using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.DynamoDBv2.Model;
using Amazon.Runtime;
using Amazon.Runtime.CredentialManagement;
using Invaxbot.NotificationService.NotificationLambda.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Invaxbot.NotificationService.NotificationLambda
{
    public class SubscriberDalHelper
    {
        private static readonly string _tableName = "bot-subscriber";

        public static IEnumerable<Subscriber> GetSubscribers(AmazonDynamoDBClient dynamoClient, string shortPincode)
        {
            List<Subscriber> subscribersToReturn = null;
            var scanRequest = new ScanRequest
            {
                TableName = _tableName,
                // Optional parameters.
                ExpressionAttributeValues = new Dictionary<string, AttributeValue> 
                {
                    {":val", new AttributeValue { S = shortPincode }}
                },
                FilterExpression = "short_pin = :val",
                ProjectionExpression = null
            };

            var response = dynamoClient.ScanAsync(scanRequest).Result;
            if (response.Items != null && response.Items.Any())
            {
                subscribersToReturn = new List<Subscriber>();
                foreach (var item in response.Items)
                {
                    var subscriber = new Subscriber
                    {
                        id = item["id"].S,
                        pincode = item["pincode"].S,
                        min_age_18 = item["min_age_18"].BOOL,
                        min_age_45 = item["min_age_45"].BOOL,
                        short_pin = item["short_pin"].S,
                        active = item["active"].BOOL,
                        last_alerted = long.Parse(item.ContainsKey("last_alerted") ? item["last_alerted"].N : "0"),
                        alert_center_ids = item.ContainsKey("alert_center_ids") ? item["alert_center_ids"].S : string.Empty
                    };
                    subscribersToReturn.Add(subscriber);
                }
            }
            return subscribersToReturn;
        }

        public static void UpdateSubscriber(AmazonDynamoDBClient dynamoClient, Subscriber subscriber)
        {
            var updateItemRequest = new UpdateItemRequest
            {
                TableName = _tableName,
                Key = new Dictionary<string, AttributeValue> { { "id", new AttributeValue { S = subscriber.id } } },
                ExpressionAttributeNames = new Dictionary<string, string>()
                {
                    { "#LA", "last_alerted" },
                    { "#AC", "alert_center_ids" }
                },
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>()
                {
                    { ":new_last_alerted", new AttributeValue { N = subscriber.last_alerted.ToString() } },
                    { ":alert_center_ids", new AttributeValue { S = subscriber.alert_center_ids } }
                },
                UpdateExpression = "SET #LA = :new_last_alerted, #AC = :alert_center_ids"
            };
            var response = dynamoClient.UpdateItemAsync(updateItemRequest).Result;
        }
    }
}
