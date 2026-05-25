using UnityEngine;
using UnityEngine.AI;

namespace Game.AI {
	// Handles NavMesh-based ground movement for enemies, including chasing + stopping + agent reset + facing control
	public sealed class GroundEnemyMotor : MonoBehaviour {
		[Header("Agent")]
		[SerializeField] private NavMeshAgent navMeshAgent;

		[Header("Movement")]
		[Tooltip("Movement speed assigned to the NavMeshAgent during runtime reset.")]
		[SerializeField] private float moveSpeed = 6.0f;
		[Tooltip("Acceleration assigned to the NavMeshAgent during runtime reset.")]
		[SerializeField] private float acceleration = 16.0f;
		[Tooltip("Angular speed assigned to the NavMeshAgent for path turning.")]
		[SerializeField] private float angularSpeed = 360.0f;

		[Header("Rotation")]
		[Tooltip("If true, this motor disables NavMeshAgent.updateRotation and uses FaceTarget/FaceDirection instead.")]
		[SerializeField] private bool useManualRotation = true;
		[Tooltip("Use smooth rotation instead of direct turn speed-based facing.")]
		[SerializeField] private bool toggleSmoothRotation = false;
		[Tooltip("Turn speed (degrees per second) when smooth rotation is disabled.")]
		[SerializeField] private float rotationSpeed = 360.0f;
		[Tooltip("Slerp multiplier used when smooth rotation is enabled.")]
		[SerializeField] private float rotationSmoothing = 10.0f;

		// Current movement speed given by the NavMeshAgent
		// Useful for animation blending.
		public float VelocityMagnitude => navMeshAgent != null ? navMeshAgent.velocity.magnitude : 0.0f;

		// Desired path velocity from the NavMeshAgent
		// Useful for facing the direction of travel while pathing around corners
		public Vector3 DesiredVelocity => navMeshAgent != null ? navMeshAgent.desiredVelocity : Vector3.zero;

		// True when the NavMeshAgent exists + is enabled + is currently on a valid NavMesh
		public bool AgentReady => navMeshAgent != null && navMeshAgent.enabled && navMeshAgent.isOnNavMesh;

		private void Reset() {
			FindNavAgentIfNeeded();
		}

		private void Awake() {
			FindNavAgentIfNeeded();
			ApplyNavAgentSettings();
		}

		// Restores agent settings and prepares the motor for reuse after pooling or respawn
		public void ResetRuntime() {
			FindNavAgentIfNeeded();

			if (navMeshAgent == null) {
				return;
			}

			navMeshAgent.enabled = true;

			if (navMeshAgent.isOnNavMesh == false) {
				// Warp back onto the NavMesh if this pooled enemy was re-enabled off-mesh
				navMeshAgent.Warp(transform.position);
			}

			ApplyNavAgentSettings();

			navMeshAgent.isStopped = false;
			navMeshAgent.ResetPath();
		}

		// Starts chasing the target's current position
		public void Chase(Transform target) {
			if (target == null) {
				return;
			}

			Chase(target.position);
		}

		// Starts pathfinding toward a world position if the agent is ready
		public void Chase(Vector3 position) {
			if (AgentReady == false) {
				return;
			}

			navMeshAgent.isStopped = false;
			navMeshAgent.SetDestination(position);
		}

		// Stops the NavMeshAgent without disabling it
		public void Stop() {
			if (AgentReady == false) {
				return;
			}

			navMeshAgent.isStopped = true;
		}

		// Stops and disables the NavMeshAgent, usually for death, knockback, or non-NavMesh movement
		public void DisableAgent() {
			if (navMeshAgent == null) {
				return;
			}

			if (navMeshAgent.enabled && navMeshAgent.isOnNavMesh) {
				navMeshAgent.isStopped = true;
			}

			navMeshAgent.enabled = false;
		}

		// Returns true when the agent currently has a useful path direction to face
		public bool HasUsefulDesiredVelocity(float minimumMagnitude = 0.1f) {
			if (navMeshAgent == null) {
				return false;
			}

			return navMeshAgent.desiredVelocity.sqrMagnitude >= minimumMagnitude * minimumMagnitude;
		}

		// Rotates the enemy to face a world position
		public void FaceTarget(Vector3 worldPosition) {
			Vector3 flatDirection = worldPosition - transform.position;
			flatDirection.y = 0.0f;

			FaceDirection(flatDirection);
		}

		// Rotates the enemy to face a world direction
		// This is useful when the enemy should face its NavMeshAgent desired velocity instead of the player
		// Uses either smooth or turn-speed-based rotation
		public void FaceDirection(Vector3 worldDirection) {
			worldDirection.y = 0.0f;

			if (worldDirection.sqrMagnitude < 0.0001f) {
				return;
			}

			Quaternion targetRotation = Quaternion.LookRotation(worldDirection.normalized, Vector3.up);

			if (toggleSmoothRotation) {
				// Smooth rotation towards the requested direction (using Slerp)
				// NOTE: You could use Smooth Rotation for chasing then rotatetowards for windup 
				transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSmoothing);
			}
			else {
				// Responsive rotation towards the requested direction (using RotateTowards)
				// NOTE: Better for SwordEnemy since precise, melee facing 
				// NOTE: Better for ShieldEnemy since heavy, deliberate
				transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
			}
		}

		// Ensures the NavMeshAgent cannot fight manual rotation when this motor owns facing
		private void ApplyNavAgentSettings() {
			if (navMeshAgent == null) {
				return;
			}

			navMeshAgent.speed = moveSpeed;
			navMeshAgent.acceleration = acceleration;
			navMeshAgent.angularSpeed = angularSpeed;
			navMeshAgent.updateRotation = useManualRotation == false;
		}

		private void FindNavAgentIfNeeded() {
			if (navMeshAgent == null) {
				navMeshAgent = GetComponent<NavMeshAgent>();
			}
		}

		private void OnValidate() {
			moveSpeed = Mathf.Max(0.0f, moveSpeed);
			acceleration = Mathf.Max(0.0f, acceleration);
			angularSpeed = Mathf.Max(0.0f, angularSpeed);
			rotationSpeed = Mathf.Max(0.0f, rotationSpeed);
			rotationSmoothing = Mathf.Max(0.0f, rotationSmoothing);
		}
	}
}