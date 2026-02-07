# Proof Envelope

## Overview

The **Proof Envelope** is a portable, serializable format for storing and transferring license proofs. It wraps a `LicenseProof` with versioning and metadata, making it suitable for file storage, environment variables, and Kubernetes secrets.

## Format

The proof envelope is a JSON structure with the following schema:

```json
{
  "proof": {
    "proofBytes": "<base64-encoded proof>",
    "verificationKeyBytes": "<base64-encoded verification key>",
    "productId": "your-product-id",
    "challenge": {
      "nonce": "random-nonce-string",
      "issuedAt": "2026-02-07T10:00:00Z",
      "expiresAt": "2026-02-07T11:00:00Z"
    },
    "metadata": {
      "custom-key": "custom-value"
    }
  },
  "version": "1.0",
  "metadata": {
    "source": "customer-wallet",
    "timestamp": "2026-02-07T10:00:00Z"
  }
}
```

### Fields

- **`proof`**: The core license proof object
  - **`proofBytes`**: Base64-encoded ZK proof bytes
  - **`verificationKeyBytes`**: Base64-encoded verification key
  - **`productId`**: Product identifier (must match the validation context)
  - **`challenge`**: Anti-replay challenge
    - **`nonce`**: Unique random string to prevent replay attacks
    - **`issuedAt`**: When the challenge was issued
    - **`expiresAt`**: When the challenge expires
  - **`metadata`**: Optional proof-specific metadata
- **`version`**: Envelope format version (currently "1.0")
- **`metadata`**: Optional envelope-level metadata

## Loading Sources

The `ProofLoader` supports multiple sources for loading proof envelopes, checked in priority order:

### 1. Environment Variable (Highest Priority)

**Variable**: `LICENSE_PROOF`

The proof envelope JSON is base64-encoded and stored in an environment variable:

```bash
# Generate base64-encoded proof
export LICENSE_PROOF=$(cat proof.json | base64 -w 0)

# Run application
docker run -e LICENSE_PROOF="$LICENSE_PROOF" myapp:latest
```

**Use case**: Simple deployments, CI/CD pipelines, testing

### 2. File Path via Environment Variable

**Variable**: `LICENSE_PROOF_FILE`

Points to a file containing the proof envelope JSON:

```bash
export LICENSE_PROOF_FILE=/mnt/secrets/proof.json
docker run -e LICENSE_PROOF_FILE=/mnt/secrets/proof.json \
  -v /host/secrets:/mnt/secrets:ro \
  myapp:latest
```

**Use case**: Docker volumes, mounted secrets, custom paths

### 3. Default Path (Fallback)

**Path**: `/etc/chainborn/proof.json`

If no environment variables are set, the loader checks this default location:

```bash
docker run -v /host/proof.json:/etc/chainborn/proof.json:ro myapp:latest
```

**Use case**: Standardized deployments, convention over configuration

## Kubernetes Integration

### Using Secrets

Create a Kubernetes Secret from a proof file:

```bash
kubectl create secret generic license-proof \
  --from-file=proof.json=./proof.json
```

Mount as a file:

```yaml
apiVersion: v1
kind: Pod
metadata:
  name: myapp
spec:
  containers:
  - name: app
    image: myapp:latest
    env:
    - name: LICENSE_PROOF_FILE
      value: /mnt/secrets/proof.json
    volumeMounts:
    - name: proof-secret
      mountPath: /mnt/secrets
      readOnly: true
  volumes:
  - name: proof-secret
    secret:
      secretName: license-proof
      items:
      - key: proof.json
        path: proof.json
```

Or inject as environment variable:

```yaml
apiVersion: v1
kind: Pod
metadata:
  name: myapp
spec:
  containers:
  - name: app
    image: myapp:latest
    env:
    - name: LICENSE_PROOF
      valueFrom:
        secretKeyRef:
          name: license-proof-base64
          key: proof
```

### Using ConfigMaps (Not Recommended for Production)

For development/testing only (secrets should be used for production):

```bash
kubectl create configmap license-proof \
  --from-file=proof.json=./proof.json
```

## Docker Compose Examples

### Volume Mount

```yaml
version: '3.8'
services:
  app:
    image: myapp:latest
    environment:
      - LICENSE_PROOF_FILE=/etc/chainborn/proof.json
    volumes:
      - ./proof.json:/etc/chainborn/proof.json:ro
```

### Environment Variable

```yaml
version: '3.8'
services:
  app:
    image: myapp:latest
    environment:
      - LICENSE_PROOF=${LICENSE_PROOF}
```

Then start with:
```bash
export LICENSE_PROOF=$(cat proof.json | base64 -w 0)
docker-compose up
```

### Using Secrets (Docker Swarm)

```yaml
version: '3.8'
services:
  app:
    image: myapp:latest
    environment:
      - LICENSE_PROOF_FILE=/run/secrets/proof
    secrets:
      - proof

secrets:
  proof:
    file: ./proof.json
```

## Error Handling

The `ProofLoader` provides clear error messages for common scenarios:

### No Proof Found
```
No proof envelope found. Checked: 
1) LICENSE_PROOF environment variable, 
2) LICENSE_PROOF_FILE environment variable, 
3) /etc/chainborn/proof.json
```

### Invalid Base64
```
Failed to decode base64 from LICENSE_PROOF environment variable
```

### File Not Found
```
Proof file not found: /custom/path/proof.json
```

### Invalid JSON
```
Failed to deserialize proof envelope JSON
```

### Missing Required Fields
```
Proof envelope is missing 'Proof' property
```

## Future Extensions

The proof loader system is designed to be extensible. Future versions may support:

### Custom Proof Sources
- **Cloud Vaults**: AWS Secrets Manager, Azure Key Vault, Google Secret Manager
- **HTTP Endpoints**: REST APIs for centralized proof distribution
- **Database**: PostgreSQL, Redis, or other storage backends

### Plugin Architecture
```csharp
public interface IProofSource
{
    Task<ProofEnvelope?> TryLoadAsync(CancellationToken cancellationToken);
    int Priority { get; }
}
```

Plugins could be registered via DI:
```csharp
services.AddLicenseValidation(options =>
{
    options.ProofSources.Add<AzureKeyVaultProofSource>();
    options.ProofSources.Add<HttpProofSource>();
});
```

### Schema Validation
Future versions may include JSON Schema validation to ensure proof envelopes conform to the canonical format before deserialization.

## Best Practices

1. **Use Kubernetes Secrets for Production**: Never store proofs in ConfigMaps or plain environment variables in production
2. **Read-Only Mounts**: Always mount proof files as read-only to prevent tampering
3. **Rotate Proofs Regularly**: Generate fresh proofs with new challenges periodically
4. **Monitor Expiration**: Ensure proof challenges don't expire during critical operations
5. **Secure Storage**: Protect proof files on disk with appropriate permissions (e.g., 0400)
6. **Audit Access**: Log proof loading attempts and failures for security auditing

## Related

- [Architecture](architecture.md) - Overall system architecture
- [Policy Schema](policy-schema.md) - License policy configuration
- [Validator README](validator/README.md) - License validator documentation
