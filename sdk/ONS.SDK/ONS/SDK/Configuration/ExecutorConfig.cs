using Microsoft.Extensions.Configuration;

namespace ONS.SDK.Configuration {
    public class ExecutorConfig : BaseServiceConfig {
        public ExecutorConfig (IConfiguration conf) {
            this.Host = conf.GetValue ("EXECUTOR_HOST", "localhost");
            this.Scheme = conf.GetValue ("EXECUTOR_SCHEME", "http");
            this.Port = conf.GetValue ("EXECUTOR_PORT", "8000");
        }
    }
}