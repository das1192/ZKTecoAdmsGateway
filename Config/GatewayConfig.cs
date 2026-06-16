namespace ZKTecoGateway.Config
{
    /// <summary>
    /// Root configuration loaded from appsettings.json
    /// </summary>
    public class GatewayConfig
    {
        public List<ClientConfig> Clients { get; set; } = new();
    }

    /// <summary>
    /// One client = one company / tenant.
    /// They can have multiple devices.
    /// </summary>
    public class ClientConfig
    {
        /// <summary>Unique client identifier (for logging / admin UI)</summary>
        public string ClientId { get; set; } = "";

        /// <summary>Human-readable name</summary>
        public string ClientName { get; set; } = "";

        /// <summary>The URL of the client's own attendance API</summary>
        public List<string> ForwardUrls { get; set; } = new();

        /// <summary>Optional bearer token / API key sent in Authorization header</summary>
        public string ApiKey { get; set; } = "";

        /// <summary>Serial numbers of ZKTeco devices belonging to this client</summary>
        public List<string> DeviceSerialNumbers { get; set; } = new();
    }
}
