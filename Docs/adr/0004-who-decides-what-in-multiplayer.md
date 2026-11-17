# Who decides what: three authority tiers, and the shooter decides hits

ADR 0001 makes the host decide shared state. That alone would make every player action wait a round trip to the host (about 30–150 ms between friends on Steam). In a co-op game, players accept seeing *other* players slightly late, but not their *own* actions feeling sluggish. So every new mechanic picks one of three tiers, and hits are decided by the shooter's machine and only sanity-checked by the host. Nothing is predicted, re-simulated or rolled back.

## The tiers

1. **Only I control it** (my movement, camera, emotes, the position of something I carry alone): the owning player decides and sends the result. No delay for them; others see it slightly late, smoothed.
2. **Shared, but only I am using it right now** (an item I pick up, a lever I pull, a door I open): I get ownership, or my machine acts at once and undoes it if the host denies it (see "Instant actions with undo"). This tier never applies to bodies that push shared physics: things several players can push stay simulated by the host (ADR 0001).
3. **Shared and contested, or game-critical** (enemy AI, damage and health, loot, score, shared physics): the host decides. The acting player gets **instant local cosmetic feedback** (sound, animation, muzzle flash, tracer) and accepts the delay on the outcome. Feedback is never a guess at the outcome: no predicted damage numbers, no predicted deaths.

## Instant actions with undo (tier 2)

When a tier 2 action would feel laggy waiting for the host (picking up an item, pulling a lever, opening a door), the acting machine shows the result **immediately** and sends the request. The host then either confirms it, through the normal replicated state, or sends an **explicit denial that names what was denied**. On denial, or if no answer arrives within a timeout, the local preview is removed and the host's state shows again. The player who lost a race sees the item jump to the winner, which is rare and easy to read. Only the acting player's machine previews; everyone else sees only the host's result. Never use this for tier 3 outcomes such as damage, deaths, loot or money.

## Hits

For every weapon, the shooter's machine raycasts against what it sees and sends a hit claim (origin, direction, point, networked target). The host checks it against what it sees: the shot came from near the shooter, stays within range, lies on the claimed ray, isn't blocked by static world geometry, and the target is near the claimed point. If the claim passes, the host applies the outcome (damage, push, enemy reaction). Tolerances are lenient: the check catches impossible shots, not lag. Projectiles that fly visibly follow the same rule (amended 2026-11-16, see "Projectiles" below).

## Projectiles (amended 2026-11-16)

A projectile on a predictable arc (a thrown or fired object) is decided like a hitscan shot, not simulated by the host:
- The shooter's machine flies its own copy at once and sends the launch (origin, velocity, the time it was fired). The host checks origin, speed and cooldown, remembers the shot, and sends it to everyone else, whose copies run faster until they have caught up with the shooter's timeline.
- Only the shooter's copy decides hits. When it touches a networked object, the shooter sends a claim (shot, flight time, point, target). The host checks the point lies on the remembered arc, that no static wall was in the way, and that the target is near; then it applies the hit and tells everyone, so every copy ends at the same spot.
- Projectiles are local objects on every machine, never NetworkObjects. Hits on static world need no message: every copy computes the same arc.

Why: a host-simulated projectile appears one round trip late for the shooter, and judges moving targets and players stepping in the way by positions the shooter never saw. A shot that hit on the shooter's screen then misses, the same problem the hitscan rule solves. Projectiles that bounce off moving things unpredictably, or that carry state late joiners must see (an object left lying as a pickup), would still be host-simulated tier 3 objects.

## Considered Options

- **The host re-raycasts the shot itself**: the shooter hits a target on screen, and the host, seeing the target ~100 ms later, misses it. Shots feel unreliable as soon as there is lag.
- **The host rewinds targets to what the shooter saw (lag compensation)**: accurate and cheat-resistant, but NGO has no built-in support. It needs a position history for every hittable thing, and the cheat resistance it buys doesn't matter in a game among friends.
- **Client prediction with server reconciliation for movement**: what competitive shooters do. Each mechanic needs its own prediction and rollback code, and it fights shared physics. Not worth it for co-op.

## Consequences

- A modified client could claim hits it didn't make, within the tolerances. Accepted: sessions are among friends.
- Hits can be made more competitive later without touching weapons' owner side: swap the host's claim check for a rewind-based check. Making movement server-authoritative with prediction would be a rewrite of the player and every tier 1 mechanic.
