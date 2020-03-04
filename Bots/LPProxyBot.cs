// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Connector;
using Microsoft.Bot.Schema;
using Microsoft.VisualStudio.Web.CodeGeneration.Contracts.Messaging;

namespace LPProxyBot.Bots
{
    public class LPProxyBot : ActivityHandler
    {
        private BotState _conversationState;

        public LPProxyBot(ConversationState conversationState)
        {
            _conversationState = conversationState;
        }

        static Dictionary<string, string> Capitals = new Dictionary<string, string>
        {
            ["France"] = "Paris",
            ["Italy"] = "Rome",
            ["Japan"] = "Tokyo"
        };

        protected override async Task OnMessageActivityAsync(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken)
        {
            var conversationStateAccessors = _conversationState.CreateProperty<ConversationData>(nameof(ConversationData));
            var conversationData = await conversationStateAccessors.GetAsync(turnContext, () => new ConversationData());
            if (conversationData.EscalationRecord != null)
            {
                // In the current implementation, a conversation is considered escalated as soon as the user
                // asks for an escalation. Other strategy would make it escalated after an agent has responded
                // to the request. Until then, the conversation must be handled by the bot.
                throw new InvalidOperationException("Bug: this conversation is in escalated state. The message must be routed to the agent");
            }

            var userText = turnContext.Activity.Text.ToLower();
            if (userText.Contains("agent"))
            {
                await turnContext.SendActivityAsync("Your request will be escalated to a human agent");
                var messages = conversationData.ConversationLog.Where(a => a.Type == ActivityTypes.Message).ToList();
                var evnt = EventFactory.CreateHandoffInitiation(turnContext, new { Skill = "Any" }, new Transcript(messages) );
                await turnContext.SendActivityAsync(evnt);
                return;
            }

            string replyText = $"Sorry, I cannot help you.";
            if (userText == "hi")
            {
                replyText = "Hello!";
            }
            else
            {
                foreach (var country in Capitals.Keys)
                {
                    if (userText.Contains(country.ToLower()))
                    {
                        replyText = $"The capital of {country} is {Capitals[country]}";
                    }
                }
            }

            await turnContext.SendActivityAsync(MessageFactory.Text(replyText, replyText), cancellationToken);
        }

        protected override async Task OnMembersAddedAsync(IList<ChannelAccount> membersAdded, ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
        {
            var welcomeText = "Hello! I can answer questions about geography.";
            foreach (var member in membersAdded)
            {
                if (member.Id != turnContext.Activity.Recipient.Id)
                {
                    // await turnContext.SendActivityAsync(MessageFactory.Text(welcomeText, welcomeText), cancellationToken);
                }
            }
        }
    }
}
