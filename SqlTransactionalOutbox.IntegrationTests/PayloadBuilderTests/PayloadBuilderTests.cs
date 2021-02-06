using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SqlTransactionalOutbox.JsonExtensions;
using SqlTransactionalOutbox.Publishing;
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
            var jsonText = @"
                {
                    ""topic"": ""SqlTransactionalOutbox/Integration-Tests"",
                    ""FifoGroupingId"": ""HttpProxy-IntegrationTest"",
                    ""to"": ""CajunCoding"",
                    ""body"": ""Testing Json Payload from HttpProxy""
                }            
            ";

            var payloadBuilder = PayloadBuilder.FromJsonSafely(jsonText);

            Assert.AreEqual("SqlTransactionalOutbox/Integration-Tests", payloadBuilder.PublishTopic);
            Assert.AreEqual("HttpProxy-IntegrationTest", payloadBuilder.FifoGroupingId);
            Assert.AreEqual("CajunCoding", payloadBuilder.To);
            Assert.AreEqual("Testing Json Payload from HttpProxy", payloadBuilder.Body);
        }

        [TestMethod]
        public void TestPayloadBuilderFromJsonToJObject()
        {
            var jsonText = @"
                {
                    ""topic"": ""SqlTransactionalOutbox/Integration-Tests"",
                    ""FifoGroupingId"": ""HttpProxy-IntegrationTest"",
                    ""to"": ""CajunCoding"",
                    ""body"": ""Testing Json Payload from HttpProxy""
                }            
            ";

            var payloadBuilder = PayloadBuilder.FromJsonSafely(jsonText);
            var jsonPayload = payloadBuilder.ToJObject();

            Assert.AreEqual("SqlTransactionalOutbox/Integration-Tests", jsonPayload.ValueSafely<string>(JsonMessageFields.PublishTopic));
            Assert.AreEqual("HttpProxy-IntegrationTest", jsonPayload.ValueSafely<string>(JsonMessageFields.FifoGroupingId));
            Assert.AreEqual("CajunCoding", jsonPayload.ValueSafely<string>(JsonMessageFields.To));
            Assert.AreEqual("Testing Json Payload from HttpProxy", jsonPayload.ValueSafely<string>(JsonMessageFields.Body));
        }
    }
}
