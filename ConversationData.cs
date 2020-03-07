// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Bot.Connector;
using Microsoft.Bot.Schema;
using System.Collections.Generic;

namespace LPProxyBot.Bots
{
    internal class ConversationData
    {
        public LivePersonConversationRecord EscalationRecord { get; set; } = null;
        public List<Activity> ConversationLog { get; set; } = new List<Activity>();
    }
}