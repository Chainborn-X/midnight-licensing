# Runtime Cache Persistence

## Overview

The Chainborn Licensing Validator uses an in-memory validation cache (`InMemoryValidationCache`) to store validation results and reduce expensive proof verification operations. The `CacheDirectory` option (`/var/chainborn/cache`) is reserved for future file-based cache implementations.

**Current State**: The default cache is in-memory and does not persist across restarts. To enable persistent caching, you must implement and register a custom `IValidationCache` that uses the `CacheDirectory` (e.g., a file-based or Redis-backed implementation).

**Future Work**: A file-based cache implementation is planned. Once available, proper volume configuration will be critical for:

- **Performance**: Avoid re-verifying proofs after container restarts
- **Resiliency**: Maintain validation state during rolling deployments
- **Audit trails**: Preserve validation history for compliance and debugging
- **Cost optimization**: Reduce computational overhead in high-traffic scenarios

By default, the cache directory is located at `/var/chainborn/cache` inside the container and can be configured via `LicenseValidationOptions.CacheDirectory`. The current implementation uses an in-memory cache that does not utilize this directory. Volume mounting prepares for future file-based cache implementations.

## Cache Directory Configuration

The validator accepts a configurable cache directory via `LicenseValidationOptions`:

```csharp
builder.Services.AddLicenseValidation(options =>
{
    options.CacheDirectory = "/var/chainborn/cache";
    options.PolicyDirectory = "/etc/chainborn/policies";
});
```

This can be overridden via configuration sources (environment variables, appsettings.json):

```json
{
  "Licensing": {
    "CacheDirectory": "/var/chainborn/cache"
  }
}
```

## Docker Compose Setup

### Basic Volume Mount

Create a `docker-compose.yml` in your project root:

```yaml
version: '3.8'

services:
  validator:
    image: chainborn/validator:latest
    ports:
      - "8080:8080"
    volumes:
      # Mount cache directory to persist validation results
      - ./cache:/var/chainborn/cache
      # Mount policy directory for license policies
      - ./policies:/etc/chainborn/policies
    environment:
      - ASPNETCORE_URLS=http://+:8080
      - Licensing__CacheDirectory=/var/chainborn/cache
      - Licensing__PolicyDirectory=/etc/chainborn/policies
```

### Named Volume (Recommended for Production)

For production deployments, use Docker named volumes for better lifecycle management:

```yaml
version: '3.8'

services:
  validator:
    image: chainborn/validator:latest
    ports:
      - "8080:8080"
    volumes:
      - validator-cache:/var/chainborn/cache
      - validator-policies:/etc/chainborn/policies:ro
    environment:
      - ASPNETCORE_URLS=http://+:8080
      - Licensing__CacheDirectory=/var/chainborn/cache
      - Licensing__PolicyDirectory=/etc/chainborn/policies

volumes:
  validator-cache:
    driver: local
  validator-policies:
    driver: local
```

### Running with Docker Compose

```bash
# Start services
docker-compose up -d

# View logs
docker-compose logs -f validator

# Stop services (cache persists)
docker-compose down

# Stop and remove volumes (cache cleared)
docker-compose down -v
```

## Kubernetes Setup

### Using Persistent Volume Claims (PVC)

For Kubernetes deployments, create a PVC and mount it to the validator pods:

#### 1. Create PersistentVolumeClaim

```yaml
apiVersion: v1
kind: PersistentVolumeClaim
metadata:
  name: validator-cache-pvc
  namespace: chainborn
spec:
  accessModes:
    - ReadWriteMany  # Allow multiple pods to share cache
  storageClassName: standard  # Use your cluster's storage class
  resources:
    requests:
      storage: 10Gi
```

**Note**: Use `ReadWriteOnce` for single-pod deployments or when your storage class doesn't support `ReadWriteMany`. Use `ReadWriteMany` only if running multiple validator replicas with a storage class that supports shared access (NFS, CephFS, Azure Files, etc.).

#### 2. Create Deployment with Volume Mount

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: validator
  namespace: chainborn
