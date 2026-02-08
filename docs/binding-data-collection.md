# Binding Data Collection

## Overview

The Chainborn Licensing Platform provides automatic environment identity data collection for license binding validation. This enables binding licenses to organizations, environments, or specific runtime contexts as required by the license policy.

## Purpose

Binding data is used to:
- **Prevent license sharing** by tying licenses to specific organizations or environments
- **Validate license authenticity** by matching runtime context against proof metadata
- **Enforce policy requirements** such as organization-bound or environment-bound licenses

## How It Works

### Automatic Collection

When the `BindingMode` in a license policy is set to anything other than `None`, the validator automatically collects binding data from the runtime environment during validation:

1. The `LicenseValidator` checks the policy's `BindingMode`
2. If binding is required and no binding data is provided in the `ValidationContext`, it automatically calls `IBindingDataCollector.CollectAsync()`
3. The collected data is used to populate `ValidationContext.BindingData`
4. This binding data is then included in the cache key and can be validated against the proof

### Manual Collection

Applications can also manually provide binding data in the `ValidationContext`:

```csharp
var bindingData = new Dictionary<string, string>
{
    ["organization_id"] = "acme-corp",
    ["environment"] = "production"
};

var context = new ValidationContext("my-product", bindingData);
var result = await validator.ValidateAsync(proof, context);
```

When binding data is provided manually, the automatic collection is skipped.

## Collected Data

The `BindingDataCollector` collects the following environment identity data:

### 1. Hostname
- **Key**: `hostname`
- **Source**: `Environment.MachineName`
- **Example**: `web-server-01`
- **Use Case**: Basic machine identification

### 2. Container ID
- **Key**: `container_id`
- **Sources**: 
  - `HOSTNAME` environment variable (if it looks like a container ID)
  - `/proc/self/cgroup` file (Linux containers)
- **Example**: `a1b2c3d4e5f6` (Docker container ID)
- **Use Case**: Container-specific licensing in Docker/Kubernetes

The collector recognizes several container ID patterns:
- Docker: `/docker/[container-id]`
- Docker scope: `/docker-[container-id].scope`
- Kubernetes/containerd: `/kubepods/.../[container-id]`

### 3. Kubernetes Namespace
- **Key**: `k8s_namespace`
- **Sources** (in order of preference):
  - `K8S_NAMESPACE` environment variable
  - `KUBERNETES_NAMESPACE` environment variable
- **Example**: `production`
- **Use Case**: Namespace-specific licensing in Kubernetes

### 4. Kubernetes Pod Name
- **Key**: `k8s_pod_name`
- **Sources** (in order of preference):
  - `K8S_POD_NAME` environment variable
  - `KUBERNETES_POD_NAME` environment variable
- **Example**: `my-app-pod-7d8f9b5c6-x4z2w`
- **Use Case**: Pod-specific licensing in Kubernetes

### 5. Custom Binding Variables
- **Key**: Derived from environment variable name (prefix removed, lowercased)
- **Source**: Environment variables prefixed with `CHAINBORN_BINDING_`
- **Example**: 
  - Env var: `CHAINBORN_BINDING_ORG_ID=acme-corp`
  - Collected as: `org_id` = `acme-corp`
- **Use Case**: Application-specific binding metadata

Custom binding variables are **case-insensitive** and can use any casing for the prefix:
- `CHAINBORN_BINDING_ORG_ID` → `org_id`
- `chainborn_binding_org_id` → `org_id`
- `Chainborn_Binding_Org_Id` → `org_id`

## Binding Modes

### None
- **Behavior**: No binding data is collected
- **Use Case**: Permissive licenses, development, or open-source scenarios
- **Example**:
```json
{
  "productId": "my-product",
  "bindingMode": "none",
  "cacheTtl": 86400,
  "revocationModel": "none",
  "version": "1.0.0"
}
```

### Organization
- **Behavior**: Binding data is collected automatically
- **Use Case**: B2B licenses sold per company/tenant
- **Validation**: The runtime organization identifier should match the proof's organization binding
- **Example**:
```json
{
  "productId": "my-product",
  "bindingMode": "organization",
  "cacheTtl": 43200,
  "revocationModel": "periodic-check",
  "version": "1.0.0"
}
```

### Environment
- **Behavior**: Binding data is collected automatically
- **Use Case**: Node-locked licenses, container-specific licensing
- **Validation**: The runtime environment fingerprint should match the proof's environment binding
- **Example**:
```json
{
  "productId": "my-product",
  "bindingMode": "environment",
  "cacheTtl": 3600,
  "revocationModel": "on-chain",
  "version": "1.0.0"
}
```

### Attestation
- **Behavior**: Binding data is collected automatically
- **Use Case**: High-security scenarios with TPM/SGX attestation
- **Validation**: Additional attestation verification beyond standard binding
- **Example**:
```json
{
  "productId": "my-product",
  "bindingMode": "attestation",
  "cacheTtl": 1800,
  "revocationModel": "on-chain",
  "version": "1.0.0"
}
```

## Configuration Examples

### Docker Container

