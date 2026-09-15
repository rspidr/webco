# WEBCO
a harmony patch for RecRoom dodgeball networking that fixes catch and hit race conditions through buffered event reconciliation.
## OVERVIEW
in networked dodgeball games, latency can cause a hit event (`RpcMasterRequestPlayerOut`) to arrive before or simultaneously with a successful catch event (`RpcMasterRequestPlayerCatch` / `RpcOnPlayerCatch`). this causes players who caught the ball to still get out by mistake.
`webco` intercepts out requests, temporarily queues them during a brief grace window, and reconciles them against incoming catch events to ensure accurate game state resolution.
## FEATURES
- **grace period queue**: buffers incoming out requests for a configurable duration before execution.
- **catch reconciliation**: automatically cancels pending out events if a matching catch is detected within the window.
- **unmatched out pass-through**: immediately resolves and executes pending hits from different throwers.
- **history tracking**: maintains a short rolling buffer of recent catches to filter late hit packets.
- **replay safety**: uses internal flags to prevent recursive harmony interception during event replay.
## HOW IT WORKS
1. **out interception**: when `RpcMasterRequestPlayerOut` is called, the request is stored in a pending list with a timestamp.
2. **catch verification**: 
   - if a catch record already exists for the catcher and thrower, the out request is discarded.
   - if a new catch event arrives, any pending out request from the same thrower is cancelled.
3. **deferred replay**: during the manager update loop, any pending out requests that exceed the grace period without being cancelled are replayed to the game engine.
## CONFIGURATION
the following parameters can be adjusted directly in `catchoutfixpatch`:
```csharp
// time in milliseconds to hold pending out requests before confirming them
public static float graceperiodms = 175f;
// time in milliseconds to keep recent catch records in memory
public static float catchhistoryms = 350f;
