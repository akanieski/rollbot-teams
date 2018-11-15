// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.XPath;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Logging;

namespace RollBot
{
    /// <summary>
    /// Allows you to roll dice to be used in a TTRPG
    /// </summary>
    public class RollBot : IBot
    {
        private readonly RollBotAccessors _accessors;
        private readonly ILogger _logger;

        private Regex _startsAndEndsWithBrackets = new Regex(@"\[.*\]");
        private Regex _findDiceMatch = new Regex(@"\dd\d*");
        private Regex _findFirstAlpha = new Regex(@"[a-zA-Z].");

        /// <summary>
        /// Initializes a new instance of the <see cref="RollBot"/> class.
        /// </summary>
        /// <param name="accessors">A class containing <see cref="IStatePropertyAccessor{T}"/> used to manage state.</param>
        /// <param name="loggerFactory">A <see cref="ILoggerFactory"/> that is hooked to the Azure App Service provider.</param>
        public RollBot(RollBotAccessors accessors, ILoggerFactory loggerFactory)
        {
            if (loggerFactory == null)
            {
                throw new System.ArgumentNullException(nameof(loggerFactory));
            }

            _logger = loggerFactory.CreateLogger<RollBot>();
            _logger.LogTrace("Rollbot turn start.");
            _accessors = accessors ?? throw new System.ArgumentNullException(nameof(accessors));
        }

        public async Task OnTurnAsync(ITurnContext turnContext, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (turnContext.Activity.Type == ActivityTypes.Message)
            {
                if (_startsAndEndsWithBrackets.IsMatch(turnContext.Activity.Text))
                {
                    var diceMatch = _findDiceMatch.Match(turnContext.Activity.Text);
                    string diceSection = diceMatch.Value;

                    string afterMathSection = turnContext.Activity.Text.Substring(diceMatch.Index + diceMatch.Length);
                    string verbiage = afterMathSection.Substring(_findFirstAlpha.Match(afterMathSection).Index, 1 + afterMathSection.IndexOf(']') - _findFirstAlpha.Match(afterMathSection).Index);
                    verbiage = verbiage.Substring(0, verbiage.Length - 1);
                    int dice = Convert.ToInt32(diceSection.Split('d')[1]);
                    int diceCount = Convert.ToInt32(diceSection.Split('d')[0]);

                    DataTable dt = new DataTable();

                    int roll = 0;
                    string mathPart = afterMathSection.Substring(0, _findFirstAlpha.Match(afterMathSection).Index);
                    List<int> rolls = new List<int>();
                    for (int x = 0; x < diceCount; x++)
                    {
                        int r = new Random().Next(1, dice);
                        rolls.Add(r);
                        roll += r;
                    }

                    string math = roll + " " + mathPart;
                    var total = Evaluate(math);

                    var responseMessage = $"{turnContext.Activity.From.Name} 🡒 ({string.Join(',', rolls)}) {mathPart} 🡒 \"{verbiage}\" 🡒 **{total}**";
                    await turnContext.SendActivityAsync(responseMessage);

                }
            }
        }

        public static double Evaluate(string expression)
        {
            var xsltExpression =
                string.Format("number({0})",
                    new Regex(@"([\+\-\*])").Replace(expression, " ${1} ")
                                            .Replace("/", " div ")
                                            .Replace("%", " mod "));

            // ReSharper disable PossibleNullReferenceException
            return (double)new XPathDocument
                (new StringReader("<r/>"))
                    .CreateNavigator()
                    .Evaluate(xsltExpression);
            // ReSharper restore PossibleNullReferenceException
        }
    }
}
