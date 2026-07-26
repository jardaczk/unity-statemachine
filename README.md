# Demondragon State Machine

Strongly typed finite state machine for gameplay code — players, enemies, traps, doors, anything with
behaviour that changes over time. Pure C# (`noEngineReferences`), so it unit-tests without a scene.

## Why this one

- **Instance-keyed states.** The same state class can appear several times in one machine (two waypoint
  waits with different durations, one sub-behaviour reused twice). Type-keyed machines cannot do this.
- **Deterministic priority.** Transitions are evaluated in registration order — the more specific edge is
  the one you register first, and it stays that way across builds.
- **Strongly typed.** `StateMachine<TContext>` / `IState<TContext>`: a state written for the player
  cannot be dropped into an enemy's machine. `State<TContext>` hands each state its typed context.
- **Three ways to drive it, freely mixed:** transitions, `ChangeState()` pushed from outside, and an
  `IStateSelector<TContext>` — the seam where a Utility AI / behaviour tree decides and the machine
  executes. A machine with no transitions at all is valid.
- **Interruptibility built in.** `MinDuration` / `CanExit` keep uninterruptible actions (attack, dodge,
  landing) from being cut short, and give AI-driven machines hysteresis so they stop thrashing between
  two similarly scored actions. `Forced()` overrides it for death, cutscenes, teleports.

## Usage

```csharp
class ChaseState : State<EnemyContext>
{
    protected override float MinDuration => 0.2f;          // commit for a moment
    public ChaseState(EnemyContext ctx) : base(ctx) { }
    public override void Enter() { base.Enter(); Context.Anim.Run(); }
    public override void FixedUpdate(float dt) => Context.Mover.MoveTowards(Context.Target);
}

var fsm = new StateMachineBuilder<EnemyContext>()
    .Initial(idle)
    .From(idle)
        .To(chase).When(() => ctx.SeesPlayer)
    .From(idle, patrol, chase)                              // one edge, several sources
        .To(attack).When(() => ctx.InAttackRange)
    .FromAny()
        .To(dead).When(() => ctx.Enemy.IsDead).Forced()     // interrupts anything
    .Build();

void Update()      => fsm.Tick(Time.deltaTime);
void FixedUpdate() => fsm.FixedTick(Time.fixedDeltaTime);
```

Plugging in a brain later (Utility AI) needs no change to the states:

```csharp
fsm.Selector = new UtilityBrain(considerations);   // implements IStateSelector<EnemyContext>
```

## API

| Member | Purpose |
| --- | --- |
| `Tick(dt)` | evaluate transitions → selector → update the running state |
| `FixedTick(dt)` | physics step of the running state (transitions are not evaluated here) |
| `ChangeState(state, force)` | push a state from outside; returns false when refused |
| `Current`, `CurrentStateName` | running state / its class name for debug overlays |
| `StateChanged` | `(previous, next)` event; `previous` is null on start |
| `Selector` | optional `IStateSelector<TContext>` brain |
| `Start()` | enter the initial state explicitly (otherwise the first tick does it) |

Builder: `Initial`, `From(params)`, `FromAny`, `To`, `When(cond, name)`, `Always`, `Forced`, `Build`.

## Tests

`Tests/Editor` — EditMode NUnit tests covering priority, instance keying, interruptibility, the selector
seam, multi-source and global transitions, lifecycle and builder validation.
