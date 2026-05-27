using System.Collections.Generic;
using UnityEngine;

namespace Game.AI {
	// Central coordinator for combat pressure
	// Enemy pressure controllers register their EnemyPressureAgent here so the director can assign jobs such as direct pressure,
	// cutoff, side pressure, ranged pressure, and backline harassment
	// The director also owns attack tokens, which limit how many enemies can start high-pressure attacks at the same time
	[DisallowMultipleComponent]
	public sealed class CombatPressureDirector : MonoBehaviour {
		// Scene-wide active director used by enemy pressure controllers
		// Only one director should exist in a combat scene
		public static CombatPressureDirector Active { get; private set; }

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void ResetStatics() {
			Active = null;
		}

		[Header("Target")]
		[Tooltip("Optional explicit player predictor reference. Leave empty to auto-find the player predictor.")]
		[SerializeField] private PlayerMotionPredictor playerPredictor;
		[Tooltip("Fallback tag used to find the player if no predictor reference is assigned.")]
		[SerializeField] private string playerTag = "Player";

		[Header("Assignment")]
		[Tooltip("How often pressure jobs are reassigned. Lower is snappier. Higher is more stable.")]
		[SerializeField] private float assignmentInterval = 0.35f;

		[Header("Attack Tokens")]
		[Tooltip("Maximum sword enemies allowed to swing at the same time.")]
		[SerializeField] private int maxSwordSwings = 2;
		[Tooltip("Maximum sword enemies allowed to lunge at the same time. Keep this at 1 for fairness.")]
		[SerializeField] private int maxSwordLunges = 1;
		[Tooltip("Maximum drones allowed to start firing at the same time.")]
		[SerializeField] private int maxDroneShots = 2;
		[Tooltip("Maximum shield enemies allowed to suppress with minigun at the same time.")]
		[SerializeField] private int maxShieldMiniguns = 1;
		[Tooltip("Maximum shield enemies allowed to slam at the same time.")]
		[SerializeField] private int maxShieldSlams = 1;

		// All registered pressure agents, regardless of enemy type
		private readonly List<EnemyPressureAgent> agents = new List<EnemyPressureAgent>(32);

		// Active attack token reservations
		// Each token hold records which agent owns a token, what kind it is, and when it should expire automatically
		private readonly List<TokenHold> tokenHolds = new List<TokenHold>(16);

		// Temporary grouped lists rebuilt during assignment
		// Keeping these as fields avoids allocating new lists every reassignment tick
		private readonly List<EnemyPressureAgent> swords = new List<EnemyPressureAgent>(12);
		private readonly List<EnemyPressureAgent> drones = new List<EnemyPressureAgent>(12);
		private readonly List<EnemyPressureAgent> shields = new List<EnemyPressureAgent>(4);

		// Next time the director should rebuild pressure jobs
		// Starts at negative infinity so the first Update() assigns immediately
		private float nextAssignmentTime = -Mathf.Infinity;

		// Public access to the player predictor and currently registered agents
		// ResolvePredictor finds/creates the predictor if it was not manually assigned
		public PlayerMotionPredictor PlayerPredictor => ResolvePredictor();
		public IReadOnlyList<EnemyPressureAgent> Agents => agents;

		// Represents one temporary permission for an enemy to begin a specific attack type
		// Tokens are intentionally time-limited so a destroyed/disabled enemy cannot block the system forever
		private struct TokenHold {
			public EnemyPressureAgent Agent;
			public PressureAttackTokenType Type;
			public float ExpireTime;
		}

		private void Awake() {
			if (Active != null && Active != this) {
				Debug.LogWarning($"Multiple {nameof(CombatPressureDirector)} instances found. Only one should exist per combat scene.", this);
			}

			// Make this director globally available and resolve the player predictor early if possible
			Active = this;
			ResolvePredictor();
		}

		private void OnEnable() {
			// Reassert this director as active when the GameObject is enabled
			Active = this;
		}

		private void OnDisable() {
			// Only clear Active if this exact director is the current active instance
			if (Active == this) {
				Active = null;
			}
		}

		private void Update() {
			// Remove redundant token reservations before checking assignment state
			ClearExpiredTokens();

			// Reassign jobs on a fixed interval instead of every frame to avoid jittery role changes
			if (Time.time >= nextAssignmentTime) {
				AssignJobs();
				nextAssignmentTime = Time.time + Mathf.Max(0.05f, assignmentInterval);
			}
		}

