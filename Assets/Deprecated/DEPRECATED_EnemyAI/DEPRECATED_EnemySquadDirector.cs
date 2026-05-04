//using Game.AI.Drone;
//using Game.AI.Shield;
//using Game.AI.Sword;
//using System.Collections.Generic;
//using UnityEngine;


//// DEPRECATED


//namespace Game.AI {
//	// Scene-level coordinator for one enemy squad
//	// It does not move or attack directly but instead assigns tactical roles +
//	// relays confirmed sightings/noises so individual BT's can make tactical decisions
//	public sealed class EnemySquadDirector : MonoBehaviour {
//		// Active directors are tracked so squad members can find the correct director by Squad Id
//		private static readonly List<EnemySquadDirector> ActiveDirectors = new List<EnemySquadDirector>();

//		[Header("Squad")]
//		[Tooltip("Only members with the same Squad Id register to this director.")]
//		[SerializeField] private string squadId = "Default";
//		[Tooltip("If true, the director periodically assigns roles so enemies do not all choose the same tactic.")]
//		[SerializeField] private bool assignRoles = true;
//		[Tooltip("Time (in seconds) between role assignment passes.")]
//		[SerializeField] private float reassignmentInterval = 1.0f;
//		[Tooltip("Minimum time (in seconds) before an enemy can be swapped to a different role again.")]
//		[SerializeField] private float roleChangeCooldown = 3.0f;

//		[Header("Role Limits")]
//		[Tooltip("Maximum interceptors at once. Keep this low for fairness.")]
//		[SerializeField] private int maxInterceptors = 1;
//		[Tooltip("Maximum suppressors at once.")]
//		[SerializeField] private int maxSuppressors = 2;
//		[Tooltip("Maximum anchor/blockers at once.")]
//		[SerializeField] private int maxAnchors = 1;

//		[Header("Shared Awareness")]
//		[Tooltip("Maximum range for sharing visual sightings between squad members.")]
//		[SerializeField] private float sightingShareRange = 35.0f;
//		[Tooltip("Maximum range for sharing suspicious noise positions between squad members.")]
//		[SerializeField] private float noiseShareRange = 28.0f;

//		public string SquadId => squadId;

//		private readonly List<EnemySquadMember> members = new List<EnemySquadMember>();
//		private float nextReassignmentTime;

//		private void OnEnable() {
//			// Register this director so squad members can find it at runtime
//			if (ActiveDirectors.Contains(this) == false) {
//				ActiveDirectors.Add(this);
//			}
//		}

//		private void OnDisable() {
//			// Remove this director from the lookup list when it is disabled or destroyed
//			ActiveDirectors.Remove(this);
//		}

//		private void Update() {
//			// Role assignment is limited so roles do not flicker every frame
//			if (assignRoles == false || Time.time < nextReassignmentTime) {
//				return;
//			}

//			AssignRoles();
//			nextReassignmentTime = Time.time + reassignmentInterval;
//		}

//		// Finds the active director that owns the requested squad id
//		public static EnemySquadDirector FindForSquad(string requestedSquadId) {
//			for (int i = 0; i < ActiveDirectors.Count; i++) {
//				EnemySquadDirector director = ActiveDirectors[i];
//				if (director != null && director.SquadId == requestedSquadId) {
//					return director;
//				}
//			}

//			return null;
//		}

//		// Adds an enemy to this squad
//		public void Register(EnemySquadMember member) {
//			if (member == null || member.SquadId != squadId || members.Contains(member)) {
//				return;
//			}

//			members.Add(member);
//		}

//		// Removes an enemy from this squad
//		public void Unregister(EnemySquadMember member) {
//			members.Remove(member);
//		}

//		// Shares a confirmed player sighting with nearby active squadmates
//		public void ReportPlayerSighting(EnemySquadMember source, Transform target, Vector3 position, Vector3 velocity) {
//			if (source == null) {
//				return;
//			}

//			for (int i = 0; i < members.Count; i++) {
//				EnemySquadMember member = members[i];

//				// Do not report back to the source, inactive enemies, or dead/disabled enemies
//				if (member == null || member == source || IsMemberActive(member) == false) {
//					continue;
//				}

//				// Only nearby squadmates receive the shared sighting
//				if (Vector3.Distance(source.transform.position, member.transform.position) > sightingShareRange) {
//					continue;
//				}

//				member.ApplySharedSighting(target, position, velocity, Time.time);
//			}
//		}

//		// Shares a suspicious noise with nearby active squadmates
//		public void ReportNoise(EnemySquadMember source, Vector3 position, float radius, AINoiseKind kind, float time) {
//			if (source == null) {
//				return;
//			}

