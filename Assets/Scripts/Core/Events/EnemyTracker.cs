using UnityEngine;
using UnityEngine.Events;

public class EnemyTracker : MonoBehaviour {
	public int TotalPlanned { get; private set; }
	public int SpawnedSoFar { get; private set; }
	public int AliveNow { get; private set; }
	public int KilledSoFar { get; private set; }
	public int RemainingToKill => Mathf.Max(0, TotalPlanned - KilledSoFar);

	public UnityEvent onAllEnemiesDead;

	private bool triggered = false;

	// Sets the total enemies remaining in the scene
	public void SetSceneTotal(int totalPlanned) {
		TotalPlanned = Mathf.Max(0, totalPlanned);
		SpawnedSoFar = 0;
		AliveNow = 0;
		KilledSoFar = 0;
		triggered = TotalPlanned == 0;
	}

	public void ResetTracker() {
		TotalPlanned = 0;
		SpawnedSoFar = 0;
		AliveNow = 0;
		KilledSoFar = 0;
		triggered = false;
	}

	// TODO: Also use this with a debug spawner for enemies
	public void EnemySpawned() {
		SpawnedSoFar++;
		AliveNow++;
	}

	// Called by enemies when they die
	// Updates enemy debug UI overlay accordingly
	public void EnemyDied() {
		if (AliveNow > 0) {
			AliveNow--;
		}

		KilledSoFar++;

		if (triggered == false && RemainingToKill <= 0) {
			triggered = true;
			onAllEnemiesDead?.Invoke();
		}
	}

	// Used when something gets returned to pool without dying first
	// e.g player dies and respawns at a checkpoint
	public void EnemyDespawnedAlive() {
		if (AliveNow > 0) {
			AliveNow--;
		}
	}
}