		public void Register(EnemyPressureAgent agent) {
			// Ignore nulls and duplicate registrations
			if (agent == null || agents.Contains(agent)) {
				return;
			}

			// Add the enemy to the managed list and force a reassignment on the next Update()
			agents.Add(agent);
			nextAssignmentTime = -Mathf.Infinity;
		}

		public void Unregister(EnemyPressureAgent agent) {
			if (agent == null) {
				return;
			}

			// Remove the enemy and release any attack permissions it was holding
			agents.Remove(agent);
			ReleaseAllTokens(agent);

			// Force reassignment so remaining enemies fill the missing role/slot quickly
			nextAssignmentTime = -Mathf.Infinity;
		}

		// Token gate used by enemies before starting an attack
		// This prevents dogpiling by limiting each attack category separately
		public bool TryRequestAttackToken(EnemyPressureAgent agent, PressureAttackTokenType type, float duration) {
			// Dead, disabled, or missing agents cannot reserve attack slots
			if (agent == null || agent.IsAliveAndEnabled == false) {
				return false;
			}

			// Clear redundant token holds first so expired permissions do not block new attacks
			ClearExpiredTokens();

			// If the same agent already has this token, refresh the duration rather than adding a duplicate
			for (int i = 0; i < tokenHolds.Count; i++) {
				TokenHold hold = tokenHolds[i];
				if (hold.Agent == agent && hold.Type == type) {
					hold.ExpireTime = Time.time + Mathf.Max(0.05f, duration);
					tokenHolds[i] = hold;
					return true;
				}
			}

			// Count how many tokens of this attack type are already active
			int used = 0;
			for (int i = 0; i < tokenHolds.Count; i++) {
				if (tokenHolds[i].Type == type) {
					used++;
				}
			}

			// Refuse the request if the configured limit has already been reached
			if (used >= GetLimit(type)) {
				return false;
			}

			// Reserve a token for this enemy and attack type
			tokenHolds.Add(new TokenHold {
				Agent = agent,
				Type = type,
				ExpireTime = Time.time + Mathf.Max(0.05f, duration)
			});

			return true;
		}

		public void ReleaseAttackToken(EnemyPressureAgent agent, PressureAttackTokenType type) {
			// Iterate backwards so RemoveAt() does not skip entries after shifting the list
			for (int i = tokenHolds.Count - 1; i >= 0; i--) {
				if (tokenHolds[i].Agent == agent && tokenHolds[i].Type == type) {
					tokenHolds.RemoveAt(i);
				}
			}
		}

		private void ReleaseAllTokens(EnemyPressureAgent agent) {
			// Used when an enemy unregisters, dies, or leaves the pressure system
			for (int i = tokenHolds.Count - 1; i >= 0; i--) {
				if (tokenHolds[i].Agent == agent) {
					tokenHolds.RemoveAt(i);
				}
			}
		}

		private int GetLimit(PressureAttackTokenType type) {
			// Clamp each limit to at least 1 so every attack category can still happen even if the inspector value is invalid
			switch (type) {
				case PressureAttackTokenType.SwordSwing:
					return Mathf.Max(1, maxSwordSwings);
				case PressureAttackTokenType.SwordLunge:
					return Mathf.Max(1, maxSwordLunges);
				case PressureAttackTokenType.DroneFire:
					return Mathf.Max(1, maxDroneShots);
				case PressureAttackTokenType.ShieldMinigun:
					return Mathf.Max(1, maxShieldMiniguns);
				case PressureAttackTokenType.ShieldSlam:
					return Mathf.Max(1, maxShieldSlams);
				default:
					return 1;
			}
		}

		private void ClearExpiredTokens() {
			// Remove token holds whose owner disappeared, became inactive, or timed out
			for (int i = tokenHolds.Count - 1; i >= 0; i--) {
				TokenHold hold = tokenHolds[i];
				if (hold.Agent == null || hold.Agent.IsAliveAndEnabled == false || Time.time >= hold.ExpireTime) {
					tokenHolds.RemoveAt(i);
				}
			}
		}

