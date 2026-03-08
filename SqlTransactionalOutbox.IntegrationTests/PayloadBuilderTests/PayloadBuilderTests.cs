using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SqlTransactionalOutbox.JsonExtensions;
using SqlTransactionalOutbox.Tests;
using SqlTransactionalOutbox.Utilities;
using SqlTransactionalOutbox.CustomExtensions;
using SystemTextJsonHelpers;

namespace SqlTransactionalOutbox.IntegrationTests
{
    [TestClass]
    public class PayloadBuilderTests
    {
        public TestContext TestContext { get; set; }

        [TestMethod]
        public void TestPayloadBuilderFromJsonWithPlainTextBody()
        {
            var scheduledPublishDateTime = DateTimeOffset.UtcNow.AddDays(10);
            //TODO: Add ALL Field options...
            var jsonText = $@"
                {{
                    ""topic"": ""{TestConfiguration.AzureServiceBusTopic}"",
                    ""fifoGroupingId"": ""HttpProxy-IntegrationTest"",
                    ""to"": ""CajunCoding"",
                    ""body"": ""Testing Json Payload from HttpProxy"",
                    ""scheduledPublishDateTimeUtc"": ""{scheduledPublishDateTime.ToIso8601RoundTripFormat()}""
                }}            
            ";

            var payloadBuilder = PayloadBuilder.FromJsonSafely(jsonText);

            Assert.AreEqual(TestConfiguration.AzureServiceBusTopic, payloadBuilder.PublishTarget);
            Assert.AreEqual("HttpProxy-IntegrationTest", payloadBuilder.FifoGroupingId);
            Assert.AreEqual("CajunCoding", payloadBuilder.To);
            Assert.AreEqual("Testing Json Payload from HttpProxy", payloadBuilder.Body);
            Assert.AreEqual(scheduledPublishDateTime, payloadBuilder.ScheduledPublishDateTimeUtc.Value);
        }

        [TestMethod]
        public void TestPayloadBuilderFromJsonToJObject()
        {
            var jsonText = $@"
                {{
                    ""publishTopic"": ""{TestConfiguration.AzureServiceBusTopic}"",
                    ""fifoGroupingId"": ""HttpProxy-IntegrationTest"",
                    ""to"": ""CajunCoding"",
                    ""body"": ""Testing Json Payload from HttpProxy"",
                    ""scheduledPublishDateTimeUtc"": ""2026-02-27T04:25:00Z""
                }}            
            ";

            var payloadBuilder = PayloadBuilder.FromJsonSafely(jsonText);
            var jsonPayload = payloadBuilder.ToJsonObject();

            Assert.AreEqual(TestConfiguration.AzureServiceBusTopic, jsonPayload.ValueSafely<string>(nameof(PayloadBuilder.PublishTarget)));
            Assert.AreEqual("HttpProxy-IntegrationTest", jsonPayload.ValueSafely<string>(nameof(PayloadBuilder.FifoGroupingId)));
            Assert.AreEqual("CajunCoding", jsonPayload.ValueSafely<string>(nameof(PayloadBuilder.To)));
            Assert.AreEqual("Testing Json Payload from HttpProxy", jsonPayload.ValueSafely<string>(nameof(PayloadBuilder.Body)));
            Assert.AreEqual(DateTimeOffset.Parse("2026-02-27T04:25:00Z"), jsonPayload.ValueSafely<DateTimeOffset>(nameof(PayloadBuilder.ScheduledPublishDateTimeUtc)));
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
                Body = "Testing Json Payload from HttpProxy",
                ScheduledPublishDateTimeUtc = DateTimeOffset.UtcNow.AddDays(10)
            };        

            var payloadBuilder = PayloadBuilder.FromObject(tempObject);