//			for (int i = 0; i < members.Count; i++) {
//				EnemySquadMember member = members[i];

//				// Skip the source and enemies that should not currently react
//				if (member == null || member == source || IsMemberActive(member) == false) {
//					continue;
//				}

//				// Only nearby squadmates receive the noise report
//				if (Vector3.Distance(source.transform.position, member.transform.position) > noiseShareRange) {
//					continue;
//				}

//				member.ApplySharedNoise(position, radius, kind, time);
//			}
//		}

//		// Assigns tactical roles while respecting role limits and cooldowns
//		private void AssignRoles() {
//			int chasers = 0;
//			int interceptors = 0;
//			int suppressors = 0;
//			int anchors = 0;
//			int flankers = 0;

//			// First count enemies whose roles are still on cooldown
//			// These roles remain stable and reduce the available role slots
//			for (int i = 0; i < members.Count; i++) {
//				EnemySquadMember member = members[i];

//				if (IsMemberActive(member) == false || CanChangeRole(member) == true) {
//					continue;
//				}

//				CountRole(member.Role, ref chasers, ref interceptors, ref suppressors, ref anchors, ref flankers);
//			}

//			// Then assign roles to enemies that are allowed to change
//			for (int i = 0; i < members.Count; i++) {
//				EnemySquadMember member = members[i];

//				if (IsMemberActive(member) == false || CanChangeRole(member) == false) {
//					continue;
//				}

//				EnemyTacticalRole role = PickRoleFor(member, chasers, interceptors, suppressors, anchors, flankers);
//				member.AssignRole(role);

//				// Count the new role so later enemies do not all pick the same one
//				CountRole(role, ref chasers, ref interceptors, ref suppressors, ref anchors, ref flankers);
//			}
//		}

//		// Chooses a useful role based on enemy type and current squad composition
//		private EnemyTacticalRole PickRoleFor(EnemySquadMember member, int chasers, int interceptors, int suppressors, int anchors, int flankers) {
//			if (member.GetComponent<ShieldEnemy>() != null) {
//				// Shield enemies are best at suppressing or blocking space
//				if (suppressors < maxSuppressors) {
//					return EnemyTacticalRole.Suppressor;
//				}

//				if (anchors < maxAnchors) {
//					return EnemyTacticalRole.Anchor;
//				}

//				return EnemyTacticalRole.Chaser;
//			}

//			// Drones are useful as suppressors or flankers because they can reposition vertically
//			if (member.GetComponent<DroneEnemy>() != null) {
//				if (suppressors < maxSuppressors) {
//					return EnemyTacticalRole.Suppressor;
//				}

//				return EnemyTacticalRole.Flanker;
//			}

//			// Sword enemies should keep pressure, but only a few should intercept
//			if (member.GetComponent<SwordEnemy>() != null) {
//				if (chasers == 0) {
//					return EnemyTacticalRole.Chaser;
//				}

//				if (interceptors < maxInterceptors) {
//					return EnemyTacticalRole.Interceptor;
//				}

//				return flankers <= chasers ? EnemyTacticalRole.Flanker : EnemyTacticalRole.Chaser;
//			}

//			// Fallback role choice for any future enemy type
//			return chasers == 0 ? EnemyTacticalRole.Chaser : EnemyTacticalRole.Flanker;
//		}

//		// Returns true when this enemy is allowed to receive a new role
//		private bool CanChangeRole(EnemySquadMember member) {
//			EnemyBlackboard blackboard = member.Blackboard;
//			if (blackboard == null || blackboard.TacticalRole == EnemyTacticalRole.None) {
//				return true;
//			}

//			return (Time.time - blackboard.RoleAssignedTime) >= roleChangeCooldown;
//		}

//		// Returns true if the member can currently participate in squad logic
//		private bool IsMemberActive(EnemySquadMember member) {
//			if (member == null || member.gameObject.activeInHierarchy == false || member.Blackboard == null) {
//				return false;
//			}

//			return member.Blackboard.IsDead == false && member.Blackboard.IsDisabled == false;
//		}

//		// Adds one to the counter for the supplied role
//		private void CountRole(EnemyTacticalRole role, ref int chasers, ref int interceptors, ref int suppressors, ref int anchors, ref int flankers) {
//			switch (role) {
//				case EnemyTacticalRole.Chaser:
//					chasers++;
//					break;
//				case EnemyTacticalRole.Interceptor:
//					interceptors++;
//					break;
//				case EnemyTacticalRole.Suppressor:
//					suppressors++;
//					break;
//				case EnemyTacticalRole.Anchor:
//					anchors++;
//					break;
//				case EnemyTacticalRole.Flanker:
//					flankers++;
//					break;
//			}
//		}
//	}
//}