		// Rebuild active enemy groups and assign simple pressure jobs
		private void AssignJobs() {
			// Clear the cached groups before rebuilding them from the master agent list
			swords.Clear();
			drones.Clear();
			shields.Clear();

			for (int i = agents.Count - 1; i >= 0; i--) {
				EnemyPressureAgent agent = agents[i];

				// Clean up destroyed agents while scanning
				if (agent == null) {
					agents.RemoveAt(i);
					continue;
				}

				// Disabled/dead agents stay registered but should not hold an active pressure job
				if (agent.IsAliveAndEnabled == false) {
					agent.SetAssignment(EnemyPressureJob.None, 0, 1);
					continue;
				}

				// Group active enemies by pressure kind so each archetype can receive type-specific jobs
				switch (agent.Kind) {
					case EnemyPressureKind.Sword:
						swords.Add(agent);
						break;
					case EnemyPressureKind.Drone:
						drones.Add(agent);
						break;
					case EnemyPressureKind.Shield:
						shields.Add(agent);
						break;
				}
			}

			// Sort each group so closer enemies generally take the most direct pressure roles
			SortByDistanceToPlayer(swords);
			SortByDistanceToPlayer(drones);
			SortByDistanceToPlayer(shields);

			// Apply archetype-specific job layouts
			AssignSwordJobs();
			AssignDroneJobs();
			AssignShieldJobs();
		}

		// One sword pressures directly, one cuts off, the rest take side pressure slots
		private void AssignSwordJobs() {
			// Side slots are indexed after the first two sword roles
			int sideCount = Mathf.Max(1, swords.Count - 2);

			for (int i = 0; i < swords.Count; i++) {
				if (i == 0) {
					// Closest sword keeps immediate pressure on the player
					swords[i].SetAssignment(EnemyPressureJob.DirectPressure, 0, 1);
				}
				else if (i == 1) {
					// Second sword attempts to intercept/deny escape paths
					swords[i].SetAssignment(EnemyPressureJob.CutOff, 0, 1);
				}
				else {
					// Remaining swords spread around side-pressure slots
					swords[i].SetAssignment(EnemyPressureJob.SidePressure, i - 2, sideCount);
				}
			}
		}

		// Drones get orbit slots
		// Every third drone harasses from the backline
		private void AssignDroneJobs() {
			for (int i = 0; i < drones.Count; i++) {
				// BacklineHarass creates variation so all drones do not occupy the same ring/role
				EnemyPressureJob job = (i % 3 == 2) ? EnemyPressureJob.BacklineHarass : EnemyPressureJob.RangedPressure;

				// Slot index and total count allow the drone controller to space orbit targets evenly
				drones[i].SetAssignment(job, i, Mathf.Max(1, drones.Count));
			}
		}

		// Shields stay direct since they are space-holders and not flankers
		private void AssignShieldJobs() {
			for (int i = 0; i < shields.Count; i++) {
				// Shield enemies use direct pressure slots to hold space and keep the player from freely pushing forward
				shields[i].SetAssignment(EnemyPressureJob.DirectPressure, i, Mathf.Max(1, shields.Count));
			}
		}

		private void SortByDistanceToPlayer(List<EnemyPressureAgent> list) {
			// Use the player predictor when available, otherwise fall back to the director position
			PlayerMotionPredictor predictor = ResolvePredictor();
			Vector3 playerPosition = predictor != null ? predictor.Position : transform.position;

			// Sort by squared distance to avoid unnecessary square-root calculations
			list.Sort((a, b) => {
				float da = a != null ? (a.transform.position - playerPosition).sqrMagnitude : Mathf.Infinity;
				float db = b != null ? (b.transform.position - playerPosition).sqrMagnitude : Mathf.Infinity;
				return da.CompareTo(db);
			});
		}

		private PlayerMotionPredictor ResolvePredictor() {
			// Prefer an explicitly assigned predictor
			if (playerPredictor != null) {
				return playerPredictor;
			}

			// Next, use the globally active predictor if one has already registered itself
			if (PlayerMotionPredictor.Active != null) {
				playerPredictor = PlayerMotionPredictor.Active;
				return playerPredictor;
			}

			// Fallback: find the player by tag and ensure it has a PlayerMotionPredictor component
			GameObject player = GameObject.FindGameObjectWithTag(playerTag);
			if (player != null) {
				playerPredictor = player.GetComponent<PlayerMotionPredictor>();
				if (playerPredictor == null) {
					playerPredictor = player.AddComponent<PlayerMotionPredictor>();
				}
			}

			return playerPredictor;
		}

		private void OnDrawGizmosSelected() {
			// Draw small debug markers for registered pressure agents when the director is selected
			if (agents == null) {
				return;
			}

			for (int i = 0; i < agents.Count; i++) {
				EnemyPressureAgent agent = agents[i];
				if (agent == null) {
					continue;
				}

				// Slight upward offset keeps the marker visible above positions placed on the floor
				Gizmos.DrawWireSphere(agent.transform.position + Vector3.up * 0.25f, 0.25f);
			}
		}
	}
}