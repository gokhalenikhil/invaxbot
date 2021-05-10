using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Amazon;
using Amazon.DynamoDBv2;
using Amazon.Lambda.Core;
using Amazon.Runtime;
using Amazon.Runtime.CredentialManagement;
using Amazon.S3;
using Amazon.S3.Model;
using Invaxbot.NotificationService.NotificationLambda.Models;
using Newtonsoft.Json;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace Invaxbot.NotificationService.NotificationLambda
{
    public class NotificationLambdaFunction
    {
        private string _bucketName;
        private string _key;
        private string _token;
        private int _noOfHours = 4;
        private IAmazonS3 _client;
        private AmazonDynamoDBClient _dynamoClient;
        AWSCredentials _awsDynamoCredentials;
        AWSCredentials _awsS3Credentials;

        /// <summary>
        /// A simple function that takes a string and does a ToUpper
        /// </summary>
        /// <param name="input"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public void NotificationFunctionHandler(NotificationLambdaInput input, ILambdaContext context)
        {
            if (context == null)
            {
                var chain = new CredentialProfileStoreChain();
                if (chain.TryGetAWSCredentials("dynamodbadmin", out _awsDynamoCredentials) && chain.TryGetAWSCredentials("s3admin", out _awsS3Credentials))
                {
                    using (_dynamoClient = new AmazonDynamoDBClient(_awsDynamoCredentials, RegionEndpoint.APSouth1))
                    using (_client = new AmazonS3Client(_awsS3Credentials, RegionEndpoint.APSouth1))
                    {
                        ReadJsonAndSendMessageToSubscribers(input, context);
                    }
                }
            }
            else
            {
                _token = Environment.GetEnvironmentVariable("BOT_TOKEN");
                _noOfHours = int.Parse(Environment.GetEnvironmentVariable("NO_OF_HOURS"));
                using (_dynamoClient = new AmazonDynamoDBClient(RegionEndpoint.APSouth1))
                using (_client = new AmazonS3Client(RegionEndpoint.APSouth1))
                {
                    ReadJsonAndSendMessageToSubscribers(input, context);
                }
            }
        }

        private void ReadJsonAndSendMessageToSubscribers(NotificationLambdaInput input, ILambdaContext context)
        {
            _bucketName = input.bucket;
            _key = input.s3url;
            var subscriberList = SubscriberDalHelper.GetSubscribers(_dynamoClient, input.short_pin);
            var centers = ReadJsonObjectFromS3Async().Result.ToList();
            if (centers != null)
            {
                foreach (var subscriber in subscriberList)
                {
                    if (!subscriber.active)
                    {
                        continue;
                    }
                    var timeSpan = (DateTime.UtcNow - new DateTime(subscriber.last_alerted));
                    var centerIds = subscriber.alert_center_ids.Split(",");

                    if (timeSpan.TotalHours >= _noOfHours || !centers.All(center => centerIds.Contains(center.center_id.ToString())))
                    {
                        var filtered = centers.Where(center => center.weeklyCapacity > 0);
                        var centers18 = subscriber.min_age_18 ? filtered.Where(center => center.eighteenplusAvailability) : null;
                        var centers45 = subscriber.min_age_45 ? filtered.Where(center => center.fortyfiveplusAvailability) : null;
                        if ((centers18 != null && centers18.Any()) || (centers45 != null && centers45.Any()))
                        {
                            if(SendMessagesToSubscriber(centers18, centers45, subscriber.id, centerIds))
                            {
                                subscriber.last_alerted = DateTime.UtcNow.Ticks;
                            }
                            var updatedCenterIds = new HashSet<String>(centerIds);

                            if (centers18 != null)
                            {
                                updatedCenterIds.UnionWith(centers18.Select((center => center.center_id.ToString())).ToArray());
                            }
                            if (centers45 != null)
                            {
                                updatedCenterIds.UnionWith(centers45.Select((center => center.center_id.ToString())).ToArray());

                            }
                            subscriber.alert_center_ids = string.Join(",", updatedCenterIds);

                            SubscriberDalHelper.UpdateSubscriber(_dynamoClient ,subscriber);
                        }
                    }                    
                } 
            }
        }

        private bool SendMessagesToSubscriber(IEnumerable<CuratedCenter> centers18, IEnumerable<CuratedCenter> centers45, string id, string[] centerIds)
        {
            string uri;
            if(string.IsNullOrEmpty(_token))
            {
                uri = $"https://api.telegram.org/botblah";
            }
            else
            {
                uri = $"https://api.telegram.org/bot{_token}";
            }
            using var httpClient = new HttpClient();
            bool msgSent = false;
            if (centers18 != null && centers18.Any())
            {
                var messages = CreateMessageLists(centers18, centerIds);
                if (messages.Count() > 0)
                {
                    var content = httpClient.GetAsync($"{uri}/sendMessage?chat_id={id}&text=List of all centers with above 18 availability\nCenterName, Pincode").Result;
                    Console.WriteLine(content);
                }

                foreach (var message in messages)
                {
                    var content = httpClient.GetAsync($"{uri}/sendMessage?chat_id={id}&text={message}").Result;
                    Console.WriteLine(content);
                    msgSent = true;
                }
            }
            if (centers45 != null && centers45.Any())
            {
                var messages = CreateMessageLists(centers45, centerIds);
                if(messages.Count() > 0)
                {
                    var content = httpClient.GetAsync($"{uri}/sendMessage?chat_id={id}&text=List of all centers with above 45 availability\nCenterName, Pincode").Result;
                    Console.WriteLine(content);
                }
                foreach (var message in messages)
                {
                    var content = httpClient.GetAsync($"{uri}/sendMessage?chat_id={id}&text={message}").Result;
                    Console.WriteLine(content);
                    msgSent = true;
                }
            }
            return msgSent;
        }

        private IEnumerable<string> CreateMessageLists(IEnumerable<CuratedCenter> centers, string[] centerIds)
        {
            int count = 0;
            StringBuilder message = new StringBuilder(string.Empty);
            var messages = new List<string>();
            foreach (var center in centers)
            {
                if (centerIds.Contains(center.center_id.ToString())){
                    continue;
                }
                if (count <= 10)
                {
                    message.Append(center.ToString());
                    count++;
                }
                else
                {
                    messages.Add(message.ToString());
                    message = new StringBuilder(string.Empty);
                    count = 0;
                }
            }
            if (!string.IsNullOrEmpty(message.ToString()))
            {
                messages.Add(message.ToString());
            }
            return messages;
        }

        private async Task<IEnumerable<CuratedCenter>> ReadJsonObjectFromS3Async()
        {
            string responseBody;
            var centerList = new List<CuratedCenter>();
            try
            {
                GetObjectRequest request = new GetObjectRequest
                {
                    BucketName = _bucketName,
                    Key = _key
                };

                using (var response = await _client.GetObjectAsync(request))
                using (var responseStream = response.ResponseStream)
                using (var reader = new StreamReader(responseStream))
                {
                    responseBody = reader.ReadToEnd();
                    if(!string.IsNullOrEmpty(responseBody))
                    {
                        centerList = JsonConvert.DeserializeObject<List<CuratedCenter>>(responseBody);
                    }
                }
                return centerList   ;
            }
            catch (AmazonS3Exception e)
            {
                // If bucket or object does not exist
                Console.WriteLine("Error encountered ***. Message:'{0}' when reading object", e.Message);
            }
            catch (Exception e)
            {
                Console.WriteLine("Unknown encountered on server. Message:'{0}' when reading object", e.Message);
            }
            return null;
        }
    }
}
