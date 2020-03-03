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
using System.Text;

namespace LPProxyBot
{
    public class HandoffMiddleware : Microsoft.Bot.Builder.IMiddleware
    {
        IConfiguration _configuration;
        private BotState _conversationState;

        public HandoffMiddleware(IConfiguration configuration, ConversationState conversationState)
        {
            _configuration = configuration;
            _conversationState = conversationState;
        }

        public async Task OnTurnAsync(ITurnContext turnContext, NextDelegate next, CancellationToken cancellationToken = default)
        {
            // Route the conversation based on whether it's been escalated
            var conversationStateAccessors = _conversationState.CreateProperty<ConversationData>(nameof(ConversationData));
            var conversationData = await conversationStateAccessors.GetAsync(turnContext, () => new ConversationData());

            if (conversationData.IsEscalated)
            {
                // TBD: check if the agent has ended the conversation. If so, reset conversationData.IsEscalated

                // Don't send this message to the bot. Route it to the agent and get a response
                // TBD: this will come from LivePerson
                await turnContext.SendActivityAsync($"You're talking with an agent. You said '{turnContext.Activity.Text}'. The agent said 'Hi!'.");
                return;
            }

            turnContext.OnSendActivities(async (sendTurnContext, activities, nextSend) =>
            {
                // Handle any escalation events, and let them propagate through the pipeline
                // This is useful for debugging with the Emulator
                var handoffEvents = activities.Where(activity =>
                    activity.Type == ActivityTypes.Event && activity.Name == HandoffEventNames.InitiateHandoff);

                if (handoffEvents.Any())
                {
                    conversationData.IsEscalated = true;
                    await _conversationState.SaveChangesAsync(turnContext);
                    foreach (var handoffEvent in handoffEvents)
                    {
                        await Escalate(sendTurnContext, handoffEvent);
                    }
                }

                // run full pipeline
                var responses = await nextSend().ConfigureAwait(false);
                return responses;
            });

            await next(cancellationToken);
        }

        private async Task Escalate(ITurnContext turnContext, IEventActivity handoffEvent)
        {
            var account = _configuration.GetValue<string>("LivePersonAccount");
            var clientId = _configuration.GetValue<string>("LivePersonClientId");
            var clientSecret = _configuration.GetValue<string>("LivePersonClientSecret");

            var sentinelDomain = await GetDomain(account, "sentinel");
            var appJWT = await GetAppJWT(account, sentinelDomain, clientId, clientSecret);
            ConsumerId consumer = new ConsumerId { ext_consumer_id = turnContext.Activity.From.Id };

            var userName = turnContext.Activity.From.Name;

            var idpDomain = await GetDomain(account, "idp");
            var consumerJWS = await GetConsumerJWS(account, idpDomain, appJWT, consumer);

            var msgDomain = await GetDomain(account, "asyncMessagingEnt");
            var conversations = new Conversation[] {
                    new Conversation {
                        kind = "req",
                        id = "1,",
                        type = "userprofile.SetUserProfile",
                        body = new Body { authenticatedData = new Authenticateddata {
                            lp_sdes = new Lp_Sdes[] {
                                new Lp_Sdes {
                                    type = "ctmrinfo",
                                    info = new Info { socialId = "1234567890", ctype = "vip" }
                                },
                                new Lp_Sdes {
                                    type = "personal",
                                    //personal = new Personal { firstname = "Alice", lastname = "Doe", gender = "FEMALE" }
                                    personal = new Personal { firstname = userName + (new Random()).Next(0,100).ToString()}
                                }
                            } }
                        } },
                    new Conversation {
                        kind = "req",
                        id = "2,",
                        type = "cm.ConsumerRequestConversation",
                        body = new Body { brandId = account }
                    },
            };

            var conversationId = await StartConversation(account, msgDomain, appJWT, consumerJWS, conversations);

            Message message = new Message {
                kind = "req",
                id = "1",
                type = "ms.PublishEvent",
                body = new MessageBody {
                    dialogId = conversationId,
                    @event = new MessageBodyEvent {
                        type = "ContentEvent",
                        contentType = "text/plain",
                        message = "From user: " + turnContext.Activity.Text
                    }
                }
            };

            await SendMessageToConversation(account, msgDomain, appJWT, consumerJWS, conversationId, message);
        }