Set environment variables in your Dockerfile or docker-compose.yml:

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY . .

# Custom binding data
ENV CHAINBORN_BINDING_ORG_ID=acme-corp
ENV CHAINBORN_BINDING_ENVIRONMENT=production

ENTRYPOINT ["dotnet", "MyApp.dll"]
```

### Kubernetes Deployment

Use the Downward API to expose pod/namespace information:

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: my-app
spec:
  template:
    spec:
      containers:
      - name: my-app
        image: my-app:latest
        env:
        # Kubernetes metadata via Downward API
        - name: K8S_NAMESPACE
          valueFrom:
            fieldRef:
              fieldPath: metadata.namespace
        - name: K8S_POD_NAME
          valueFrom:
            fieldRef:
              fieldPath: metadata.name
        # Custom binding data
        - name: CHAINBORN_BINDING_ORG_ID
          value: "acme-corp"
        - name: CHAINBORN_BINDING_CLUSTER
          value: "us-east-1"
```

### .NET Application

Manually provide binding data:

```csharp
var bindingData = new Dictionary<string, string>
{
    ["organization_id"] = GetOrganizationId(),
    ["deployment_region"] = "us-west-2"
};

var context = new ValidationContext(
    productId: "my-product",
    bindingData: bindingData
);

var result = await licenseValidator.ValidateAsync(proof, context);
```

## Testing Binding Data Collection

### Unit Tests

The `BindingDataCollectorTests` class provides comprehensive unit tests:

```bash
dotnet test --filter "FullyQualifiedName~BindingDataCollectorTests"
```

Tests cover:
- Hostname collection
- Container ID extraction from cgroup
- Kubernetes metadata collection
- Custom environment variable collection
- Case-insensitive prefix matching

### Integration Tests

The `BindingModeIntegrationTests` class tests end-to-end binding scenarios:

```bash
dotnet test --filter "FullyQualifiedName~BindingModeIntegrationTests"
```

Tests cover:
- Binding mode None (no collection)
- Binding mode Organization (automatic collection)
- Binding mode Environment (automatic collection)
- Manual binding data (no automatic collection)
- Custom environment variables
- Kubernetes environment variables

## Extensibility

### Custom Binding Data Collector

Applications can provide their own implementation of `IBindingDataCollector`:

```csharp
public class CustomBindingDataCollector : IBindingDataCollector
{
    public async Task<IReadOnlyDictionary<string, string>> CollectAsync(
        CancellationToken cancellationToken = default)
    {
        var data = new Dictionary<string, string>();
        
        // Add custom logic
        data["tenant_id"] = await GetTenantIdFromDatabase();
        data["region"] = await GetCloudRegion();
        
        return data;
    }
}
```

Register the custom collector:

```csharp
services.AddSingleton<IBindingDataCollector, CustomBindingDataCollector>();
```

### Combining with Default Collector

Extend the default collector:

```csharp
public class ExtendedBindingDataCollector : BindingDataCollector
{
    public ExtendedBindingDataCollector(ILogger<BindingDataCollector> logger)
        : base(logger)
    {
    }

    public override async Task<IReadOnlyDictionary<string, string>> CollectAsync(
        CancellationToken cancellationToken = default)
    {
        var baseData = await base.CollectAsync(cancellationToken);
        var extended = new Dictionary<string, string>(baseData);
        
        // Add additional data
        extended["custom_field"] = GetCustomValue();
        
        return extended;
    }
}
```

## Security Considerations

1. **Sensitive Data**: Do not include secrets or credentials in binding data
2. **Validation**: Always validate binding data against proof metadata on the server side
3. **Tampering**: Binding data can be manipulated by the client; rely on cryptographic proof verification
4. **Privacy**: Be mindful of PII in binding data, especially in shared/cloud environments

## Troubleshooting

### Binding Data Not Collected

**Symptom**: Binding data is empty or missing expected fields

**Solutions**:
- Ensure `BindingMode` is not `None` in the policy
- Check environment variables are set correctly
- Verify Kubernetes Downward API configuration
- Review logs for collection errors

### Container ID Not Detected

**Symptom**: `container_id` field is missing

**Solutions**:
- Verify running in a container environment
- Check `/proc/self/cgroup` is accessible (Linux only)
- Set `HOSTNAME` environment variable explicitly if needed

### Custom Variables Not Collected

**Symptom**: Custom binding variables not appearing

**Solutions**:
- Ensure variables use `CHAINBORN_BINDING_` prefix (case-insensitive)
- Check variable values are not empty or whitespace
- Verify environment variables are accessible to the process

## Related Documentation

- [Policy Schema](policy-schema.md) - Detailed policy configuration
- [Architecture](architecture.md) - System architecture overview
- [Validator README](validator/README.md) - Validator usage guide

## Support

For questions or issues:
- Open an issue on [GitHub](https://github.com/Chainborn-X/midnight-licensing/issues)
- Review [policy-schema.md](policy-schema.md) for binding mode details
- Check test examples in `BindingDataCollectorTests.cs` and `BindingModeIntegrationTests.cs`
