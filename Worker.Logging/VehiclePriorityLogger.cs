// SPDX-License-Identifier: MIT
// Copyright: 2023 Econolite Systems, Inc.
using Econolite.Ode.Messaging;
using Econolite.Ode.Messaging.Elements;
using Econolite.Ode.Models.VehiclePriority;
using Econolite.Ode.Monitoring.Events;
using Econolite.Ode.Monitoring.Events.Extensions;
using Econolite.Ode.Monitoring.Metrics;
using Econolite.Ode.Repository.VehiclePriority;

namespace Econolite.Ode.Worker.Logging
{
    public class VehiclePriorityLogger : BackgroundService
    {
        private readonly ILogger _logger;
        private readonly IScalingConsumer<Guid, PriorityRequestMessage> _scalingPriorityRequestMessageConsumer;
        private readonly IScalingConsumer<Guid, PriorityResponseMessage> _scalingPriorityResponseMessageConsumer;
        private readonly IPriorityRequestLogStore _priorityRequestLogStore;
        private readonly IPriorityResponseLogStore _priorityResponseLogStore;
        private readonly IConfiguration _configuration;
        private readonly UserEventFactory _userEventFactory;
        private readonly IMetricsCounter _requestCounter;
        private readonly IMetricsCounter _responseCounter;

        public VehiclePriorityLogger(IScalingConsumer<Guid, PriorityRequestMessage> scalingPriorityRequestMessageConsumer, IScalingConsumer<Guid, PriorityResponseMessage> scalingPriorityResponseMessageConsumer, IPriorityRequestLogStore priorityRequestLogStore, IPriorityResponseLogStore priorityResponseLogStore, IConfiguration configuration, UserEventFactory userEventFactory, IMetricsFactory metricsFactory, ILoggerFactory loggerFactory)
        {
            if (loggerFactory is null)
            {
                throw new ArgumentNullException(nameof(loggerFactory));
            }

            _logger = loggerFactory.CreateLogger(GetType().Name);
            _scalingPriorityRequestMessageConsumer = scalingPriorityRequestMessageConsumer ?? throw new ArgumentNullException(nameof(scalingPriorityRequestMessageConsumer));
            _scalingPriorityResponseMessageConsumer = scalingPriorityResponseMessageConsumer ?? throw new ArgumentNullException(nameof(scalingPriorityResponseMessageConsumer));
            _priorityRequestLogStore = priorityRequestLogStore ?? throw new ArgumentNullException(nameof(priorityRequestLogStore));
            _priorityResponseLogStore = priorityResponseLogStore ?? throw new ArgumentNullException(nameof(priorityResponseLogStore));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _userEventFactory = userEventFactory ?? throw new ArgumentNullException(nameof(userEventFactory));

            _requestCounter = metricsFactory.GetMetricsCounter("Vehicle Priority Requests");
            _responseCounter = metricsFactory.GetMetricsCounter("Vehicle Priority Responses");
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _ = Task.Run(async () =>
            {
                await Task.WhenAll(
                    ConsumeRequestsAsync(stoppingToken),
                    ConsumeResponsesAsync(stoppingToken)
                    );
            }, stoppingToken);
            return Task.CompletedTask;
        }

        private async Task ConsumeRequestsAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (true)
                {
                    try
                    {
                        await _scalingPriorityRequestMessageConsumer.ConsumeOn(_configuration[Domain.VehiclePriority.Consts.TOPIC_ODE_VEHICLE_REQUEST] ?? Domain.VehiclePriority.Consts.TOPIC_ODE_VEHICLE_REQUEST_DEFAULT, async consumerResult =>
                        {
                            try
                            {
                                await StoreAsync(consumerResult);

                                _requestCounter.Increment();
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Error processing vehicle priority request: {@}", consumerResult.Value);

                                _logger.ExposeUserEvent(_userEventFactory.BuildUserEvent(EventLevel.Error, string.Format("Error processing vehicle priority request: {0}", consumerResult.Value?.Request?.RequestId)));
                            }
                        }, cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception)
                    {
                        _logger.LogError("Unable to start Request Consumer");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Stopping Request Consumer");
            }
            catch (Exception)
            {
                _logger.LogCritical("Request Consumer stopped");
            }
        }

        private async Task ConsumeResponsesAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (true)
                {
                    try
                    {
                        await _scalingPriorityResponseMessageConsumer.ConsumeOn(_configuration[Domain.VehiclePriority.Consts.TOPIC_ODE_VEHICLE_RESPONSE] ?? Domain.VehiclePriority.Consts.TOPIC_ODE_VEHICLE_RESPONSE_DEFAULT, async consumerResult =>
                        {
                            try
                            {
                                await StoreAsync(consumerResult);

                                _responseCounter.Increment();
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Error processing vehicle priority response: {@}", consumerResult.Value);

                                _logger.ExposeUserEvent(_userEventFactory.BuildUserEvent(EventLevel.Error, string.Format("Error processing vehicle priority response: {0}", consumerResult.Value?.PriorityResponse?.RequestId)));
                            }
                        }, cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception)
                    {
                        _logger.LogError("Unable to start Response Consumer");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Stopping Response Consumer");
            }
            catch (Exception)
            {
                _logger.LogCritical("Response Consumer stopped");
            }
        }

        private async Task StoreAsync(ConsumeResult<Guid, PriorityRequestMessage> result)
        {
            // Note eating the exception here will result in the message consumer believing the message was successfully processed.
            if (result.DeviceId.HasValue)
            {
                await _priorityRequestLogStore.InsertAsync(result.DeviceId.Value, result.Value);
            }
        }

        private async Task StoreAsync(ConsumeResult<Guid, PriorityResponseMessage> result)
        {
            // Note eating the exception here will result in the message consumer believing the message was successfully processed.
            if (result.DeviceId.HasValue)
            {
                await _priorityResponseLogStore.InsertAsync(result.DeviceId.Value, result.Value);
            }
        }
    }
}
