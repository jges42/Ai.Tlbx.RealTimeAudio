using Ai.Tlbx.RealTimeAudio.OpenAi.Models;
using System;
using System.Threading.Tasks;

namespace Ai.Tlbx.RealTimeAudio.OpenAi.Tools.BuiltInTools
{
    /// <summary>
    /// A built-in tool for retrieving the current date and time.
    /// </summary>
    public class TimeTool : BaseTool
    {
        public override string Name => "get_current_time";

        public override string Description => "Gets the current UTC date and time.";

        // No parameters needed for this tool, so we use the base class default (empty object schema).

        /// <summary>
        /// Executes the tool to get the current time.
        /// </summary>
        /// <param name="argumentsJson">Ignored for this tool as it takes no arguments.</param>
        /// <returns>The current UTC date and time as an ISO 8601 string.</returns>
        public override Task<string> ExecuteAsync(string argumentsJson)
        {
            // Arguments are ignored.
            // Return the current UTC time in a standard format (ISO 8601 is good for APIs)
            var currentTime = DateTime.UtcNow.ToString("o"); 
            return Task.FromResult(currentTime);
        }
    }
} 