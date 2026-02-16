// Implemented in: TBD
// Accessed inside: Enemy Sensor Scripts
public interface IHealthSettings {
	float MaxHealth { get; }
	// NOTE: Can use for retreating (enemies) | Can use for low hp HUD (player)
	float LowHealthThreshold { get; }
}