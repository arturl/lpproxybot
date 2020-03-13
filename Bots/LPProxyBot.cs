// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LivePersonConnector;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Connector;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;

namespace LPProxyBot.Bots
{
    public class LPProxyBot : ActivityHandler
    {
        private readonly BotState _conversationState;
        private readonly ICredentialsProvider _creds;

        public LPProxyBot(ConversationState conversationState, ICredentialsProvider creds)
        {
            _conversationState = conversationState;
            _creds = creds;
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
            var conversationStateAccessors = _conversationState.CreateProperty<LoggingConversationData>(nameof(LoggingConversationData));
            var conversationData = await conversationStateAccessors.GetAsync(turnContext, () => new LoggingConversationData());

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
                else if(userText == "info")
                {
                    replyText = $"Version 1.0. AppId: {_creds.LpAppId}";
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
                var conversationStateAccessors = _conversationState.CreateProperty<LoggingConversationData>(nameof(LoggingConversationData));
                var conversationData = await conversationStateAccessors.GetAsync(turnContext, () => new LoggingConversationData());

                string text;
                var state = (turnContext.Activity.Value as JObject)?.Value<string>("state");
                if (state == "accepted")
                {
                    text = "An agent has accepted the conversation and will respond shortly.";
                    await _conversationState.SaveChangesAsync(turnContext);
                }
                else if (state == "completed")
                {
                    text = "The agent has closed the conversation.";
                }
                else
                {
                    text = $"Conversation status changed to '{state}'";
                }

                await turnContext.SendActivityAsync(MessageFactory.Text(text));
            }

            await base.OnEventAsync(turnContext, cancellationToken);
        }
    }
}
