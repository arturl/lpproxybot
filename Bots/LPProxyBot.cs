// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Connector;
using Microsoft.Bot.Schema;

namespace LPProxyBot.Bots
{
    public class LPProxyBot : ActivityHandler
    {
        private BotState _conversationState;

        public LPProxyBot(ConversationState conversationState)
        {
            _conversationState = conversationState;
        }

        protected override async Task OnMessageActivityAsync(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken)
        {
            var conversationStateAccessors = _conversationState.CreateProperty<ConversationData>(nameof(ConversationData));
            var conversationData = await conversationStateAccessors.GetAsync(turnContext, () => new ConversationData());
            if (conversationData.IsEscalated)
            {
                // In the current implementation, a conversation is considered escalated as soon as the user
                // asks for an escalation. Other strategy would make it escalated after an agent has responded
                // to the request. Until then, the conversation must be handled by the bot.
                throw new InvalidOperationException("Bug: this conversation is in escalated state. The message must be routed to the agent");
            }

            var userText = turnContext.Activity.Text;
            if (userText=="agent")
            {
                await turnContext.SendActivityAsync("Your request will be escalated to a human agent");
                var evnt = EventFactory.CreateHandoffInitiation(turnContext, new { Skill = "Any" } );
                await turnContext.SendActivityAsync(evnt);
                return;
            }

            var replyText = $"Echo: {userText}";
            await turnContext.SendActivityAsync(MessageFactory.Text(replyText, replyText), cancellationToken);
        }

        protected override async Task OnMembersAddedAsync(IList<ChannelAccount> membersAdded, ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
        {
            var welcomeText = "Hello and welcome!";
            foreach (var member in membersAdded)
            {
                if (member.Id != turnContext.Activity.Recipient.Id)
                {
                    await turnContext.SendActivityAsync(MessageFactory.Text(welcomeText, welcomeText), cancellationToken);
                }
            }
        }
    }
}
