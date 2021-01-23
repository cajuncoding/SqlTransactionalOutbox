//using System;
//using System.Collections.Generic;
//using System.Text;
//using System.Threading.Tasks;
//using Microsoft.Azure.ServiceBus;
//using Newtonsoft.Json;

//namespace SqlTransactionalOutbox.AzureServiceBus
//{
//    public class AzureServiceBusReceiver<TUniqueIdentifier, TPayload>
//    {
//        public string ConnectionString { get; }
//        public string TopicPath { get; }
//        public string SubscriptionName { get; }

//        protected SubscriptionClient TopicSubscriptionClient { get; }

//        public AzureServiceBusReceiver(string azureServiceBusConnectionString)
//        {
//            ConnectionString = azureServiceBusConnectionString;
//            TopicPath = topicPath;
//            SubscriptionName = subscriptionName;

//            TopicSubscriptionClient = new SubscriptionClient(ConnectionString, TopicPath, subscriptionName);
//        }

//        public Task RegisterReceiverAsync(string topicPath, string subscriptionName, Action<ISqlTransactionalOutboxItem<Guid> handlerAction)
//        {
//            try
//            {
//                TopicSubscriptionClient.RegisterMessageHandler(
//                    async (message, token) =>
//                    {
//                        var jsonPayload = Encoding.UTF8.GetString(message.Body);

//                        await TopicSubscriptionClient.CompleteAsync(message.SystemProperties.LockToken);
//                    },
//                    new MessageHandlerOptions(async args => Console.WriteLine(args.Exception))
//                    {
//                        MaxConcurrentCalls = 1,
//                        AutoComplete = false
//                    }
//                );
//            }
//            catch (Exception e)
//            {
//                Console.WriteLine("Exception: " + e.Message);
//            }
//        }
//    }
