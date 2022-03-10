using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SqlTransactionalOutbox.JsonExtensions;
using SqlTransactionalOutbox.Tests;
using SqlTransactionalOutbox.Utilities;

namespace SqlTransactionalOutbox.IntegrationTests
{
    [TestClass]
    public class PayloadBuilderTests
    {
        public TestContext TestContext { get; set; }

        [TestMethod]
        public void TestPayloadBuilderFromJsonWithPlainTextBody()
        {
            //TODO: Add ALL Field options...
            var jsonText = $@"
                {{
                    ""topic"": ""{TestConfiguration.AzureServiceBusTopic}"",
                    ""fifoGroupingId"": ""HttpProxy-IntegrationTest"",
                    ""to"": ""CajunCoding"",
                    ""body"": ""Testing Json Payload from HttpProxy""
                }}            
            ";

            var payloadBuilder = PayloadBuilder.FromJsonSafely(jsonText);

            Assert.AreEqual(TestConfiguration.AzureServiceBusTopic, payloadBuilder.PublishTarget);
            Assert.AreEqual("HttpProxy-IntegrationTest", payloadBuilder.FifoGroupingId);
            Assert.AreEqual("CajunCoding", payloadBuilder.To);
            Assert.AreEqual("Testing Json Payload from HttpProxy", payloadBuilder.Body);
        }

        [TestMethod]
        public void TestPayloadBuilderFromJsonToJObject()
        {
            var jsonText = $@"
                {{
                    ""publishTopic"": ""{TestConfiguration.AzureServiceBusTopic}"",
                    ""fifoGroupingId"": ""HttpProxy-IntegrationTest"",
                    ""to"": ""CajunCoding"",
                    ""body"": ""Testing Json Payload from HttpProxy""
                }}            
            ";

            var payloadBuilder = PayloadBuilder.FromJsonSafely(jsonText);
            var jsonPayload = payloadBuilder.ToJObject();

            Assert.AreEqual(TestConfiguration.AzureServiceBusTopic, jsonPayload.ValueSafely<string>(nameof(PayloadBuilder.PublishTarget)));
            Assert.AreEqual("HttpProxy-IntegrationTest", jsonPayload.ValueSafely<string>(nameof(PayloadBuilder.FifoGroupingId)));
            Assert.AreEqual("CajunCoding", jsonPayload.ValueSafely<string>(nameof(PayloadBuilder.To)));
            Assert.AreEqual("Testing Json Payload from HttpProxy", jsonPayload.ValueSafely<string>(nameof(PayloadBuilder.Body)));
        }

        [TestMethod]
        public void TestPayloadBuilderFromObject()
        {
            //TODO: Add ALL Field options...
            var tempObject = new
            {
                Topic = TestConfiguration.AzureServiceBusTopic,
                FifoGroupingId = "HttpProxy-IntegrationTest",
                To = "CajunCoding",
                Body = "Testing Json Payload from HttpProxy"
            };        

            var payloadBuilder = PayloadBuilder.FromObject(tempObject);

            Assert.AreEqual(TestConfiguration.AzureServiceBusTopic, payloadBuilder.PublishTarget);
            Assert.AreEqual("HttpProxy-IntegrationTest", payloadBuilder.FifoGroupingId);
            Assert.AreEqual("CajunCoding", payloadBuilder.To);
            Assert.AreEqual("Testing Json Payload from HttpProxy", payloadBuilder.Body);
        }
    }
}
