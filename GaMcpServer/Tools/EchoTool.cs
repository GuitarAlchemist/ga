namespace GaMcpServer.Tools;

using ModelContextProtocol.Server;

[McpServerToolType]
public static class EchoTool
{
    [McpServerTool]
    [Description("Echoes the message back to the client.")]
    public static string Echo(string message)
    {
        return $"Hello from C#: {message}";
    }

    [McpServerTool]
    [Description("Echoes in reverse the message sent by the client.")]
    public static string ReverseEcho(string message)
    {
        return new string([.. message.Reverse()]);
    }
}
