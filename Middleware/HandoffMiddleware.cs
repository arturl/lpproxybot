using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LPProxyBot.Bots;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;

namespace LPProxyBot
{
    public class ConversationRecord
    {
        public ConversationReference ConversationReference;
        public bool IsAcked = false;
        public bool IsClosed = false;
    }

    public class ConversationMap
    {
        public ConcurrentDictionary<string, ConversationRecord> ConversationRecords = new ConcurrentDictionary<string, ConversationRecord>();
    }

    public class HandoffMiddleware : IMiddleware
    {
        IConfiguration _configuration;
        private BotState _conversationState;
        private ConversationMap _conversationMap;

        public HandoffMiddleware(IConfiguration configuration, ConversationState conversationState, ConversationMap conversationMap)
        {
            _configuration = configuration;
            _conversationState = conversationState;
            _conversationMap = conversationMap;
        }

        public async Task OnTurnAsync(ITurnContext turnContext, NextDelegate next, CancellationToken cancellationToken = default)
        {
            // Route the conversation based on whether it's been escalated
            var conversationStateAccessors = _conversationState.CreateProperty<ConversationData>(nameof(ConversationData));
            var conversationData = await conversationStateAccessors.GetAsync(turnContext, () => new ConversationData());

            if (turnContext.Activity.Type == ActivityTypes.Message && conversationData.EscalationRecord != null)
            {
                var account = _configuration.GetValue<string>("LivePersonAccount");
                var message = LivePersonConnector.MakeLivePersonMessage(0 /*?*/, conversationData.EscalationRecord.ConversationId, turnContext.Activity.Text);

                await LivePersonConnector.SendMessageToConversation(account,
                    conversationData.EscalationRecord.MsgDomain,
                    conversationData.EscalationRecord.AppJWT,
                    conversationData.EscalationRecord.ConsumerJWS,
                    conversationData.EscalationRecord.ConversationId,
                    message);
                return;
            }

            if (turnContext.Activity.Type == ActivityTypes.Event && turnContext.Activity.Name == HandoffEventNames.HandoffStatus)
            {
                try
                {
                    var state = (turnContext.Activity.Value as JObject)?.Value<string>("state");
                    if (state == "completed")
                    {
                        conversationData.EscalationRecord = null;
                        await _conversationState.SaveChangesAsync(turnContext);
                    }
                }
                catch { }
            }

            turnContext.OnSendActivities(async (sendTurnContext, activities, nextSend) =>
            {
                // Handle any escalation events, and let them propagate through the pipeline
                // This is useful for debugging with the Emulator
                var handoffEvents = activities.Where(activity =>
                    activity.Type == ActivityTypes.Event && activity.Name == HandoffEventNames.InitiateHandoff);

                if (handoffEvents.Count() == 1)
                {
                    var handoffEvent = handoffEvents.First();
                    conversationData.EscalationRecord = await Escalate(sendTurnContext, handoffEvent);
                    await _conversationState.SaveChangesAsync(turnContext);
                }

                // run full pipeline
                var responses = await nextSend().ConfigureAwait(false);
                return responses;
            });

            await next(cancellationToken);
        }

        private Task<LivePersonConversationRecord> Escalate(ITurnContext turnContext, IEventActivity handoffEvent)
        {
            var account = _configuration.GetValue<string>("LivePersonAccount");
            var clientId = _configuration.GetValue<string>("LivePersonClientId");
            var clientSecret = _configuration.GetValue<string>("LivePersonClientSecret");

            return LivePersonConnector.EscalateToAgent(turnContext, handoffEvent, account, clientId, clientSecret, _conversationMap);
        }
    }
}
