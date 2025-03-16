using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace eShop.Ordering.API.Application.Commands;

using eShop.Ordering.Domain.AggregatesModel.OrderAggregate;
using MediatR;

// Regular CommandHandler
public class CreateOrderCommandHandler : IRequestHandler<CreateOrderCommand, bool>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IIdentityService _identityService;
    private readonly IMediator _mediator;
    private readonly IOrderingIntegrationEventService _orderingIntegrationEventService;
    private readonly ILogger<CreateOrderCommandHandler> _logger;
    private readonly Counter<long> _orderPlacedCounter;
    private readonly Counter<long> _orderItemsCounter;
    private readonly Counter<long> _orderValueCounter;
    private readonly Histogram<double> _orderProcessingTimeHistogram;
    private readonly Counter<long> _orderProcessingErrorsCounter;

    public CreateOrderCommandHandler(
        IMediator mediator,
        IOrderingIntegrationEventService orderingIntegrationEventService,
        IOrderRepository orderRepository,
        IIdentityService identityService,
        ILogger<CreateOrderCommandHandler> logger,
        [FromKeyedServices("orderPlacedCounter")] Counter<long> orderPlacedCounter,
        [FromKeyedServices("orderItemsCounter")] Counter<long> orderItemsCounter,
        [FromKeyedServices("orderValueCounter")] Counter<long> orderValueCounter,
        [FromKeyedServices("orderProcessingTimeHistogram")] Histogram<double> orderProcessingTimeHistogram,
        [FromKeyedServices("orderProcessingErrorsCounter")] Counter<long> orderProcessingErrorsCounter)
    {
        _orderRepository = orderRepository ?? throw new ArgumentNullException(nameof(orderRepository));
        _identityService = identityService ?? throw new ArgumentNullException(nameof(identityService));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _orderingIntegrationEventService = orderingIntegrationEventService ?? throw new ArgumentNullException(nameof(orderingIntegrationEventService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _orderPlacedCounter = orderPlacedCounter ?? throw new ArgumentNullException(nameof(orderPlacedCounter));
        _orderItemsCounter = orderItemsCounter ?? throw new ArgumentNullException(nameof(orderItemsCounter));
        _orderValueCounter = orderValueCounter ?? throw new ArgumentNullException(nameof(orderValueCounter));
        _orderProcessingTimeHistogram = orderProcessingTimeHistogram ?? throw new ArgumentNullException(nameof(orderProcessingTimeHistogram));
        _orderProcessingErrorsCounter = orderProcessingErrorsCounter ?? throw new ArgumentNullException(nameof(orderProcessingErrorsCounter));
    }

    public async Task<bool> Handle(CreateOrderCommand message, CancellationToken cancellationToken)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            // Add Integration event to clean the basket
            var orderStartedIntegrationEvent = new OrderStartedIntegrationEvent(message.UserId);
            await _orderingIntegrationEventService.AddAndSaveEventAsync(orderStartedIntegrationEvent);

            // Add/Update the Buyer AggregateRoot
            var address = new Address(message.Street, message.City, message.State, message.Country, message.ZipCode);
            var order = new Order(message.UserId, message.UserName, address, message.CardTypeId, message.CardNumber, message.CardSecurityNumber, message.CardHolderName, message.CardExpiration);

            // Contador de itens do pedido
            int totalItems = 0;
            decimal orderTotal = 0;

            foreach (var item in message.OrderItems)
            {
                order.AddOrderItem(item.ProductId, item.ProductName, item.UnitPrice, item.Discount, item.PictureUrl, item.Units);
                totalItems += item.Units;
                orderTotal += (item.UnitPrice - item.Discount) * item.Units;
            }

            // Métricas
            _logger.LogInformation("Incrementing Order Placed Counter");
            _orderPlacedCounter.Add(1, new KeyValuePair<string, object>("userId", message.UserId));

            // Contador de itens
            _orderItemsCounter.Add(totalItems, new KeyValuePair<string, object>("orderId", order.Id.ToString()));

            // Valor total do pedido
            _orderValueCounter.Add((long)orderTotal, new KeyValuePair<string, object>("orderId", order.Id.ToString()));

            _logger.LogInformation("Creating Order - Order: {@Order}", order);

            _orderRepository.Add(order);

            var result = await _orderRepository.UnitOfWork.SaveEntitiesAsync(cancellationToken);

            stopwatch.Stop();
            // Registrar tempo de processamento em segundos
            _orderProcessingTimeHistogram.Record(stopwatch.Elapsed.TotalSeconds,
                new KeyValuePair<string, object>("orderId", order.Id.ToString()),
                new KeyValuePair<string, object>("userId", message.UserId));
            
            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _orderProcessingErrorsCounter.Add(1, new KeyValuePair<string, object>("errorType", ex.GetType().Name));
            _logger.LogError(ex, "Error processing order");
            throw;
        }
    }
}


// Use for Idempotency in Command process
public class CreateOrderIdentifiedCommandHandler : IdentifiedCommandHandler<CreateOrderCommand, bool>
{
    public CreateOrderIdentifiedCommandHandler(
        IMediator mediator,
        IRequestManager requestManager,
        ILogger<IdentifiedCommandHandler<CreateOrderCommand, bool>> logger)
        : base(mediator, requestManager, logger)
    {
    }

    protected override bool CreateResultForDuplicateRequest()
    {
        return true; // Ignore duplicate requests for creating order.
    }
}
