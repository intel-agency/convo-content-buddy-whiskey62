# ADR-0002: Implement Triple Modular Redundancy (TMR)

## Status
Accepted

## Context

ConvoContentBuddy is designed for mission-critical interview assistance. The system must:
- Maintain 100% uptime during single-container failures
- Preserve SignalR WebSocket connections across failures
- Ensure no transcript data loss
- Provide seamless failover without user notification

The API layer is the critical path - any failure here disrupts the entire user experience.

## Decision

We will implement **Triple Modular Redundancy (TMR)** for the API.Brain service with:
- 3 identical replicas of the API service
- Redis backplane for SignalR connection synchronization
- Load balancer for request distribution
- Automatic health checks and restart policies

### TMR Architecture

```
                    Load Balancer
                         │
        ┌────────────────┼────────────────┐
        │                │                │
        ▼                ▼                ▼
   ┌────────┐       ┌────────┐       ┌────────┐
   │ API #1 │       │ API #2 │       │ API #3 │
   └────────┘       └────────┘       └────────┘
        │                │                │
        └────────────────┼────────────────┘
                         │
                    Redis Backplane
                  (SignalR Sync + Cache)
```

### Configuration

**Docker Compose**:
```yaml
api-brain:
  deploy:
    replicas: 3
    restart_policy:
      condition: on-failure
      delay: 5s
      max_attempts: 3
```

**Aspire**:
```csharp
builder.AddProject<Projects.API_Brain>("api-brain")
    .WithReplicas(3)
    .WithReference(redis);
```

**SignalR Redis Backplane**:
```csharp
builder.Services.AddSignalR()
    .AddStackExchangeRedis(redisConnectionString, options =>
    {
        options.Configuration.ChannelPrefix = "ConvoContentBuddy:";
    });
```

## Consequences

### Positive
- **Zero Downtime**: System survives loss of any single API instance
- **Connection Persistence**: SignalR connections maintained via Redis backplane
- **Load Distribution**: Requests automatically distributed across healthy instances
- **Graceful Degradation**: N-1 failures still provide full service

### Negative
- **Resource Overhead**: 3x memory and CPU for API layer
- **Complexity**: Additional moving parts (Redis backplane, load balancing)
- **Cost**: Higher infrastructure costs in production

### Neutral
- Requires Redis for backplane
- Load balancer configuration required

## Testing Strategy

### TMR Verification Tests
1. **Instance Failure Test**
   - Kill one API container
   - Verify SignalR connection persists
   - Verify no transcript loss
   - Verify response times remain < 2s

2. **Rolling Update Test**
   - Deploy new version
   - Verify zero-downtime deployment
   - Verify connection migration

3. **Load Test with Failures**
   - Generate load (100 concurrent users)
   - Kill instances randomly
   - Verify 100% success rate

## Monitoring

### Key Metrics
- `api_brain_replicas_active`: Current number of healthy replicas (target: 3)
- `signalr_connections_active`: Active WebSocket connections
- `signalr_reconnect_count`: SignalR reconnection attempts
- `api_request_latency_p99`: 99th percentile latency

### Health Checks
- `/health/ready`: Replica is ready to accept traffic
- `/health/live`: Replica process is alive
- Redis backplane connectivity check

## References

- [SignalR Redis Backplane](https://learn.microsoft.com/aspnet/core/signalr/redis-backplane)
- [Triple Modular Redundancy](https://en.wikipedia.org/wiki/Triple_modular_redundancy)
- [Docker Swarm Replicas](https://docs.docker.com/engine/swarm/services/#replicate-a-service)

---

**Date**: 2026-03-12  
**Decision Makers**: Development Team  
**Supersedes**: N/A
