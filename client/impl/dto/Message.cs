namespace io.harness.ff_dotnet_client_sdk.client.impl.dto
{
    internal class Message
    {
        public string? Event { get; set; }
        public string? Domain { get; set; }
        public string? Identifier { get; set; }
        public long? Version { get; set; }
    }
}