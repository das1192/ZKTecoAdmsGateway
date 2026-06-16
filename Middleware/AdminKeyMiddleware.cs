namespace ZKTecoGateway.Middleware
{
    /// <summary>
    /// Protects /admin/* routes with a static key in X-Admin-Key header or ?adminKey= query param.
    /// Set GatewaySettings:AdminKey in appsettings.json to enable.
    /// Leave empty to disable (allow all — only do this on internal networks).
    /// </summary>
    public class AdminKeyMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly string _adminKey;

        public AdminKeyMiddleware(RequestDelegate next, IConfiguration config)
        {
            _next = next;
            _adminKey = config["GatewaySettings:AdminKey"] ?? "";
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (context.Request.Path.StartsWithSegments("/admin") && !string.IsNullOrEmpty(_adminKey))
            {
                var providedKey =
                    context.Request.Headers["X-Admin-Key"].FirstOrDefault() ??
                    context.Request.Query["adminKey"].FirstOrDefault() ?? "";

                if (providedKey != _adminKey)
                {
                    context.Response.StatusCode = 401;
                    await context.Response.WriteAsync("Unauthorized");
                    return;
                }
            }

            await _next(context);
        }
    }
}
