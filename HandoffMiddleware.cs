using System;
using Microsoft.Bot.Builder;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Configuration;
using System.Linq;
using LPProxyBot.Bots;

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

            if(conversationData.IsEscalated)
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

                if(handoffEvents.Any())
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
            // TBD: establish connection with LivePerson. Get AppJWT, ConsumerJWS and start the conversation
        }
    }
}
