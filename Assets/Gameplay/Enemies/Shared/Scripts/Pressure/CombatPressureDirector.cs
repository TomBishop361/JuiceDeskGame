using System.Collections.Generic;
using UnityEngine;

namespace Game.AI {
	// Assigns pressure jobs and owns attack tokens
	[DisallowMultipleComponent]
	public sealed class CombatPressureDirector : MonoBehaviour {
		public static CombatPressureDirector Active { get; private set; }

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

		private readonly List<EnemyPressureAgent> agents = new List<EnemyPressureAgent>(32);
		private readonly List<TokenHold> tokenHolds = new List<TokenHold>(16);
		private readonly List<EnemyPressureAgent> swords = new List<EnemyPressureAgent>(12);
		private readonly List<EnemyPressureAgent> drones = new List<EnemyPressureAgent>(12);
		private readonly List<EnemyPressureAgent> shields = new List<EnemyPressureAgent>(4);

		private float nextAssignmentTime = -Mathf.Infinity;

		public PlayerMotionPredictor PlayerPredictor => ResolvePredictor();
		public IReadOnlyList<EnemyPressureAgent> Agents => agents;

		private struct TokenHold {
			public EnemyPressureAgent Agent;
			public PressureAttackTokenType Type;
			public float ExpireTime;
		}

		private void Awake() {
			if (Active != null && Active != this) {
				Debug.LogWarning($"Multiple {nameof(CombatPressureDirector)} instances found. Only one should exist per combat scene.", this);
			}

			Active = this;
			ResolvePredictor();
		}

		private void OnEnable() {
			Active = this;
		}

		private void OnDisable() {
			if (Active == this) {
				Active = null;
			}
		}

		private void Update() {
			ClearExpiredTokens();

			if (Time.time >= nextAssignmentTime) {
				AssignJobs();
				nextAssignmentTime = Time.time + Mathf.Max(0.05f, assignmentInterval);
			}
		}

		public void Register(EnemyPressureAgent agent) {
			if (agent == null || agents.Contains(agent)) {
				return;
			}

			agents.Add(agent);
			nextAssignmentTime = -Mathf.Infinity;
		}

		public void Unregister(EnemyPressureAgent agent) {
			if (agent == null) {
				return;
			}

			agents.Remove(agent);
			ReleaseAllTokens(agent);
			nextAssignmentTime = -Mathf.Infinity;
		}

		// Token gate used by enemies before starting an attack. This prevents dogpiling.
		public bool TryRequestAttackToken(EnemyPressureAgent agent, PressureAttackTokenType type, float duration) {
			if (agent == null || agent.IsAliveAndEnabled == false) {
				return false;
			}

			ClearExpiredTokens();

			for (int i = 0; i < tokenHolds.Count; i++) {
				TokenHold hold = tokenHolds[i];
				if (hold.Agent == agent && hold.Type == type) {
					hold.ExpireTime = Time.time + Mathf.Max(0.05f, duration);
					tokenHolds[i] = hold;
					return true;
				}
			}

			int used = 0;
			for (int i = 0; i < tokenHolds.Count; i++) {
				if (tokenHolds[i].Type == type) {
					used++;
				}
			}

			if (used >= GetLimit(type)) {
				return false;
			}

			tokenHolds.Add(new TokenHold {
				Agent = agent,
				Type = type,
				ExpireTime = Time.time + Mathf.Max(0.05f, duration)
			});

			return true;
		}

		public void ReleaseAttackToken(EnemyPressureAgent agent, PressureAttackTokenType type) {
			for (int i = tokenHolds.Count - 1; i >= 0; i--) {
				if (tokenHolds[i].Agent == agent && tokenHolds[i].Type == type) {
					tokenHolds.RemoveAt(i);
				}
			}
		}

		private void ReleaseAllTokens(EnemyPressureAgent agent) {
			for (int i = tokenHolds.Count - 1; i >= 0; i--) {
				if (tokenHolds[i].Agent == agent) {
					tokenHolds.RemoveAt(i);
				}
			}
		}

		private int GetLimit(PressureAttackTokenType type) {
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
			for (int i = tokenHolds.Count - 1; i >= 0; i--) {
				TokenHold hold = tokenHolds[i];
				if (hold.Agent == null || hold.Agent.IsAliveAndEnabled == false || Time.time >= hold.ExpireTime) {
					tokenHolds.RemoveAt(i);
				}
			}
		}

		// Rebuild active enemy groups and assign simple pressure jobs
		private void AssignJobs() {
			swords.Clear();
			drones.Clear();
			shields.Clear();

			for (int i = agents.Count - 1; i >= 0; i--) {
				EnemyPressureAgent agent = agents[i];
				if (agent == null) {
					agents.RemoveAt(i);
					continue;
				}

				if (agent.IsAliveAndEnabled == false) {
					agent.SetAssignment(EnemyPressureJob.None, 0, 1);
					continue;
				}

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

			SortByDistanceToPlayer(swords);
			SortByDistanceToPlayer(drones);
			SortByDistanceToPlayer(shields);

			AssignSwordJobs();
			AssignDroneJobs();
			AssignShieldJobs();
		}

		// One sword pressures directly, one cuts off, the rest take side pressure slots.
		private void AssignSwordJobs() {
			int sideCount = Mathf.Max(1, swords.Count - 2);

			for (int i = 0; i < swords.Count; i++) {
				if (i == 0) {
					swords[i].SetAssignment(EnemyPressureJob.DirectPressure, 0, 1);
				}
				else if (i == 1) {
					swords[i].SetAssignment(EnemyPressureJob.CutOff, 0, 1);
				}
				else {
					swords[i].SetAssignment(EnemyPressureJob.SidePressure, i - 2, sideCount);
				}
			}
		}

		// Drones get orbit slots
		// Every third drone harasses from the backline
		private void AssignDroneJobs() {
			for (int i = 0; i < drones.Count; i++) {
				EnemyPressureJob job = (i % 3 == 2) ? EnemyPressureJob.BacklineHarass : EnemyPressureJob.RangedPressure;
				drones[i].SetAssignment(job, i, Mathf.Max(1, drones.Count));
			}
		}

		// Shields stay direct since they are space-holders and not flankers
		private void AssignShieldJobs() {
			for (int i = 0; i < shields.Count; i++) {
				shields[i].SetAssignment(EnemyPressureJob.DirectPressure, i, Mathf.Max(1, shields.Count));
			}
		}

		private void SortByDistanceToPlayer(List<EnemyPressureAgent> list) {
			PlayerMotionPredictor predictor = ResolvePredictor();
			Vector3 playerPosition = predictor != null ? predictor.Position : transform.position;

			list.Sort((a, b) => {
				float da = a != null ? (a.transform.position - playerPosition).sqrMagnitude : Mathf.Infinity;
				float db = b != null ? (b.transform.position - playerPosition).sqrMagnitude : Mathf.Infinity;
				return da.CompareTo(db);
			});
		}

		private PlayerMotionPredictor ResolvePredictor() {
			if (playerPredictor != null) {
				return playerPredictor;
			}

			if (PlayerMotionPredictor.Active != null) {
				playerPredictor = PlayerMotionPredictor.Active;
				return playerPredictor;
			}

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
			if (agents == null) {
				return;
			}

			for (int i = 0; i < agents.Count; i++) {
				EnemyPressureAgent agent = agents[i];
				if (agent == null) {
					continue;
				}

				Gizmos.DrawWireSphere(agent.transform.position + Vector3.up * 0.25f, 0.25f);
			}
		}
	}
}