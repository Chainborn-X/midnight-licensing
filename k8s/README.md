# Kubernetes Deployment

This directory contains Kubernetes manifests for deploying the Chainborn Licensing Validator with persistent cache storage.

## Quick Start

```bash
# Deploy all resources
kubectl apply -f validator-deployment.yaml

# Check deployment status
kubectl get all -n chainborn

# View logs
kubectl logs -f deployment/validator -n chainborn

# Test the validator
kubectl port-forward svc/validator-service 8080:80 -n chainborn
curl http://localhost:8080/health
```

## What's Included

The `validator-deployment.yaml` file contains:

- **Namespace**: Isolates Chainborn resources in the `chainborn` namespace
- **PersistentVolumeClaim**: 10Gi storage for validation cache
- **ConfigMap**: License policy configuration
- **Deployment**: 3-replica validator deployment with persistent cache
- **Service**: ClusterIP service for internal access
- **HorizontalPodAutoscaler**: Auto-scaling based on CPU/memory

## Customization

### Storage Class

Update the storage class to match your cluster:

```yaml
spec:
  storageClassName: standard  # Change to: gp2, fast, nfs-client, etc.
```

### Replica Count

Adjust replicas based on your load:

```yaml
spec:
  replicas: 3  # Increase for higher load
```

### Access Mode

- Use `ReadWriteMany` for shared cache across multiple pods
- Use `ReadWriteOnce` for single-pod deployments

### Resource Limits

Adjust CPU and memory limits based on workload:

```yaml
resources:
  requests:
    cpu: 250m
    memory: 512Mi
  limits:
    cpu: 1000m
    memory: 1Gi
```

## Policy Configuration

### Option 1: Create ConfigMap from Files

```bash
kubectl create configmap validator-policies \
  --from-file=../policies/ \
  -n chainborn
```

### Option 2: Update Inline

Edit the ConfigMap section in `validator-deployment.yaml`:

```yaml
data:
  my-product.json: |
    {
      "productId": "my-product",
      "minimumTier": "enterprise",
      "cacheTTL": "01:00:00"
    }
```

## External Access

### LoadBalancer (Cloud Providers)

Change the Service type:

```yaml
spec:
  type: LoadBalancer
```

### Ingress (NGINX, Traefik, etc.)

Create an Ingress resource:

```yaml
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  name: validator-ingress
  namespace: chainborn
spec:
  rules:
  - host: validator.example.com
    http:
      paths:
      - path: /
        pathType: Prefix
        backend:
          service:
            name: validator-service
            port:
              number: 80
```

## Monitoring

### View Logs

```bash
# All pods
kubectl logs -f deployment/validator -n chainborn

# Specific pod
kubectl logs -f validator-<pod-id> -n chainborn

# Follow logs from all replicas
kubectl logs -f -l app=validator -n chainborn --all-containers=true
```

### Describe Resources

```bash
# Check PVC status
kubectl describe pvc validator-cache-pvc -n chainborn

# Check deployment
kubectl describe deployment validator -n chainborn

# Check pod issues
kubectl describe pod <pod-name> -n chainborn
```

### Check Cache Usage

```bash
kubectl exec -it deployment/validator -n chainborn -- df -h /var/chainborn/cache
```

## Troubleshooting

### PVC Not Binding

```bash
# Check PVC status
kubectl get pvc -n chainborn

# Check available PVs
kubectl get pv

# Describe PVC for events
kubectl describe pvc validator-cache-pvc -n chainborn
```

**Solution**: Verify your cluster has a storage provisioner and the storage class exists.

### Pods in CrashLoopBackOff

```bash
# Check logs
kubectl logs <pod-name> -n chainborn

# Check events
kubectl get events -n chainborn --sort-by='.lastTimestamp'
```

**Common causes**:
- Missing policy ConfigMap
- Permission issues on cache volume
- Invalid configuration

### Permission Denied on Cache

```bash
# Check volume permissions
kubectl exec -it <pod-name> -n chainborn -- ls -la /var/chainborn/
```

**Solution**: Ensure `fsGroup: 1654` is set in the deployment security context.

## Cleanup

### Remove Everything

```bash
kubectl delete -f validator-deployment.yaml
```

### Remove Including Persistent Data

```bash
kubectl delete -f validator-deployment.yaml
kubectl delete pvc validator-cache-pvc -n chainborn
```

## Further Reading

- [Runtime Cache Documentation](../docs/validator/runtime-cache.md) - Comprehensive cache configuration guide
- [Validator Architecture](../docs/validator/README.md) - Validator design and interfaces
- [Kubernetes Persistent Volumes](https://kubernetes.io/docs/concepts/storage/persistent-volumes/) - K8s storage concepts
