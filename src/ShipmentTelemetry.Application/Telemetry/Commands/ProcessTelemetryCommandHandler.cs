using System.Diagnostics;
using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;
using ShipmentTelemetry.Application.Abstractions;
using ShipmentTelemetry.Contracts.IntegrationEvents;
using ShipmentTelemetry.Domain.Aggregates;
using ShipmentTelemetry.Domain.Enums;
using ShipmentTelemetry.Domain.Models;
using ShipmentTelemetry.Domain.Repositories;
using ShipmentTelemetry.Domain.ValueObjects;

namespace ShipmentTelemetry.Application.Telemetry.Commands;

public sealed class ProcessTelemetryCommandHandler : IRequestHandler<ProcessTelemetryCommand, ProcessTelemetryResult>
{
    private const int MaxConcurrencyRetries = 5;

    private readonly IShipmentOperationalStateRepository _stateRepository;
    private readonly IProcessedTelemetryStore _processedTelemetryStore;
    private readonly IOutboxWriter _outboxWriter;
    private readonly IOperationalStateReadModel _readModel;
    private readonly ITelemetryStatusStore _telemetryStatusStore;
    private readonly IPayloadHasher _payloadHasher;
    private readonly ITelemetryMetrics _metrics;
    private readonly IShipmentOperationalStateCache _cache;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ProcessTelemetryCommandHandler> _logger;

