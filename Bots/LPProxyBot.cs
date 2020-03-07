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
using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.Web.CodeGeneration.Contracts.Messaging;
using Newtonsoft.Json;

namespace LPProxyBot.Bots
{
    public class LPProxyBot : ActivityHandler
    {
        private readonly BotState _conversationState;
        private readonly IConfiguration _configuration;
        private readonly string _appId;

        public LPProxyBot(ConversationState conversationState, IConfiguration configuration)
        {
            _conversationState = conversationState;
            _appId = configuration["MicrosoftAppId"];
        }

        static Dictionary<string, string> Capitals = new Dictionary<string, string>
        {
            ["France"] = "Paris",
            ["Italy"] = "Rome",
            ["Japan"] = "Tokyo",
            ["Poland"] = "Warsaw",
            ["Germany"] = "Hamburg" // not really
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

                var transcript = new Transcript(conversationData.ConversationLog.Where(a => a.Type == ActivityTypes.Message).ToList());

                var evnt = EventFactory.CreateHandoffInitiation(turnContext, new { Skill = "Credit Cards" }, transcript);

                await turnContext.SendActivityAsync(evnt);
            }
            else
            {
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
        }

        protected override async Task OnEventAsync(ITurnContext<IEventActivity> turnContext, CancellationToken cancellationToken)
        {
            if(turnContext.Activity.Name == "handoff.status")
            {
                var conversationStateAccessors = _conversationState.CreateProperty<ConversationData>(nameof(ConversationData));
                var conversationData = await conversationStateAccessors.GetAsync(turnContext, () => new ConversationData());

                var status = JsonConvert.DeserializeObject<LivePersonHandoffStatus>(turnContext.Activity.Value.ToString());
                string text;
                if(status.state == "accepted")
                {
                    if(conversationData.Acked)
                    {
                        // already acked, get out
                        return;
                    }
                    text = "An agent has accepted the conversation and will respond shortly.";
                    conversationData.Acked = true;
                    await _conversationState.SaveChangesAsync(turnContext);
                }
                else if (status.state == "completed")
                {
                    text = "The agent has closed the conversation.";
                }
                else
                {
                    text = $"Conversation status changed to '{status.state}'";
                }

                // Can only respond as a proactive message, not directly on turnContext

                var conversationRef = turnContext.Activity.GetConversationReference();
                await turnContext.Adapter.ContinueConversationAsync(
                    _appId,
                    conversationRef,
                    (ITurnContext turnContext, CancellationToken cancellationToken) =>
                        turnContext.SendActivityAsync(MessageFactory.Text(text), cancellationToken),
                    default(CancellationToken));
            }

            await base.OnEventAsync(turnContext, cancellationToken);
        }
    }

    // TODO: move this
    class LivePersonHandoffStatus
    {
        public string state;
        public string message;
    }
}
