using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// Tracks encounter enemy totals and references to living enemies
// Visual systems such as Hunt Mode reads from this class instead of scanning the scene
public class EnemyTracker : MonoBehaviour {
	[Header("Events")]
	[Tooltip("Invoked once when the planned number of enemies has been killed.")]
	public UnityEvent onAllEnemiesDead;

	public int TotalPlanned { get; private set; }
	public int SpawnedSoFar { get; private set; }
	public int AliveNow { get; private set; }
	public int KilledSoFar { get; private set; }

	// Remaining enemies based on the planned encounter total, not just currently spawned enemies
	public int RemainingToKill => Mathf.Max(0, TotalPlanned - KilledSoFar);
	public int RemainingEnemies => RemainingToKill;

	// Read-only view for systems that need living enemy references without allocating each frame
	public IReadOnlyList<EnemyCombat> AliveEnemies => aliveEnemies;

	// Events for gameplay systems that should react without polling
	public event Action<EnemyCombat> OnEnemyRegistered;
	public event Action<EnemyCombat> OnEnemyUnregistered;
	public event Action<int> OnRemainingEnemiesChanged;
	public event Action<int> OnAliveEnemyCountChanged;
	public event Action OnAllEnemiesDead;

	private readonly List<EnemyCombat> aliveEnemies = new List<EnemyCombat>(32);
	private bool triggered = false;

	// Sets the total enemies remaining in the scene
	// Usually called by SceneEnemyTrackerInitializer after it sums all GenericSpawner wave entries
	public void SetSceneTotal(int totalPlanned) {
		TotalPlanned = Mathf.Max(0, totalPlanned);
		SpawnedSoFar = 0;
		AliveNow = 0;
		KilledSoFar = 0;
		triggered = TotalPlanned == 0;
		aliveEnemies.Clear();
		NotifyCountsChanged();
	}

	public void ResetTracker() {
		TotalPlanned = 0;
		SpawnedSoFar = 0;
		AliveNow = 0;
		KilledSoFar = 0;
		triggered = false;
		aliveEnemies.Clear();
		NotifyCountsChanged();
	}

	// Spawn entry point for callers that only update counts
	public void EnemySpawned() {
		EnemySpawned(null);
	}

	// Called by spawners when an enemy becomes active
	// Passing the EnemyCombat reference lets systems avoid FindObjectsOfType during play
	public void EnemySpawned(EnemyCombat enemy) {
		SpawnedSoFar++;

		if (enemy != null) {
			RegisterEnemyInternal(enemy);
		}
		else {
			AliveNow++;
			NotifyAliveChanged();
		}

		EnsureTotalCoversKnownEnemies();
		NotifyRemainingChanged();
	}

	// For non-spawner systems or manually placed enemies
	// This only registers the living reference
	// Use EnemySpawned(enemy) when the spawn should increment SpawnedSoFar
	public void RegisterEnemy(EnemyCombat enemy) {
		if (enemy == null) {
			return;
		}

		RegisterEnemyInternal(enemy);
		EnsureTotalCoversKnownEnemies();
		NotifyRemainingChanged();
	}

	// Death entry point for callers that only update counts
	public void EnemyDied() {
		EnemyDied(null);
	}

	// Called by enemies when they die
	// Removes the living reference + updates counts then checks completion
	public void EnemyDied(EnemyCombat enemy) {
		if (enemy != null) {
			UnregisterEnemyInternal(enemy);
		}
		else if (AliveNow > 0) {
			AliveNow--;
			NotifyAliveChanged();
		}

		KilledSoFar++;
		EnsureTotalCoversKnownEnemies();
		NotifyRemainingChanged();

		if (triggered == false && RemainingToKill <= 0) {
			triggered = true;
			onAllEnemiesDead?.Invoke();
			OnAllEnemiesDead?.Invoke();
		}
	}

	// Used when something gets returned to pool without dying first
	// Example: when the player dies and respawns at a checkpoint
	public void EnemyDespawnedAlive() {
		EnemyDespawnedAlive(null);
	}

