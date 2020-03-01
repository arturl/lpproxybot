using System;
using Microsoft.Bot.Builder;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Configuration;
using System.Linq;

namespace LPProxyBot
{
    public class HandoffMiddleware : Microsoft.Bot.Builder.IMiddleware
    {
        IConfiguration _configuration;

        public HandoffMiddleware(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task OnTurnAsync(ITurnContext turnContext, NextDelegate next, CancellationToken cancellationToken = default)
        {
            turnContext.OnSendActivities(async (sendTurnContext, activities, nextSend) =>
            {
                // Handle any escalation events, and let them propagate through the pipeline
                // This is useful for debugging with the Emulator
                var handoffEvents = activities.Where(activity =>
                    activity.Type == ActivityTypes.Event && activity.Name == HandoffEventNames.InitiateHandoff);

                foreach(var handoffEvent in handoffEvents)
                {
                    Escalate(handoffEvent);
                }

                // run full pipeline
                var responses = await nextSend().ConfigureAwait(false);
                return responses;
            });

            await next(cancellationToken);
        }

        private void Escalate(IEventActivity handoffEvent)
        {
            // TBD
        }
    }
}
