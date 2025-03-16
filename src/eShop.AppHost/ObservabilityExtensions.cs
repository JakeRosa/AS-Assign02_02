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
    public static (IResourceBuilder<ContainerResource>, IResourceBuilder<ContainerResource>, IResourceBuilder<ContainerResource>) AddObservability(this IDistributedApplicationBuilder builder)
    {
        // Adicionar Prometheus
        var prometheus = builder.AddContainer("prometheus", "prom/prometheus:latest");
        prometheus.WithBindMount("../../prometheus.yml", "/etc/prometheus/prometheus.yml");
        prometheus.WithEndpoint( 9090,  9090, name: "prometheus-ui", isExternal: true);
        
        // Adicionar Grafana
        var grafana = builder.AddContainer("grafana", "grafana/grafana:latest");
        grafana.WithVolume("grafana-storage", "/var/lib/grafana");
        grafana.WithBindMount("./grafana/provisioning", "/etc/grafana/provisioning");
        grafana.WithBindMount("./grafana/dashboards", "/var/lib/grafana/dashboards");
        grafana.WithEndpoint( 3000,  3000, name: "grafana-ui", isExternal: true);
        grafana.WithEnvironment("GF_SECURITY_ADMIN_PASSWORD", "admin");
        
        // jaeger.yml is in the root of the project
        var jaegerPath = "../../jaeger.yml";
        
        // Adicionar Jaeger
        var jaeger = builder.AddContainer("jaeger", "jaegertracing/jaeger", "2.3.0")
            .WithEndpoint(port: 16686, targetPort: 16686, name: "jaeger-ui")
            .WithEndpoint(port: 4319, targetPort: 4319, name: "jaeger-otlp-grpc")
            .WithEndpoint(port: 4320, targetPort: 4320, name: "jaeger-otlp-http")
            .WithEndpoint(port: 14250, targetPort: 14250, name: "jaeger-collector")
            .WithEndpoint(port: 9411, targetPort: 9411, name: "jaeger-zipkin")
            .WithEndpoint(port: 6831, targetPort: 6831, name: "jaeger-thrift-udp")
            .WithEnvironment("COLLECTOR_OTLP_ENABLED", "true")
            .WithEnvironment("COLLECTOR_ZIPKIN_HOST_PORT", "9411")
            .WithBindMount(jaegerPath, "/etc/jaeger/config.yml")
            .WithArgs("--config", "/etc/jaeger/config.yml");
        
        // Adicionar OpenTelemetry Collector
        var otelCollector = builder.AddContainer("otel-collector", "otel/opentelemetry-collector-contrib:latest");
        otelCollector.WithBindMount("../../otel-collector-config.yml", "/etc/otel-collector-config.yml");
        otelCollector.WithEndpoint( 4317,  14317, name: "otlp-grpc");
        otelCollector.WithEndpoint( 4318,  14318, name: "otlp-http");
        otelCollector.WithEndpoint( 9464,  9464, name: "prometheus-exporter");
        otelCollector.WithArgs("--config=/etc/otel-collector-config.yml");
        
        // Definir dependências
        prometheus.WaitFor(jaeger);
        otelCollector.WaitFor(prometheus);
        otelCollector.WaitFor(jaeger);
        grafana.WaitFor(prometheus);
        grafana.WaitFor(jaeger);

        return (prometheus, grafana, jaeger);
    }
    
    /// <summary>
    /// Configures a service to send telemetry to the observability infrastructure
    /// </summary>
    public static IResourceBuilder<T> WithObservability<T>(
        this IResourceBuilder<T> builder, 
        string serviceName, 
        IResourceBuilder<ContainerResource> jaeger) 
        where T : IResourceWithEnvironment
    {
        // Get the fixed IP address for Jaeger instead of relying on DNS resolution
        var otelEndpoint = "http://localhost:4319";
        
        return builder
            // Add direct connection to make debugging easier
            .WithEnvironment("ConnectionStrings__Jaeger", otelEndpoint)
            // Add standard OpenTelemetry configuration
            .WithEnvironment("OTEL_SERVICE_NAME", serviceName)
            .WithEnvironment("OTEL_RESOURCE_ATTRIBUTES", $"service.name={serviceName}")
            .WithEnvironment("OTEL_EXPORTER_OTLP_ENDPOINT", otelEndpoint)
            .WithEnvironment("OTEL_EXPORTER_OTLP_PROTOCOL", "grpc")
            .WithEnvironment("OTEL_METRICS_EXPORTER", "otlp,prometheus")
            .WithEnvironment("OTEL_LOGS_EXPORTER", "otlp")
            .WithEnvironment("OTEL_TRACES_EXPORTER", "otlp")
            .WithEnvironment("OTEL_TRACES_SAMPLER", "always_on")
            .WithEnvironment("OTEL_PROPAGATORS", "tracecontext,baggage")
            // Explicitly enable instrumentation
            .WithEnvironment("ASPNETCORE_HOSTINGSTARTUPASSEMBLIES", "Microsoft.Extensions.Telemetry.Abstractions");
    }
}
