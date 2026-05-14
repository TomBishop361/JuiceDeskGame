using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// EnemyTracker is the single source of truth for enemy counts in an encounter
// It tracks:
// - how many enemies are planned for the level/encounter
// - how many have spawned
// - how many are alive right now
// - how many have died
// - references to currently alive enemies
// Hunt Mode and other systems should read from this tracker instead of scanning the scene
public class EnemyTracker : MonoBehaviour {
	[Header("Events")]
	[Tooltip("Invoked once when the planned number of enemies has been killed.")]
	public UnityEvent onAllEnemiesDead;

	[Tooltip("Invoked once when the first enemy spawns in this tracker. Useful for alarms, music escalation, or lockdowns.")]
	public UnityEvent onFirstEnemySpawned;

	[Tooltip("Invoked every time an enemy spawns in this tracker.")]
	public UnityEvent onEnemySpawned;

	// Total number of enemies expected in this level/encounter
	// Usually set by SceneEnemyTrackerInitializer
	public int TotalPlanned { get; private set; }

	// Number of enemies that have spawned so far
	public int SpawnedSoFar { get; private set; }

	// Number of enemies currently alive and active
	public int AliveNow { get; private set; }

	// Number of enemies that have reported death
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
	public event Action<EnemyCombat> OnEnemySpawned;
	public event Action<EnemyCombat> OnFirstEnemySpawned;

	// Internal reusable list of living enemies
	// Hunt Mode reads from this so it does not need FindObjectsOfType during gameplay
	private readonly List<EnemyCombat> aliveEnemies = new List<EnemyCombat>(32);

	// Prevents onAllEnemiesDead from firing more than once
	private bool triggered = false;

	// Prevents first-spawn systems such as alarms/music lockdowns from triggering more than once per tracker reset
	private bool firstEnemySpawnedTriggered = false;

	// Called by SceneEnemyTrackerInitializer when the level/encounter starts
	// This sets the planned number of enemies before they are all spawned
	public void SetSceneTotal(int totalPlanned) {
		TotalPlanned = Mathf.Max(0, totalPlanned);

		SpawnedSoFar = 0;
		AliveNow = 0;
		KilledSoFar = 0;

		// If there are zero planned enemies, completion is already considered triggered
		triggered = TotalPlanned == 0;
		firstEnemySpawnedTriggered = false;

		aliveEnemies.Clear();

		NotifyCountsChanged();
	}

	// Fully resets the tracker
	// Useful for restarting an encounter, debug tools, or scene reloads
	public void ResetTracker() {
		TotalPlanned = 0;
		SpawnedSoFar = 0;
		AliveNow = 0;
		KilledSoFar = 0;

		triggered = false;
		firstEnemySpawnedTriggered = false;

		aliveEnemies.Clear();

		NotifyCountsChanged();
	}

	// Spawn entry point for callers that only update counts
	// This does not register an enemy reference
	public void EnemySpawned() {
		EnemySpawned(null);
	}

	// Called by spawners when an enemy becomes active
	// Passing the EnemyCombat reference allows systems like Hunt Mode and Alarm Mode to know
	// exactly which enemies are alive without scene searches
	public void EnemySpawned(EnemyCombat enemy) {
		SpawnedSoFar++;

		// Fire first-spawn events before normal spawn processing
		// This is mainly for encounter-wide systems such as alarms, music escalation, and lockdown doors
		if (firstEnemySpawnedTriggered == false) {
			firstEnemySpawnedTriggered = true;

			onFirstEnemySpawned?.Invoke();
			OnFirstEnemySpawned?.Invoke(enemy);
		}

		onEnemySpawned?.Invoke();
		OnEnemySpawned?.Invoke(enemy);

		if (enemy != null) {
			RegisterEnemyInternal(enemy);
		}
		else {
			// Fallback path for callers that do not pass a reference
			AliveNow++;
			NotifyAliveChanged();
		}

		// Safety for manually spawned enemies or encounters without a precomputed total
		EnsureTotalCoversKnownEnemies();

		NotifyRemainingChanged();
	}

	// Registers a living enemy reference without increasing SpawnedSoFar
	// Use this for manually placed enemies or systems that only need to tell the tracker "this enemy is alive"
	// Use EnemySpawned(enemy if the spawn count should also increase.
	public void RegisterEnemy(EnemyCombat enemy) {
		if (enemy == null) {
			return;
		}

		RegisterEnemyInternal(enemy);

		EnsureTotalCoversKnownEnemies();
		NotifyRemainingChanged();
	}

	// Death entry point for callers that only update counts
	// This does not remove a specific enemy reference
	public void EnemyDied() {
		EnemyDied(null);
	}

	// Called by enemies when they die
	// This:
	// - removes the enemy from the alive list
	// - updates death count
	// - updates remaining count
	// - fires completion events if all planned enemies are dead
	public void EnemyDied(EnemyCombat enemy) {
		if (enemy != null) {
			UnregisterEnemyInternal(enemy);
		}
		else if (AliveNow > 0) {
			// Fallback path for callers that do not pass a reference
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

	// Despawn entry point for callers
	// Use this when an enemy disappears without dying
	// Example: returned to pool after player death/checkpoint reset
	public void EnemyDespawnedAlive() {
		EnemyDespawnedAlive(null);
	}

	// Called when a living enemy is removed without dying
	// This removes the enemy from the alive list but does not increase KilledSoFar
	public void EnemyDespawnedAlive(EnemyCombat enemy) {
		if (enemy != null) {
			UnregisterEnemyInternal(enemy);
			return;
		}

		// Fallback path for callers that do not pass a reference
		if (AliveNow > 0) {
			AliveNow--;
			NotifyAliveChanged();
		}
	}

	// Returns the internal read-only list of living enemies
	// Important:
	// Do not modify the returned list
	// If another system needs its own list, use FillAliveEnemies instead
	public IReadOnlyList<EnemyCombat> GetAliveEnemies() {
		PruneInvalidAliveEnemies();
		return aliveEnemies;
	}

	// Copies living enemies into a caller-owned list so they can avoid allocations
	// This is useful because the caller can reuse the same list/buffer, which avoids GC issues during gameplay
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

	// Checks whether this tracker currently knows about a living enemy
	public bool ContainsEnemy(EnemyCombat enemy) {
		return enemy != null && aliveEnemies.Contains(enemy);
	}

	// Adds an enemy to the alive reference list
	private void RegisterEnemyInternal(EnemyCombat enemy) {
		if (enemy == null || aliveEnemies.Contains(enemy)) {
			return;
		}

		aliveEnemies.Add(enemy);

		AliveNow = aliveEnemies.Count;

		OnEnemyRegistered?.Invoke(enemy);
		NotifyAliveChanged();
	}

	// Removes an enemy from the alive reference list
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
			// Fallback for enemies that were counted but never registered by reference
			AliveNow--;
			NotifyAliveChanged();
		}
	}

	// Removes destroyed, disabled, or dead enemies from the alive list
	// This protects systems like Hunt Mode from redundent references,
	// especially when enemies are pooled or disabled unexpectedly
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

	// Keeps TotalPlanned large enough to cover enemies that were manually spawned
	// or registered without SceneEnemyTrackerInitializer precomputing a total
	private void EnsureTotalCoversKnownEnemies() {
		int knownEnemies = KilledSoFar + AliveNow;

		if (TotalPlanned < knownEnemies) {
			TotalPlanned = knownEnemies;
		}
	}

	// Sends both alive-count and remaining-count updates
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
}