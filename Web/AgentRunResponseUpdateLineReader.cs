using System.Collections.Generic;
using System.Text;
using Microsoft.Agents.AI;

namespace AiWebsite.ApiService;

public static class AgentRunResponseUpdateReader
{
    public static async IAsyncEnumerable<string> ReadLines(this IAsyncEnumerator<AgentRunResponseUpdate> updates)
    {
        var sb = new StringBuilder();
        while (await updates.MoveNextAsync())
        {
            var update = updates.Current;

            sb.Append(update.Text);
            var lineEndUpdateIndex = update.Text.IndexOf('\n');
            if (lineEndUpdateIndex is not -1)
            {
                var lineEndIndex = sb.Length - (update.Text.Length - lineEndUpdateIndex);
                var lineLength = sb[lineEndIndex - 1] is '\r' ? lineEndIndex - 1 : lineEndIndex;
                var nextLineStartIndex = lineEndIndex + 1;
                yield return sb.ToString(0, lineLength);
                sb.Remove(0, nextLineStartIndex);
            }
        }
    }

    public static async IAsyncEnumerable<string> ReadTokens(this IAsyncEnumerator<AgentRunResponseUpdate> updates)
    {
        while (await updates.MoveNextAsync())
        {
            yield return updates.Current.Text;
        }
    }
}
