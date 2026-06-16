namespace ZKTecoGateway.Config
{
    public class ScheduledPullConfig
    {
        public bool Enabled { get; set; } = true;

        public List<string> Times { get; set; } = new();
    }
}
