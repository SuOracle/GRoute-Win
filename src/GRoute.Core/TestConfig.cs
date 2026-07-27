namespace GRoute.Windows.Core;

public static class TestConfig
{
    public const int SocksPort = 10626;
    public const int HttpPort = 10809;

    public static string Build() => $$"""
    {
      "log": { "loglevel": "warning" },
      "inbounds": [
        {
          "tag": "socks-in",
          "listen": "127.0.0.1",
          "port": {{SocksPort}},
          "protocol": "socks",
          "settings": { "udp": true }
        },
        {
          "tag": "http-in",
          "listen": "127.0.0.1",
          "port": {{HttpPort}},
          "protocol": "http"
        }
      ],
      "outbounds": [
        { "tag": "direct", "protocol": "freedom" }
      ]
    }
    """;
}
