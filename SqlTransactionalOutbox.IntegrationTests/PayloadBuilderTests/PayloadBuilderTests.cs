using System;
using System.Linq;
using Azure.Identity;
using Microsoft.IdentityModel.Tokens;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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

        private record ComplexBody(string Message, int[] Ids, Guid[] Guids, bool IsThisABoolean);

        [TestMethod]
        public void TestPayloadBuilderFromObjectWithComplexBody()
        {
            var tempObject = new
            {
                Topic = TestConfiguration.AzureServiceBusTopic,
                FifoGroupingId = "HttpProxy-IntegrationTest",
                To = "CajunCoding",
                Body = new ComplexBody(
                    Message: "This is a complex Body Payload...",
                    Ids: new[] { 1, 2, 3 },
                    Guids: new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() },
                    IsThisABoolean: true
                )
            };


            var payloadBuilder = PayloadBuilder.FromObject(tempObject);

            Assert.AreEqual(TestConfiguration.AzureServiceBusTopic, payloadBuilder.PublishTarget);
            Assert.AreEqual("HttpProxy-IntegrationTest", payloadBuilder.FifoGroupingId);
            Assert.AreEqual("CajunCoding", payloadBuilder.To);

            //Compare and Validate the Complex Body values...
            var originalBody = tempObject.Body;
            var payloadBody = JsonConvert.DeserializeObject<ComplexBody>(payloadBuilder.Body);
            Assert.AreEqual(originalBody.Message, payloadBody.Message);
            Assert.IsTrue(originalBody.Ids.SequenceEqual(payloadBody.Ids));
            Assert.IsTrue(originalBody.Guids.SequenceEqual(payloadBody.Guids));
            Assert.AreEqual(originalBody.IsThisABoolean, payloadBody.IsThisABoolean);
        }
    }
}
