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
		[Tooltip("Use smooth rotation instead of direct turn speed-based facing.")]
		[SerializeField] private bool toggleSmoothRotation = false;
		[Tooltip("Turn speed (degrees per second) when smooth rotation is disabled.")]
		[SerializeField] private float rotationSpeed = 360.0f;
		[Tooltip("Slerp multiplier used when smooth rotation is enabled.")]
		[SerializeField] private float rotationSmoothing = 10.0f;

		// Current movement speed given by the NavMeshAgent
		// Useful for animation blending.
		public float VelocityMagnitude => navMeshAgent != null ? navMeshAgent.velocity.magnitude : 0.0f;

		// True when the NavMeshAgent exists + is enabled + is currently on a valid NavMesh
		public bool AgentReady => navMeshAgent != null && navMeshAgent.enabled && navMeshAgent.isOnNavMesh;

		private void Reset() {
			if (navMeshAgent == null) {
				navMeshAgent = GetComponent<NavMeshAgent>();
			}
		}

		// Restores agent settings and prepares the motor for reuse after pooling or respawn
		public void ResetRuntime() {
			if (navMeshAgent == null) {
				return;
			}

			navMeshAgent.enabled = true;

			if (navMeshAgent.isOnNavMesh == false) {
				// Warp back onto the NavMesh if this pooled enemy was re-enabled off-mesh.
				navMeshAgent.Warp(transform.position);
			}

			navMeshAgent.speed = moveSpeed;
			navMeshAgent.acceleration = acceleration;
			navMeshAgent.angularSpeed = angularSpeed;
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

		// Rotates the enemy to face a world position using either smooth or turn-speed-based rotation
		public void FaceTarget(Vector3 worldPosition) {
			Vector3 flatDirection = worldPosition - transform.position;
			flatDirection.y = 0.0f;

			if (flatDirection.sqrMagnitude < 0.0001f) {
				return;
			}

			Quaternion targetRotation = Quaternion.LookRotation(flatDirection.normalized, Vector3.up);

			if (toggleSmoothRotation) {
				// Smooth rotation towards target direction (using Slerp)
				// NOTE: USE SMOOTH ROTATION FOR CHASING THEN ROTATETOWARDS FOR WINDUP
				transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSmoothing);
			}
			else {
				// Responsive rotation towards target direction (using RotateTowards)
				// [BETTER FOR SWORD since precise, melee facing]
				// [BETTER FOR SHIELD since heavy, deliberate]
				transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
			}
		}
	}
}