            Assert.AreEqual(TestConfiguration.AzureServiceBusTopic, payloadBuilder.PublishTarget);
            Assert.AreEqual("HttpProxy-IntegrationTest", payloadBuilder.FifoGroupingId);
            Assert.AreEqual("CajunCoding", payloadBuilder.To);
            Assert.AreEqual("Testing Json Payload from HttpProxy", payloadBuilder.Body);
            Assert.AreEqual(tempObject.ScheduledPublishDateTimeUtc, payloadBuilder.ScheduledPublishDateTimeUtc);
        }

        private record ComplexBody(string Message, int[] Ids, Guid[] Guids, bool IsThisABoolean, DateTime? ScheduledPublishDateTimeUtc);

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
                    Ids: [ 1, 2, 3 ],
                    Guids: [ Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() ],
                    IsThisABoolean: true,
                    ScheduledPublishDateTimeUtc: null
                )
            };

            var payloadBuilder = PayloadBuilder.FromObject(tempObject);

            Assert.AreEqual(TestConfiguration.AzureServiceBusTopic, payloadBuilder.PublishTarget);
            Assert.AreEqual("HttpProxy-IntegrationTest", payloadBuilder.FifoGroupingId);
            Assert.AreEqual("CajunCoding", payloadBuilder.To);
            Assert.IsNull(payloadBuilder.ScheduledPublishDateTimeUtc); // This value was not set, so it should be null/empty/default.

            //Compare and Validate the Complex Body values...
            var originalBody = tempObject.Body;
            var payloadBody = payloadBuilder.Body.FromJsonTo<ComplexBody>(PayloadBuilder.OutboxJsonSerializerOptions);
            Assert.AreEqual(originalBody.Message, payloadBody.Message);
            Assert.IsTrue(originalBody.Ids.SequenceEqual(payloadBody.Ids));
            Assert.IsTrue(originalBody.Guids.SequenceEqual(payloadBody.Guids));
            Assert.AreEqual(originalBody.IsThisABoolean, payloadBody.IsThisABoolean);
            Assert.IsNull(originalBody.ScheduledPublishDateTimeUtc);
            Assert.IsNull(payloadBody.ScheduledPublishDateTimeUtc);
        }


        [TestMethod]
        public void TestPayloadBuilderDirectlyToCreatingPayloads()
        {
            var complexBodyModel = new ComplexBody(
                Message: "This is a complex Body Payload...",
                Ids: [ 1, 2, 3 ],
                Guids: [ Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() ],
                IsThisABoolean: true,
                ScheduledPublishDateTimeUtc: DateTime.UtcNow.AddDays(10)
            );

            var payloadBuilder = new PayloadBuilder()
            {
                PublishTarget = TestConfiguration.AzureServiceBusTopic, // aka Topic for Azure Service Bus!
                FifoGroupingId = "HttpProxy-IntegrationTest",
                To = "CajunCoding",
                Body = complexBodyModel.ToJson(PayloadBuilder.OutboxJsonSerializerOptions)
            }
            .ApplyValues(complexBodyModel);

            Assert.AreEqual(TestConfiguration.AzureServiceBusTopic, payloadBuilder.PublishTarget);
            Assert.AreEqual("HttpProxy-IntegrationTest", payloadBuilder.FifoGroupingId);
            Assert.AreEqual("CajunCoding", payloadBuilder.To);
            Assert.AreEqual(complexBodyModel.ScheduledPublishDateTimeUtc, payloadBuilder.ScheduledPublishDateTimeUtc);

            //Compare and Validate the Complex Body values...
            var originalBody = complexBodyModel;
            var payloadBody = payloadBuilder.Body.FromJsonTo<ComplexBody>(PayloadBuilder.OutboxJsonSerializerOptions);
            Assert.AreEqual(originalBody.Message, payloadBody.Message);
            Assert.IsTrue(originalBody.Ids.SequenceEqual(payloadBody.Ids));
            Assert.IsTrue(originalBody.Guids.SequenceEqual(payloadBody.Guids));
            Assert.AreEqual(originalBody.IsThisABoolean, payloadBody.IsThisABoolean);
            Assert.AreEqual(complexBodyModel.ScheduledPublishDateTimeUtc, payloadBody.ScheduledPublishDateTimeUtc);
        }
    }
}