spec:
  replicas: 3
  selector:
    matchLabels:
      app: validator
  template:
    metadata:
      labels:
        app: validator
    spec:
      containers:
      - name: validator
        image: chainborn/validator:latest
        ports:
        - containerPort: 8080
        env:
        - name: ASPNETCORE_URLS
          value: "http://+:8080"
        - name: Licensing__CacheDirectory
          value: "/var/chainborn/cache"
        - name: Licensing__PolicyDirectory
          value: "/etc/chainborn/policies"
        volumeMounts:
        - name: cache-volume
          mountPath: /var/chainborn/cache
        - name: policy-volume
          mountPath: /etc/chainborn/policies
          readOnly: true
      volumes:
      - name: cache-volume
        persistentVolumeClaim:
          claimName: validator-cache-pvc
      - name: policy-volume
        configMap:
          name: validator-policies
```

#### 3. Create Service

```yaml
apiVersion: v1
kind: Service
metadata:
  name: validator-service
  namespace: chainborn
spec:
  selector:
    app: validator
  ports:
  - protocol: TCP
    port: 80
    targetPort: 8080
  type: ClusterIP
```

### Alternative: EmptyDir (Development Only)

For development or testing, you can use `emptyDir` volumes (non-persistent):

```yaml
volumes:
- name: cache-volume
  emptyDir: {}
```

**Warning**: `emptyDir` volumes are ephemeral and lost when pods are deleted. Not recommended for production.

### Deploying to Kubernetes

```bash
# Create namespace
kubectl create namespace chainborn

# Create ConfigMap for policies
kubectl create configmap validator-policies --from-file=policies/ -n chainborn

# Deploy validator (includes PVC, deployment, service)
kubectl apply -f k8s/validator-deployment.yaml

# Check status
kubectl get pods -n chainborn
kubectl logs -f deployment/validator -n chainborn

# Test the validator
kubectl port-forward svc/validator-service 8080:80 -n chainborn
curl http://localhost:8080/health
```

## Why Cache Persistence Matters

### Performance

Zero-knowledge proof verification is computationally expensive. The validation cache stores results for previously verified proofs, allowing:

- **Sub-millisecond response times** for cached validations vs. seconds for proof verification
- **Reduced CPU load** on validator pods
- **Better scalability** by serving more requests with fewer resources

### Resiliency

In production environments, container restarts are common due to:

- Rolling deployments
- Auto-scaling events
- Node maintenance
- Crash recovery

Without persistent cache:
- Every restart requires re-verifying all proofs
- Validation latency spikes during warm-up
- Increased load on proof verification systems

With persistent cache:
- Validation state survives restarts
- Consistent performance during deployments
- Faster recovery from failures

### Audit Trails

Cached validation results can serve as an audit log:

- Timestamp of each validation
- Product IDs and validation outcomes
- Nonce/challenge tracking for replay detection
- Historical validation patterns for compliance

**Note**: For long-term audit requirements, consider exporting cache data to a dedicated audit storage system.

## Troubleshooting

### Permission Denied Errors

**Symptom**: Container fails to write to cache directory

```
Error: Permission denied: /var/chainborn/cache
```

**Solution**: Ensure the volume has correct permissions

```bash
# For host-mounted volumes
chmod 777 ./cache

# Or match container user (usually UID 1654 for ASP.NET images)
chown -R 1654:1654 ./cache
chmod 755 ./cache
```

**Docker Compose**:
```yaml
services:
  validator:
    user: "1654:1654"  # Run as non-root user
    volumes:
      - ./cache:/var/chainborn/cache
```

**Kubernetes**:
```yaml
spec:
  securityContext:
    fsGroup: 1654  # Set group ownership for volumes
  containers:
  - name: validator
    securityContext:
      runAsUser: 1654
      runAsGroup: 1654
