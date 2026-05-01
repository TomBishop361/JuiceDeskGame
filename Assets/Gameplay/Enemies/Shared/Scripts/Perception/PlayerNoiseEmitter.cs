using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.AI {
	// Add this to the player
	// It emits AI noise events that enemy hearing sensors can react to
	// Existing player scripts can call the public Emit methods from movement/combat events
	[DisallowMultipleComponent]
	public sealed class PlayerNoiseEmitter : MonoBehaviour {
		[Header("Automatic Movement Noise")]
		[Tooltip("If true, emits periodic movement noise based on Rigidbody or CharacterController velocity.")]
		[SerializeField] private bool autoEmitMovementNoise = true;
		[Tooltip("Minimum horizontal speed needed before automatic movement noise can be emitted.")]
		[SerializeField] private float minMovementSpeed = 1.5f;
		[Tooltip("Seconds between automatic movement noise pulses.")]
		[SerializeField] private float movementNoiseInterval = 0.35f;
		[Tooltip("Radius of normal movement noise.")]
		[SerializeField] private float movementNoiseRadius = 5.0f;
		[Tooltip("Extra radius added when the detected horizontal speed is high.")]
		[SerializeField] private float fastMovementBonusRadius = 4.0f;
		[Tooltip("Horizontal speed treated as fast movement for noise scaling.")]
		[SerializeField] private float fastMovementSpeed = 10.0f;

		[Header("Landing Noise")]
		[Tooltip("If true, emits a landing noise when a CharacterController or Rigidbody becomes grounded after falling.")]
		[SerializeField] private bool autoEmitLandingNoise = true;
		[Tooltip("Downward speed needed to emit a landing noise.")]
		[SerializeField] private float landingVerticalSpeedThreshold = 6.0f;
		[Tooltip("Radius of a normal landing noise.")]
		[SerializeField] private float landingNoiseRadius = 10.0f;

		[Header("Manual Noise Radius")]
		[Tooltip("Noise radius for firing a loud weapon.")]
		[SerializeField] private float weaponNoiseRadius = 18.0f;
		[Tooltip("Noise radius for grapple launch or latch events.")]
		[SerializeField] private float grappleNoiseRadius = 12.0f;
		[Tooltip("Noise radius for wallrun start/tick events.")]
		[SerializeField] private float wallRunNoiseRadius = 9.0f;
		[Tooltip("Noise radius for slide start/tick events.")]
		[SerializeField] private float slideNoiseRadius = 8.0f;
		[Tooltip("Noise radius for rail grind start/tick events.")]
		[SerializeField] private float railGrindNoiseRadius = 11.0f;

		private Rigidbody cachedRigidbody;
		private CharacterController cachedCharacterController;
		private float nextMovementNoiseTime;
		private bool wasGrounded;
		private float previousVerticalVelocity;
		private Vector3 previousPosition;

		private void Awake() {
			// Cache movement components
			cachedRigidbody = GetComponent<Rigidbody>();
			cachedCharacterController = GetComponent<CharacterController>();

			// Store initial movement state for fallback velocity and landing checks
			previousPosition = transform.position;
			wasGrounded = IsGrounded();
		}

		private void Update() {
			// Estimate velocity from the best available movement component
			Vector3 velocity = GetEstimatedVelocity();

			// Optional passive footstep/movement noise
			if (autoEmitMovementNoise) {
				TickAutomaticMovementNoise(velocity);
			}

			// Optional passive landing noise
			if (autoEmitLandingNoise) {
				TickAutomaticLandingNoise(velocity);
			}

			// Save values for the next frame
			previousVerticalVelocity = velocity.y;
			previousPosition = transform.position;
		}

		// Emits a normal movement noise e.g. running or sprinting
		public void EmitMovementNoise(float multiplier = 1.0f) {
			EmitNoise(AINoiseKind.Movement, movementNoiseRadius * Mathf.Max(0.1f, multiplier));
		}

		// Emits a landing noise e.g. usually after a fall or jump
		public void EmitLandingNoise(float multiplier = 1.0f) {
			EmitNoise(AINoiseKind.Landing, landingNoiseRadius * Mathf.Max(0.1f, multiplier));
		}

		// Emits a weapon noise e.g. firing the SMG or sniper
		public void EmitWeaponNoise(float multiplier = 1.0f) {
			EmitNoise(AINoiseKind.Weapon, weaponNoiseRadius * Mathf.Max(0.1f, multiplier));
		}

		// Emits a grapple noise
		public void EmitGrappleNoise(float multiplier = 1.0f) {
			EmitNoise(AINoiseKind.Grapple, grappleNoiseRadius * Mathf.Max(0.1f, multiplier));
		}

		// Emits a wallrun noise
		public void EmitWallRunNoise(float multiplier = 1.0f) {
			EmitNoise(AINoiseKind.WallRun, wallRunNoiseRadius * Mathf.Max(0.1f, multiplier));
		}

		// Emits a slide noise
		public void EmitSlideNoise(float multiplier = 1.0f) {
			EmitNoise(AINoiseKind.Slide, slideNoiseRadius * Mathf.Max(0.1f, multiplier));
		}

		// Emits a rail-grind noise
		public void EmitRailGrindNoise(float multiplier = 1.0f) {
			EmitNoise(AINoiseKind.RailGrind, railGrindNoiseRadius * Mathf.Max(0.1f, multiplier));
		}

		// Sends a custom noise event to the AI noise bus
		public void EmitNoise(AINoiseKind kind, float radius, float loudness = 1.0f) {
			AINoiseBus.Emit(transform.position, transform, radius, loudness, kind);
		}

		// Emits occasional automatic movement noise based on horizontal speed
		private void TickAutomaticMovementNoise(Vector3 velocity) {
			float horizontalSpeed = new Vector3(velocity.x, 0.0f, velocity.z).magnitude;

			// Ignore slow movement and wait for the next allowed noise pulse
			if (horizontalSpeed < minMovementSpeed || Time.time < nextMovementNoiseTime) {
				return;
			}

			// Faster movement creates a slightly larger and louder noise
			float fastT = Mathf.InverseLerp(minMovementSpeed, fastMovementSpeed, horizontalSpeed);
			float radius = movementNoiseRadius + fastMovementBonusRadius * fastT;

			EmitNoise(AINoiseKind.Movement, radius, Mathf.Lerp(0.5f, 1.0f, fastT));
			nextMovementNoiseTime = Time.time + movementNoiseInterval;
		}

		// Emits a landing noise when the player becomes grounded after falling fast enough
		private void TickAutomaticLandingNoise(Vector3 velocity) {
			bool grounded = IsGrounded();

			if (grounded && wasGrounded == false && previousVerticalVelocity <= -landingVerticalSpeedThreshold) {
				// Harder landings create stronger noise
				float hardLandingT = Mathf.InverseLerp(landingVerticalSpeedThreshold, landingVerticalSpeedThreshold * 2.0f, Mathf.Abs(previousVerticalVelocity));

				EmitLandingNoise(Mathf.Lerp(0.75f, 1.5f, hardLandingT));
			}

			wasGrounded = grounded;
		}

		// Gets velocity from Rigidbody, CharacterController, or frame by frame movement
		private Vector3 GetEstimatedVelocity() {
			if (cachedRigidbody != null) {
				return cachedRigidbody.linearVelocity;
			}

			if (cachedCharacterController != null) {
				return cachedCharacterController.velocity;
			}

			// Fallback for custom movement controllers
			float dt = Mathf.Max(Time.deltaTime, 0.0001f);
			return (transform.position - previousPosition) / dt;
		}

		// Checks whether the player is grounded using the available movement component
		private bool IsGrounded() {
			if (cachedCharacterController != null) {
				return cachedCharacterController.isGrounded;
			}

			if (cachedRigidbody != null) {
				return Mathf.Abs(cachedRigidbody.linearVelocity.y) < 0.05f;
			}

			// Assume grounded if no movement component exists
			return true;
		}
	}
}