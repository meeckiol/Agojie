namespace Agojie.AI
{
    public enum EnemyState
    {
        Patrol,         // Following waypoints peacefully
        Suspicious,     // Saw/heard something, investigating last known position
        Chase,          // Target acquired, running toward target
        Attack,         // In melee range, performing attack
        Stunned,        // Staggered after receiving damage
        Dead            // Eliminated
    }
}