    public ProcessTelemetryCommandHandler(
        IShipmentOperationalStateRepository stateRepository,
        IProcessedTelemetryStore processedTelemetryStore,
        IOutboxWriter outboxWriter,
        IOperationalStateReadModel readModel,
        ITelemetryStatusStore telemetryStatusStore,
        IPayloadHasher payloadHasher,
        ITelemetryMetrics metrics,
        IShipmentOperationalStateCache cache,
        IUnitOfWork unitOfWork,
        ILogger<ProcessTelemetryCommandHandler> logger)
    {
        _stateRepository = stateRepository;
        _processedTelemetryStore = processedTelemetryStore;
        _outboxWriter = outboxWriter;
        _readModel = readModel;
        _telemetryStatusStore = telemetryStatusStore;
        _payloadHasher = payloadHasher;
        _metrics = metrics;
        _cache = cache;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<ProcessTelemetryResult> Handle(
        ProcessTelemetryCommand request,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        _metrics.TelemetryReceived();

        var envelope = BuildEnvelope(request, _payloadHasher);

        for (var attempt = 0; attempt < MaxConcurrencyRetries; attempt++)
        {
            var result = await ProcessOnceAsync(envelope, cancellationToken).ConfigureAwait(false);

            if (result.ShouldRetry)
            {
                _metrics.ConcurrencyConflict();
                await Task.Delay(TimeSpan.FromMilliseconds(25 * (attempt + 1)), cancellationToken)
                    .ConfigureAwait(false);
                continue;
            }

            stopwatch.Stop();
            _metrics.ProcessingDuration(stopwatch.Elapsed);
            RecordOutcomeMetrics(result.Outcome);
            return result;
        }

        
        throw new InvalidOperationException(
            $"Failed to process telemetry {envelope.EventId} due to repeated concurrency conflicts.");
    }

    private async Task<ProcessTelemetryResult> ProcessOnceAsync(
        TelemetryEnvelope envelope,
        CancellationToken cancellationToken)
    {
        //inbox pattern
        var existing = await _processedTelemetryStore
            .FindByEventIdAsync(envelope.EventId, cancellationToken)
            .ConfigureAwait(false);

        if (existing is not null)
        {
            if (existing.PayloadHash == envelope.PayloadHash.Value)
            {
                var duplicateResult = await BuildResultFromExistingStateAsync(
                    envelope.ShipmentId,
                    TelemetryProcessingOutcome.Duplicate,
                    "Duplicate event id.",
                    cancellationToken).ConfigureAwait(false);

                await PersistStatusAsync(envelope.EventId, duplicateResult, cancellationToken)
                    .ConfigureAwait(false);
                return duplicateResult;
            }

            var conflictResult = new ProcessTelemetryResult(
                TelemetryProcessingOutcome.PayloadConflict,
                "Same event id with different payload.",
                null);

            await PersistRejectedAsync(envelope, conflictResult, cancellationToken).ConfigureAwait(false);
            return conflictResult;
        }

        var sequenceOwner = await _processedTelemetryStore
            .FindByContainerSequenceAsync(envelope.ContainerId, envelope.SequenceNumber.Value, cancellationToken)
            .ConfigureAwait(false);

        if (sequenceOwner is not null && sequenceOwner.EventId.Value != envelope.EventId.Value)
        {
            var conflictResult = new ProcessTelemetryResult(
                TelemetryProcessingOutcome.SequenceConflict,
                $"Sequence {envelope.SequenceNumber.Value} already used by event {sequenceOwner.EventId}.",
                null);

            await PersistRejectedAsync(envelope, conflictResult, cancellationToken).ConfigureAwait(false);
            return conflictResult;
        }

        var aggregate = await _stateRepository
            .GetByShipmentIdAsync(envelope.ShipmentId, cancellationToken)
            .ConfigureAwait(false);

        var isNewAggregate = aggregate is null;

        if (isNewAggregate)
        {
            aggregate = ShipmentOperationalState.Create(
                envelope.ShipmentId,
                envelope.ContainerId);

            await _stateRepository.AddAsync(
                aggregate,
                cancellationToken).ConfigureAwait(false);
        }

        if (aggregate.LastAcceptedSequence >= 0
            && envelope.SequenceNumber.Value > aggregate.LastAcceptedSequence + 1)
        {
            _logger.LogDebug(
                "Sequence gap detected for {EventId}; waiting for in-flight lower sequences. Incoming={Incoming} LastAccepted={LastAccepted}",
                envelope.EventId,
                envelope.SequenceNumber.Value,
                aggregate.LastAcceptedSequence);

            return ProcessTelemetryResult.Retry();
        }

        var domainResult = aggregate.ProcessTelemetry(envelope);

        if (domainResult.Outcome != TelemetryProcessingOutcome.Accepted)
        {
            var rejectedResult = new ProcessTelemetryResult(
                domainResult.Outcome,
                domainResult.Reason,
                domainResult.CurrentMilestone);

            await PersistRejectedAsync(envelope, rejectedResult, cancellationToken).ConfigureAwait(false);
            return rejectedResult;
        }

        await _processedTelemetryStore.AddAsync(
            new ProcessedTelemetryRecord(
                envelope.EventId,
                envelope.ContainerId,
                envelope.ShipmentId,
                envelope.SequenceNumber.Value,
                envelope.PayloadHash.Value,
                TelemetryProcessingOutcome.Accepted,
                DateTimeOffset.UtcNow),
            cancellationToken).ConfigureAwait(false);

        var milestoneEvent = domainResult.MilestoneEvent!;
        var integrationEvent = new ShipmentMilestoneRecordedIntegrationEvent(
            milestoneEvent.EventId.Value,
            milestoneEvent.ShipmentId.Value,
            milestoneEvent.ContainerId.Value,
            milestoneEvent.Milestone.ToString(),
            milestoneEvent.SequenceNumber.Value,
            milestoneEvent.RecordedAt);

        await _outboxWriter.EnqueueAsync(
            nameof(ShipmentMilestoneRecordedIntegrationEvent),
            JsonSerializer.Serialize(integrationEvent),
            cancellationToken).ConfigureAwait(false);

        await _readModel.UpsertAsync(
            envelope.ShipmentId,
            envelope.ContainerId,
            domainResult.CurrentMilestone!.Value,
            envelope.SequenceNumber.Value,
            DateTimeOffset.UtcNow,
            cancellationToken).ConfigureAwait(false);

        if (!isNewAggregate)
        {
            await _stateRepository.UpdateAsync(
                aggregate,
                cancellationToken).ConfigureAwait(false);
        }
        await _telemetryStatusStore.SaveAsync(
            new TelemetryStatusRecord(
                envelope.EventId,
                TelemetryProcessingOutcome.Accepted,
                null,
                domainResult.CurrentMilestone,
                DateTimeOffset.UtcNow),
            cancellationToken).ConfigureAwait(false);

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (IsConcurrencyException(ex))
        {
            _logger.LogWarning(
                ex,
                "Concurrency conflict processing telemetry {EventId} for shipment {ShipmentId}",
                envelope.EventId,
                envelope.ShipmentId);

            return ProcessTelemetryResult.Retry();
        }

        aggregate.ClearDomainEvents();

        await _cache.InvalidateAsync(envelope.ShipmentId, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Telemetry accepted. EventId={EventId} ShipmentId={ShipmentId} ContainerId={ContainerId} Sequence={Sequence} Milestone={Milestone}",
            envelope.EventId,
            envelope.ShipmentId,
            envelope.ContainerId,
            envelope.SequenceNumber.Value,
            domainResult.AppliedMilestone);

        return new ProcessTelemetryResult(
            TelemetryProcessingOutcome.Accepted,
            null,
            domainResult.CurrentMilestone);
    }

    private async Task PersistRejectedAsync(
        TelemetryEnvelope envelope,
        ProcessTelemetryResult result,
        CancellationToken cancellationToken)
    {
        await _processedTelemetryStore.AddAsync(
            new ProcessedTelemetryRecord(
                envelope.EventId,
                envelope.ContainerId,
                envelope.ShipmentId,
                envelope.SequenceNumber.Value,
                envelope.PayloadHash.Value,
                result.Outcome,
                DateTimeOffset.UtcNow),
            cancellationToken).ConfigureAwait(false);

        await _telemetryStatusStore.SaveAsync(
            new TelemetryStatusRecord(
                envelope.EventId,
                result.Outcome,
                result.Reason,
                result.CurrentMilestone,
                DateTimeOffset.UtcNow),
            cancellationToken).ConfigureAwait(false);

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogWarning(
            "Telemetry rejected. EventId={EventId} Outcome={Outcome} Reason={Reason}",
            envelope.EventId,
            result.Outcome,
            result.Reason);
    }

    private async Task PersistStatusAsync(
        TelemetryEventId eventId,
        ProcessTelemetryResult result,
        CancellationToken cancellationToken)
    {
        await _telemetryStatusStore.SaveAsync(
            new TelemetryStatusRecord(
                eventId,
                result.Outcome,
                result.Reason,
                result.CurrentMilestone,
                DateTimeOffset.UtcNow),
            cancellationToken).ConfigureAwait(false);

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<ProcessTelemetryResult> BuildResultFromExistingStateAsync(
        ShipmentId shipmentId,
        TelemetryProcessingOutcome outcome,
        string reason,
        CancellationToken cancellationToken)
    {
        var snapshot = await _readModel.GetByShipmentIdAsync(shipmentId, cancellationToken)
            .ConfigureAwait(false);

        return new ProcessTelemetryResult(outcome, reason, snapshot?.CurrentMilestone);
    }

    private static TelemetryEnvelope BuildEnvelope(ProcessTelemetryCommand request, IPayloadHasher payloadHasher)
    {
        var payloadHash = new PayloadHash(payloadHasher.ComputeHash(request.PayloadJson));

        return new TelemetryEnvelope(
            new TelemetryEventId(request.EventId),
            new ContainerId(request.ContainerId),
            new ShipmentId(request.ShipmentId),
            request.EventType,
            new SequenceNumber(request.SequenceNumber),
            request.DeviceTimestamp,
            request.DeviceId,
            new Location(request.LocationName, request.Latitude, request.Longitude),
            request.PayloadJson,
            payloadHash);
    }

    private static bool IsConcurrencyException(Exception ex)
    {
        for (var current = ex; current is not null; current = current.InnerException)
        {
            if (current.GetType().Name.Contains("Concurrency", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (current.Message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase)
                || current.Message.Contains("23505", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private void RecordOutcomeMetrics(TelemetryProcessingOutcome outcome)
    {
        switch (outcome)
        {
            case TelemetryProcessingOutcome.Accepted:
                _metrics.MilestoneRecorded();
                break;
            case TelemetryProcessingOutcome.Duplicate:
                _metrics.TelemetryDuplicate();
                break;
            case TelemetryProcessingOutcome.Stale:
                _metrics.TelemetryStale();
                break;
            default:
                _metrics.MilestoneRejected(outcome);
                break;
        }
    }
}
