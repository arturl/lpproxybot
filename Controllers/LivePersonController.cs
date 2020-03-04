using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Bot.Schema;
using Newtonsoft.Json;

namespace LPProxyBot.Controllers
{
    [Route("api/LivePerson")]
    [ApiController]
    public class LivePersonController : ControllerBase
    {
        private readonly IBotFrameworkHttpAdapter Adapter;
        private readonly IBot Bot;

        public LivePersonController(IBotFrameworkHttpAdapter adapter, IBot bot)
        {
            Adapter = adapter;
            Bot = bot;
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
                catch
                {
                    int n = 0;
                }
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
                catch
                {
                    int n = 0;
                }
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
                try
                {
                    var wbhookData = JsonConvert.DeserializeObject<Webhook.WebhookData>(body);
                    if(wbhookData.body.changes[0].@event.type == "ContentEvent")
                    {
                        if(wbhookData.body.changes[0].@event.message != null)
                        {
                            // TODO: read from settings
                            string botAppId = "438450f9-cab5-4801-9b3b-fa08910acba8";
                            var humanActivity = MessageFactory.Text(wbhookData.body.changes[0].@event.message);

                            var adapter = Adapter as Microsoft.Bot.Builder.EchoBot.AdapterWithErrorHandler;
                            // This is the message we can send to the 
                            await adapter.ContinueConversationAsync(
                              botAppId,
                              g_conversationRef,
                              (ITurnContext turnContext, CancellationToken cancellationToken) =>
                              SendIt(turnContext, humanActivity),
                              default(CancellationToken));
                        }
                    }
                }
                catch
                {
                    int n = 0;
                }
            }
            Response.StatusCode = 200;
        }

        // HACK!!!!
        static public ConversationReference g_conversationRef;

        private static Task<ResourceResponse> SendIt(ITurnContext turnContext, Activity newAct)
        {
            return turnContext.SendActivityAsync(newAct);
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
                catch
                {
                    int n = 0;
                }
            }
            Response.StatusCode = 200;
        }

    }
}