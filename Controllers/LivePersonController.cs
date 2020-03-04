﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Bot.Connector.Authentication;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;

namespace LPProxyBot.Controllers
{
    [Route("api/LivePerson")]
    [ApiController]
    public class LivePersonController : ControllerBase
    {
        private readonly IBotFrameworkHttpAdapter _adapter;
        IConfiguration _configuration;
        private readonly IBot _bot;
        private ConcurrentDictionary<string, ConversationReference> _conversationReferences;
        private readonly string _appId;

        public LivePersonController(IBotFrameworkHttpAdapter adapter, IConfiguration configuration, IBot bot, ConcurrentDictionary<string, ConversationReference> conversationReferences)
        {
            _adapter = adapter;
            _configuration = configuration;
            _bot = bot;
            _conversationReferences = conversationReferences;
            _appId = configuration["MicrosoftAppId"];

            // If the channel is the Emulator, and authentication is not in use,
            // the AppId will be null.  We generate a random AppId for this case only.
            // This is not required for production, since the AppId will have a value.
            if (string.IsNullOrEmpty(_appId))
            {
                _appId = Guid.NewGuid().ToString(); //if no AppId, use a random Guid
            }
        }

        [HttpPost]
        [Route("AcceptStatusEvent")]
        public async Task PostAcceptStatusEventAsync()
        {
            using (StreamReader readStream = new StreamReader(Request.Body, Encoding.UTF8))
            {
                var body = await readStream.ReadToEndAsync();
                try
                {
                    var wbhookData = JsonConvert.DeserializeObject<Webhook.WebhookData>(body);
                }
                catch { }
            }
            Response.StatusCode = 200;
        }

        [HttpPost]
        [Route("chatstateevent")]
        public async Task PostChatStateEventAsync()
        {
            using (StreamReader readStream = new StreamReader(Request.Body, Encoding.UTF8))
            {
                var body = await readStream.ReadToEndAsync();
                try
                {
                    var wbhookData = JsonConvert.DeserializeObject<Webhook.WebhookData>(body);
                }
                catch { }
            }
            Response.StatusCode = 200;
        }

        [HttpPost]
        [Route("contentevent")]
        public async Task PostContentEventAsync()
        {
            using (StreamReader readStream = new StreamReader(Request.Body, Encoding.UTF8))
            {
                var body = await readStream.ReadToEndAsync();

                var wbhookData = JsonConvert.DeserializeObject<Webhook.WebhookData>(body);

                foreach (var change in wbhookData.body.changes)
                {
                    if (change?.@event.type == "ContentEvent" && change?.originatorMetadata?.role == "ASSIGNED_AGENT")
                    {
                        if (change.@event.message != null)
                        {
                            var humanActivity = MessageFactory.Text(change.@event.message);

                            ConversationReference conversationRef;
                            if (_conversationReferences.TryGetValue(change.conversationId, out conversationRef))
                            {
                                MicrosoftAppCredentials.TrustServiceUrl(conversationRef.ServiceUrl);

                                await (_adapter as BotAdapter).ContinueConversationAsync(
                                    _appId,
                                    conversationRef,
                                    (ITurnContext turnContext, CancellationToken cancellationToken) =>
                                        turnContext.SendActivityAsync(humanActivity, cancellationToken),
                                    default(CancellationToken));
                            }
                            else
                            {
                                // The bot has no record of this conversation, this should not happen
                                throw new Exception("Cannot find conversation");
                            }
                        }
                    }
                }
            }
            Response.StatusCode = 200;
        }

        [HttpPost]
        [Route("richcontentevent")]
        public async Task PostRichContentEventAsync()
        {
            using (StreamReader readStream = new StreamReader(Request.Body, Encoding.UTF8))
            {
                var body = await readStream.ReadToEndAsync();
                try
                {
                    var wbhookData = JsonConvert.DeserializeObject<Webhook.WebhookData>(body);
                }
                catch { }
            }
            Response.StatusCode = 200;
        }

    }
}