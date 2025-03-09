using Aspire.Hosting;

namespace eShop.AppHost;

/// <summary>
/// Extensions to add observability containers to the application.
/// </summary>
public static class ObservabilityExtensions
{
    /// <summary>
    /// Adds and configures containers for observability (Prometheus, Grafana, and OpenTelemetry Collector).
    /// </summary>
    /// <param name="builder">The distributed application builder.</param>
    /// <returns>The distributed application builder for chaining.</returns>
    public static IDistributedApplicationBuilder AddObservability(this IDistributedApplicationBuilder builder)
    {
        // Adicionar Prometheus
        var prometheus = builder.AddContainer("prometheus", "prom/prometheus:latest");
        prometheus.WithBindMount("../../prometheus.yml", "/etc/prometheus/prometheus.yml");
        prometheus.WithEndpoint( 9090,  9090, name: "prometheus-ui", isExternal: true);
        
        // Adicionar Grafana
        var grafana = builder.AddContainer("grafana", "grafana/grafana:latest");
        grafana.WithVolume("grafana-storage", "/var/lib/grafana");
        grafana.WithEndpoint( 3000,  3000, name: "grafana-ui", isExternal: true);
        grafana.WithEnvironment("GF_SECURITY_ADMIN_PASSWORD", "admin");
        
        // Adicionar Jaeger
        var jaeger = builder.AddContainer("jaeger", "jaegertracing/jaeger", "2.3.0");
        jaeger.WithEndpoint(16686, 16686, name: "jaeger-ui", isExternal: true);
        jaeger.WithEndpoint(port: 4317, targetPort: 4317, name: "jaeger-otlp-grpc");
        jaeger.WithEndpoint(port: 4318, targetPort: 4318, name: "jaeger-otlp-http");
        jaeger.WithEndpoint(port: 14250, targetPort: 14250, name: "jaeger-collector");
        jaeger.WithEndpoint(port: 9411, targetPort: 9411, name: "jaeger-zipkin");
        jaeger.WithEndpoint(6831, 6831, name: "jaeger-thrift-udp");
        jaeger.WithEnvironment("COLLECTOR_ZIPKIN_HTTP_PORT", "9411");
        jaeger.WithEnvironment("COLLECTOR_OTLP_ENABLED", "true");
        jaeger.WithEnvironment("LOG_LEVEL", "debug");
        
        // Adicionar OpenTelemetry Collector
        var otelCollector = builder.AddContainer("otel-collector", "otel/opentelemetry-collector-contrib:latest");
        otelCollector.WithBindMount("../../otel-collector-config.yml", "/etc/otel-collector-config.yml");
        otelCollector.WithEndpoint( 4317,  14317, name: "otlp-grpc");
        otelCollector.WithEndpoint( 4318,  14318, name: "otlp-http");
        otelCollector.WithEndpoint( 9464,  9464, name: "prometheus-exporter");
        otelCollector.WithEndpoint( 14268,  14268, name: "otlp-jaeger");
        otelCollector.WithArgs("--config=/etc/otel-collector-config.yml");
        
        // Definir dependências
        prometheus.WaitFor(jaeger);
        otelCollector.WaitFor(prometheus);
        otelCollector.WaitFor(jaeger);
        grafana.WaitFor(prometheus);
        grafana.WaitFor(jaeger);
        
        return builder;
    }
}