```

### Cache Not Persisting

**Symptom**: Cache resets after container restart

**Checklist**:

1. Verify volume is mounted correctly:
   ```bash
   docker inspect <container_id> | grep Mounts -A 10
   ```

2. Check that cache directory matches configuration:
   ```bash
   docker exec <container_id> printenv | grep Licensing__CacheDirectory
   ```

3. Ensure volume isn't being removed:
   ```bash
   # Don't use -v flag when stopping
   docker-compose down  # Good
   docker-compose down -v  # Bad - removes volumes
   ```

### Cache Growing Too Large

**Symptom**: Disk space exhaustion from unbounded cache growth

**Solutions**:

1. **Set volume size limits** (Kubernetes):
   ```yaml
   resources:
     requests:
       storage: 10Gi
   ```

2. **Implement cache cleanup strategy**:
   ```bash
   # Cron job to clean old cache files
   find /var/chainborn/cache -type f -mtime +7 -delete
   ```

3. **Configure cache TTL** in your policy files (example shows relevant field):
   ```json
   {
     "productId": "my-product",
     "version": "1.0.0",
     "bindingMode": "organization",
     "cacheTtl": 86400,
     "revocationModel": "none"
   }
   ```

4. **Monitor disk usage**:
   ```bash
   # Docker
   docker exec validator df -h /var/chainborn/cache
   
   # Kubernetes
   kubectl exec -it <pod-name> -- df -h /var/chainborn/cache
   ```

### Multiple Replicas Cache Coherence

**Symptom**: Inconsistent validation results across pods

**Issue**: Each pod has its own cache when using `ReadWriteOnce` volumes

**Solutions**:

1. **Use shared cache** (Kubernetes):
   - Set PVC to `ReadWriteMany` mode
   - Use a storage class supporting shared access (NFS, CephFS, Azure Files)

2. **Use distributed cache**:
   - Implement Redis-based cache instead of file-based
   - Register custom `IValidationCache` implementation

3. **Accept eventual consistency**:
   - For most scenarios, per-pod caching is acceptable
   - Validation results will converge as proofs are verified

### Volume Mount Issues in Kubernetes

**Symptom**: Pod stuck in `ContainerCreating` state

```bash
kubectl describe pod <pod-name>
# Events:
# Warning  FailedMount  PVC "validator-cache-pvc" not found
```

**Solution**: Verify PVC exists and is bound

```bash
kubectl get pvc -n chainborn
kubectl describe pvc validator-cache-pvc -n chainborn

# If unbound, check storage class
kubectl get storageclass
```

### Cache Corruption

**Symptom**: Validator crashes or returns errors after reading cache

**Recovery**:

```bash
# Docker Compose - Clear and recreate volume
docker-compose down
docker volume rm <project>_validator-cache
docker-compose up -d

# Kubernetes - Delete PVC and recreate
kubectl delete pvc validator-cache-pvc -n chainborn
kubectl apply -f k8s/validator-pvc.yaml
kubectl rollout restart deployment/validator -n chainborn
```

## Best Practices

1. **Always use persistent volumes in production** - Never rely on ephemeral storage for cache
2. **Set appropriate TTL values** - Balance between cache freshness and verification cost
3. **Monitor cache hit rates** - Track cache effectiveness with metrics/logging
4. **Plan for cache cleanup** - Implement retention policies to prevent unbounded growth
5. **Use readonly mounts for policies** - Prevent accidental modification of policy files
6. **Test disaster recovery** - Ensure application works correctly with empty cache
7. **Secure cache data** - Cache may contain sensitive validation metadata
8. **Regular backups** - Consider backing up cache for long-term audit requirements

## Security Considerations

- **File Permissions**: Restrict cache directory access to validator process only
- **Volume Encryption**: Use encrypted storage for sensitive validation data
- **Network Policies**: Limit access to cache volumes in multi-tenant clusters
- **Data Retention**: Comply with data retention regulations when caching validation results
- **Audit Logging**: Enable container logs to track cache access patterns

## Further Reading

- [Validator Architecture](README.md) - Core validator concepts and interfaces
- [License Policy Schema](../policy-schema.md) - Policy configuration format
- [Docker Documentation](https://docs.docker.com/storage/volumes/) - Docker volume management
- [Kubernetes Persistent Volumes](https://kubernetes.io/docs/concepts/storage/persistent-volumes/) - K8s storage concepts
