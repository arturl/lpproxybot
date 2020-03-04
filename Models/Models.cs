using System;
using Microsoft.Bot.Builder;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Configuration;
using System.Linq;
using LPProxyBot.Bots;
using System.Net.Http;
using Newtonsoft.Json;

namespace LPProxyBot
{
    public class DomainInfo
    {
        public string service { get; set; }
        public string account { get; set; }
        public string baseURI { get; set; }
    }

    public class AppJWT
    {
        public string access_token { get; set; }
        public string token_type { get; set; }
    }

    public class ConsumerId
    {
        public string ext_consumer_id { get; set; }
    }


    public class ConsumerJWS
    {
        public string token { get; set; }
    }

    // Start conversation
    public class Conversation
    {
        public string kind { get; set; }
        public string id { get; set; }
        public string type { get; set; }
        public Body body { get; set; }
    }

    public class Body
    {
        public Authenticateddata authenticatedData { get; set; }
        public string brandId { get; set; }
    }

    public class Authenticateddata
    {
        public Lp_Sdes[] lp_sdes { get; set; }
    }

    public class Lp_Sdes
    {
        public string type { get; set; }
        public Info info { get; set; }
        public Personal personal { get; set; }
    }

    public class Info
    {
        public string socialId { get; set; }
        public string ctype { get; set; }
    }

    public class Personal
    {
        public string firstname { get; set; }
        public string lastname { get; set; }
        public string gender { get; set; }
    }

    public class ConversationResponse
    {
        public string code { get; set; }
        public ConversationResponseBody body { get; set; }
        public string reqId { get; set; }
    }

    public class ConversationResponseBody
    {
        public string msg { get; set; }
        public string conversationId { get; set; }
    }

    // Send conversation

    public class Message
    {
        public string kind { get; set; }
        public string id { get; set; }
        public string type { get; set; }
        public MessageBody body { get; set; }
    }

    public class MessageBody
    {
        public string dialogId { get; set; }
        public MessageBodyEvent @event { get; set; }
    }

    public class MessageBodyEvent
    {
        public string type { get; set; }
        public string contentType { get; set; }
        public string message { get; set; }
    }

    public class SendResponse
    {
        public string reqId { get; set; }
        public string code { get; set; }
        public SendBody body { get; set; }
    }

    public class SendBody
    {
        public int sequence { get; set; }
    }

    // Webhook models:

    namespace Webhook
    {
        public class WebhookData
        {
            public string kind { get; set; }
            public Body body { get; set; }
            public string type { get; set; }
        }

        public class Body
        {
            public Change[] changes { get; set; }
        }

        public class Change
        {
            public int sequence { get; set; }
            public string originatorId { get; set; }
            public Originatormetadata originatorMetadata { get; set; }
            public long serverTimestamp { get; set; }
            public Event @event { get; set; }
            public string conversationId { get; set; }
            public string dialogId { get; set; }
        }

        public class Originatormetadata
        {
            public string id { get; set; }
            public string role { get; set; }
        }

        public class Event
        {
            public string type { get; set; }
            public string chatState { get; set; }
            public string message { get; set; }
            public string contentType { get; set; }
        }
    }
}
