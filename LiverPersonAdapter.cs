// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using LPProxyBot;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Bot.Builder.TraceExtensions;
using Microsoft.Bot.Connector;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace LPProxyBot
{
    public class LivePersonAdapter : BotFrameworkHttpAdapter
    {
        public LivePersonAdapter(IConfiguration configuration, ILogger<BotFrameworkHttpAdapter> logger, HandoffMiddleware handoffMiddleware, LoggingMiddleware loggingMiddleware, ConversationState conversationState = null)
            : base(configuration, logger)
        {
            Use(loggingMiddleware);
            Use(handoffMiddleware);

            OnTurnError = async (turnContext, exception) =>
            {
                // Log any leaked exception from the application.
                logger.LogError(exception, $"[OnTurnError] unhandled error : {exception.Message}");

                // Send a message to the user
                await turnContext.SendActivityAsync("The bot encounted an error or bug.");
                await turnContext.SendActivityAsync("To continue to run this bot, please fix the bot source code.");

                // Send a trace activity, which will be displayed in the Bot Framework Emulator
                await turnContext.TraceActivityAsync("OnTurnError Trace", exception.Message, "https://www.botframework.com/schemas/error", "TurnError");
            };
        }

        public async Task ProcessActivityAsync(Activity activity, BotCallbackHandler callback, CancellationToken cancellationToken)
        {
            BotAssert.ActivityNotNull(activity);

            using (var context = new TurnContext(this, activity))
            {
                await base.RunPipelineAsync(context, callback, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
