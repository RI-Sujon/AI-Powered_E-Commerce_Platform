# Redis integration

This document captures the Redis integration added to the ECommerce app.

## Goal

Add a shared, externally-backed cache layer so product queries and other read-heavy operations are not limited to the process-local in-memory cache.

## Why Redis here

The app already contains an abstraction for cache access through `ICacheProvider` and an in-memory implementation: [src/ProjectMainApp/Project.Core/Caching/ICacheProvider.cs](../../src/ProjectMainApp/Project.Core/Caching/ICacheProvider.cs) and [src/ProjectMainApp/Project.Core/Caching/InMemoryCacheProvider.cs](../../src/ProjectMainApp/Project.Core/Caching/InMemoryCacheProvider.cs).

That memory cache is process-local only, so it is not shared across container app replicas. For a multi-instance deployment, Redis is the better fit for shared reads and TTL-based caching.

## What was added

### .NET wiring

The API now checks for a Redis connection string first and registers a Redis-backed cache provider when configured.

- If `Redis:ConnectionString` or `ConnectionStrings:Redis` is present, the app uses a Redis-backed implementation.
- Otherwise, it falls back to the existing in-memory cache provider for local dev and safer startup behavior.

The registration lives in [src/ProjectMainApp/Project.Endpoint/Program.cs](../../src/ProjectMainApp/Project.Endpoint/Program.cs).

### Terraform infrastructure

The dev environment creates an Azure Cache for Redis instance in the shared resource group.

The Redis resource is defined in [terraform/environments/dev/main.tf](../../terraform/environments/dev/main.tf).

It is exposed into the API container app as a secret named `redis-connection` and mapped to the app configuration `ConnectionStrings__Redis`.

## Recommended usage

Use Redis for:

- product list caching
- read-heavy catalog queries
- short-lived AI result caching
- any per-environment shared cache data that should survive across replicas

Keep Postgres as the authoritative transactional store.

## Notes

- This is intentionally a gradual integration: Redis is added without breaking the existing in-memory path.
- The fallback keeps local development working even when no Redis service is configured.
- In production, the connection string should come from a secure secret source rather than being committed to source control.

## Next steps

- Add a concrete `RedisCacheProvider` implementation that serializes values using JSON.
- Move product list caching and any AI result caching onto the Redis-backed provider.
- Add invalidation when product data changes so cached catalog entries refresh correctly.
