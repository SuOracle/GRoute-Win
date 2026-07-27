using System.Net.Http;
using System.Text.Json.Nodes;

namespace GRoute.Windows.Core;

public static class SingBoxStats
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMilliseconds(900) };

    public static (long Up, long Down) Query(int port)
    {
        try
        {
            var text = Http.GetStringAsync($"http://127.0.0.1:{port}/connections")
                .GetAwaiter().GetResult();
            var node = JsonNode.Parse(text);
            return (Num(node?["uploadTotal"]), Num(node?["downloadTotal"]));
        }
        catch
        {
            return (0, 0);
        }
    }

    private static long Num(JsonNode? node)
    {
        if (node is null) return 0;
        try
        {
            return node.GetValue<long>();
        }
        catch
        {
        }
        try
        {
            return (long)node.GetValue<double>();
        }
        catch
        {
            return 0;
        }
    }
}
