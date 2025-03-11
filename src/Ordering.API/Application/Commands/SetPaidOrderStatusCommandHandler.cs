using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace eShop.Ordering.API.Application.Commands;

using MediatR;

// Regular CommandHandler
public class SetPaidOrderStatusCommandHandler : IRequestHandler<SetPaidOrderStatusCommand, bool>
{
    private readonly IOrderRepository _orderRepository;
    private readonly ILogger<SetPaidOrderStatusCommandHandler> _logger;
    private readonly Counter<long> _orderPaidCounter;
    private readonly Histogram<double> _paymentProcessingTimeHistogram;
    private readonly Counter<long> _paymentProcessingErrorsCounter;

    public SetPaidOrderStatusCommandHandler(
        IOrderRepository orderRepository,
        ILogger<SetPaidOrderStatusCommandHandler> logger,
        [FromKeyedServices("orderPaidCounter")] Counter<long> orderPaidCounter,
        [FromKeyedServices("paymentProcessingTimeHistogram")] Histogram<double> paymentProcessingTimeHistogram,
        [FromKeyedServices("paymentProcessingErrorsCounter")] Counter<long> paymentProcessingErrorsCounter)
    {
        _orderRepository = orderRepository ?? throw new ArgumentNullException(nameof(orderRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _orderPaidCounter = orderPaidCounter ?? throw new ArgumentNullException(nameof(orderPaidCounter));
        _paymentProcessingTimeHistogram = paymentProcessingTimeHistogram ?? throw new ArgumentNullException(nameof(paymentProcessingTimeHistogram));
        _paymentProcessingErrorsCounter = paymentProcessingErrorsCounter ?? throw new ArgumentNullException(nameof(paymentProcessingErrorsCounter));
    }

    public async Task<bool> Handle(SetPaidOrderStatusCommand command, CancellationToken cancellationToken)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        try
        {
            // Simulate a work time for validating the payment
            await Task.Delay(10000, cancellationToken);

            var orderToUpdate = await _orderRepository.GetAsync(command.OrderNumber);
            if (orderToUpdate == null)
            {
                _logger.LogWarning("Order {OrderNumber} not found", command.OrderNumber);
                return false;
            }

            orderToUpdate.SetPaidStatus();
            
            // Incrementar contador de pedidos pagos
            _orderPaidCounter.Add(1, new KeyValuePair<string, object>("orderId", orderToUpdate.Id.ToString()));
            _logger.LogInformation("Order {OrderNumber} marked as paid", command.OrderNumber);
            
            var result = await _orderRepository.UnitOfWork.SaveEntitiesAsync(cancellationToken);
            
            stopwatch.Stop();
            // Registrar tempo de processamento do pagamento em segundos
            _paymentProcessingTimeHistogram.Record(stopwatch.Elapsed.TotalSeconds, 
                new KeyValuePair<string, object>("orderId", orderToUpdate.Id.ToString()));
            
            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _paymentProcessingErrorsCounter.Add(1, new KeyValuePair<string, object>("errorType", ex.GetType().Name));
            _logger.LogError(ex, "Error processing payment for order {OrderNumber}", command.OrderNumber);
            throw;
        }
    }
}


// Use for Idempotency in Command process
public class SetPaidIdentifiedOrderStatusCommandHandler : IdentifiedCommandHandler<SetPaidOrderStatusCommand, bool>
{
    public SetPaidIdentifiedOrderStatusCommandHandler(
        IMediator mediator,
        IRequestManager requestManager,
        ILogger<IdentifiedCommandHandler<SetPaidOrderStatusCommand, bool>> logger)
        : base(mediator, requestManager, logger)
    {
    }

    protected override bool CreateResultForDuplicateRequest()
    {
        return true; // Ignore duplicate requests for processing order.
    }
}