        private async Task<string> GetDomain(string account, string serviceName)
        {
            using (var client = new HttpClient())
            {
                var result = await client.GetAsync($"http://api.liveperson.net/api/account/{account}/service/{serviceName}/baseURI.json?version=1.0");
                if (result.IsSuccessStatusCode)
                {
                    var strResult = await result.Content.ReadAsStringAsync();
                    var domain = JsonConvert.DeserializeObject<DomainInfo>(strResult);
                    return domain.baseURI;
                }
                else
                {
                    throw new Exception($"Failed to get Domain for service {serviceName}. Error {result.StatusCode}");
                }
            }
        }

        private async Task<string> GetAppJWT(string account, string domain, string clientId, string clientSecret)
        {
            using (var client = new HttpClient())
            {
                var stringPayload = "";
                var httpContent = new StringContent(stringPayload, Encoding.UTF8, "application/x-www-form-urlencoded");

                var request = new HttpRequestMessage();
                request.Method = HttpMethod.Post;
                request.Content = httpContent;
                request.RequestUri = new Uri($"https://{domain}/sentinel/api/account/{account}/app/token?v=1.0&grant_type=client_credentials&client_id={clientId}&client_secret={clientSecret}");

                var response = await client.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    var strResult = await response.Content.ReadAsStringAsync();
                    var appJWT = JsonConvert.DeserializeObject<AppJWT>(strResult);
                    return appJWT.access_token;
                }
                else
                {
                    throw new Exception($"Failed to obtain AppJWT");
                }
            }
        }

        private async Task<string> GetConsumerJWS(string account, string domain, string authToken, ConsumerId consumer)
        {
            using (var client = new HttpClient())
            {
                var stringPayload = JsonConvert.SerializeObject(consumer);
                var httpContent = new StringContent(stringPayload, Encoding.UTF8, "application/json");

                var request = new HttpRequestMessage();
                request.Method = HttpMethod.Post;
                request.Content = httpContent;
                request.Headers.Add("Authorization", authToken);
                request.RequestUri = new Uri($"https://{domain}/api/account/{account}/consumer?v=1.0");

                var response = await client.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    var strResult = await response.Content.ReadAsStringAsync();
                    var consumerJWS = JsonConvert.DeserializeObject<ConsumerJWS>(strResult);
                    return consumerJWS.token;
                }
                else
                {
                    throw new Exception($"Failed to obtain ConsumerJWS");
                }
            }
        }

        private async Task<string> StartConversation(string account, string domain, string appJWT, string consumerJWS, Conversation[] conversations)
        {
            using (var client = new HttpClient())
            {
                var stringPayload = JsonConvert.SerializeObject(conversations, Formatting.Indented, new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore
                });
                var httpContent = new StringContent(stringPayload, Encoding.UTF8, "application/json");

                var request = new HttpRequestMessage();
                request.Method = HttpMethod.Post;
                request.Content = httpContent;
                request.Headers.Add("Authorization", appJWT);
                request.Headers.Add("X-LP-ON-BEHALF", consumerJWS);
                request.RequestUri = new Uri($"https://{domain}/api/account/{account}/messaging/consumer/conversation?v=3");

                var response = await client.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    var strResult = await response.Content.ReadAsStringAsync();
                    var conversationResponses = JsonConvert.DeserializeObject<ConversationResponse[]>(strResult);
                    foreach(var convo in conversationResponses)
                    {
                        var convId = convo.body?.conversationId;
                        if (convId != null)
                        {
                            return convId;
                        }
                    }

                    throw new Exception($"Failed to StartConversation - cannot get conversation id");
                }
                else
                {
                    throw new Exception($"Failed to StartConversation");
                }
            }
        }

        private async Task<int> SendMessageToConversation(string account, string domain, string appJWT, string consumerJWS, string conversationId, Message message)
        {
            using (var client = new HttpClient())
            {
                var stringPayload = JsonConvert.SerializeObject(message, Formatting.Indented, new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore
                });
                var httpContent = new StringContent(stringPayload, Encoding.UTF8, "application/json");

                var request = new HttpRequestMessage();
                request.Method = HttpMethod.Post;
                request.Content = httpContent;
                request.Headers.Add("Authorization", appJWT);
                request.Headers.Add("X-LP-ON-BEHALF", consumerJWS);
                request.RequestUri = new Uri($"https://{domain}/api/account/{account}/messaging/consumer/conversation/send?v=3");

                var response = await client.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    var strResult = await response.Content.ReadAsStringAsync();
                    var sendResponse = JsonConvert.DeserializeObject<SendResponse>(strResult);
                    return sendResponse.body.sequence;
                }
                else
                {
                    throw new Exception($"Failed to StartConversation");
                }
            }
        }

    }
}
