using System.Diagnostics.Metrics;
using OpenTelemetry.Metrics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using eShop.Ordering.API.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddApplicationServices();
builder.Services.AddProblemDetails();

var withApiVersioning = builder.Services.AddApiVersioning();

builder.AddDefaultOpenApi(withApiVersioning);

// Creating meter
var meter = new Meter("Ordering.API");
builder.Services.AddSingleton(meter);

// Registre as métricas com chaves específicas
builder.Services.AddKeyedSingleton<Counter<long>>("orderPlacedCounter",
    meter.CreateCounter<long>("order_placed_count", description: "Número total de pedidos criados"));

builder.Services.AddKeyedSingleton<Counter<long>>("orderPaidCounter",
    meter.CreateCounter<long>("order_paid_count", description: "Número total de pedidos pagos"));

builder.Services.AddKeyedSingleton<Histogram<double>>("orderProcessingTimeHistogram",
    meter.CreateHistogram<double>("order_processing_time_seconds", description: "Tempo de processamento de pedidos em segundos"));

builder.Services.AddKeyedSingleton<Histogram<double>>("paymentProcessingTimeHistogram",
    meter.CreateHistogram<double>("order_payment_processing_time_seconds", description: "Tempo de processamento de pagamentos em segundos"));

builder.Services.AddKeyedSingleton<Counter<long>>("orderItemsCounter",
    meter.CreateCounter<long>("order_items_count", description: "Número total de itens em pedidos"));

builder.Services.AddKeyedSingleton<Counter<long>>("orderValueCounter",
    meter.CreateCounter<long>("order_value_total", unit: "currency", description: "Valor total dos pedidos"));

builder.Services.AddKeyedSingleton<Counter<long>>("orderProcessingErrorsCounter",
    meter.CreateCounter<long>("order_processing_errors", description: "Número de erros no processamento de pedidos"));

builder.Services.AddKeyedSingleton<Counter<long>>("paymentProcessingErrorsCounter",
    meter.CreateCounter<long>("payment_processing_errors", description: "Número de erros no processamento de pagamentos"));

// Config meters
builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics =>
    {
        metrics
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddMeter("Ordering.API")
            .AddOtlpExporter(options =>
            {
                options.Endpoint = new Uri("http://localhost:4317");
            });
    });

var app = builder.Build();

app.MapDefaultEndpoints();

// Make sure to add the middleware in the right order - authentication should be before our user tracking
app.UseAuthentication();
app.UseAuthorization();

// Add user tracking middleware - this will add user_id and user_name tags to all traces
app.UseUserTracking();

var orders = app.NewVersionedApi("Orders");

orders.MapOrdersApiV1()
      .RequireAuthorization();

app.UseDefaultOpenApi();
app.Run();