	public void EnemyDespawnedAlive(EnemyCombat enemy) {
		if (enemy != null) {
			UnregisterEnemyInternal(enemy);
			return;
		}

		if (AliveNow > 0) {
			AliveNow--;
			NotifyAliveChanged();
		}
	}

	// Returns the internal read-only list of living enemies
	// NOTE: Do not modify the returned list
	// Use FillAliveEnemies instead
	public IReadOnlyList<EnemyCombat> GetAliveEnemies() {
		PruneInvalidAliveEnemies();
		return aliveEnemies;
	}

	// Copies living enemies into an existing list so callers can avoid allocations
	public void FillAliveEnemies(List<EnemyCombat> results) {
		if (results == null) {
			return;
		}

		PruneInvalidAliveEnemies();
		results.Clear();

		for (int i = 0; i < aliveEnemies.Count; i++) {
			EnemyCombat enemy = aliveEnemies[i];
			if (enemy != null && enemy.isActiveAndEnabled && enemy.HasReportedDeath == false) {
				results.Add(enemy);
			}
		}
	}

	public bool ContainsEnemy(EnemyCombat enemy) {
		return enemy != null && aliveEnemies.Contains(enemy);
	}

	private void RegisterEnemyInternal(EnemyCombat enemy) {
		if (enemy == null || aliveEnemies.Contains(enemy)) {
			return;
		}

		aliveEnemies.Add(enemy);
		AliveNow = aliveEnemies.Count;
		OnEnemyRegistered?.Invoke(enemy);
		NotifyAliveChanged();
	}

	private void UnregisterEnemyInternal(EnemyCombat enemy) {
		if (enemy == null) {
			return;
		}

		if (aliveEnemies.Remove(enemy)) {
			AliveNow = aliveEnemies.Count;
			OnEnemyUnregistered?.Invoke(enemy);
			NotifyAliveChanged();
		}
		else if (AliveNow > 0) {
			// Fallback for older enemies that were counted but never registered by reference
			AliveNow--;
			NotifyAliveChanged();
		}
	}

	private void PruneInvalidAliveEnemies() {
		bool changed = false;

		for (int i = aliveEnemies.Count - 1; i >= 0; i--) {
			EnemyCombat enemy = aliveEnemies[i];
			if (enemy == null || enemy.isActiveAndEnabled == false || enemy.HasReportedDeath) {
				aliveEnemies.RemoveAt(i);
				changed = true;
			}
		}

		if (changed) {
			AliveNow = aliveEnemies.Count;
			NotifyAliveChanged();
		}
	}

	// This helps keep the tracker useful for manually spawned or auto-spawned enemies even when
	// SceneEnemyTrackerInitializer did not precompute a wave total
	private void EnsureTotalCoversKnownEnemies() {
		int knownEnemies = KilledSoFar + AliveNow;
		if (TotalPlanned < knownEnemies) {
			TotalPlanned = knownEnemies;
		}
	}

	private void NotifyCountsChanged() {
		NotifyAliveChanged();
		NotifyRemainingChanged();
	}

	private void NotifyAliveChanged() {
		OnAliveEnemyCountChanged?.Invoke(AliveNow);
	}

	private void NotifyRemainingChanged() {
		OnRemainingEnemiesChanged?.Invoke(RemainingEnemies);
	}

	//// TODO: Also use this with a debug spawner for enemies
	//public void EnemySpawned() {
	//	SpawnedSoFar++;
	//	AliveNow++;
	//}

	//// Called by enemies when they die
	//// Updates enemy debug UI overlay accordingly
	//public void EnemyDied() {
	//	if (AliveNow > 0) {
	//		AliveNow--;
	//	}

	//	KilledSoFar++;

	//	if (triggered == false && RemainingToKill <= 0) {
	//		triggered = true;
	//		onAllEnemiesDead?.Invoke();
	//	}
	//}

	//// Used when something gets returned to pool without dying first
	//// e.g player dies and respawns at a checkpoint
	//public void EnemyDespawnedAlive() {
	//	if (AliveNow > 0) {
	//		AliveNow--;
	//	}
	//}
}