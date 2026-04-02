# Network Modules — Infrastructure Services

## Modules

| Module | Description |
|--------|-------------|
| [unique-id-generator](unique-id-generator/README.md) | UUID v4 and sequential ID generation |
| [task-id-generator](task-id-generator/README.md) | Task ID generation with module prefix |
| [subscription-manager](subscription-manager/README.md) | Topic-based pub/sub service |
| [runtime](runtime/README.md) | Module lifecycle management via Docker API |
| [virtual-machine](virtual-machine/README.md) | Sandboxed strategy execution |
| [container-registry](container-registry/README.md) | Container image and health tracking |
| [network-mask](network-mask/README.md) | URL registry and environment-aware endpoint resolution |

## Network Integration
All network modules are lightweight services that run as part of the Block Controller container or as standalone services on the `mls-network`.
