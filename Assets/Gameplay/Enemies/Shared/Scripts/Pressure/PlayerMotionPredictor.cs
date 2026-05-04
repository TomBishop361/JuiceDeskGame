using UnityEngine;

namespace Game.AI {
	// Player motion memory used by the pressure system for enemies
	// Put this on the player root
	// It does not require changes to the player controller
	// This component is the only place that estimates future player positions
	// Enemy scripts read these predictions instead of each implementing their own velocity logic
	[DisallowMultipleComponent]
	public sealed class PlayerMotionPredictor : MonoBehaviour {
		// Global reference so AI systems can quickly find the active player predictor
		public static PlayerMotionPredictor Active { get; private set; }

		[Header("Prediction")]
		[Tooltip("Time (in seconds) ahead used for immediate melee aim and close pressure.")]
		[SerializeField] private float shortLeadTime = 0.25f;
		[Tooltip("Time (in seconds) ahead used for cutoff movement and drone orbit anchoring.")]
		[SerializeField] private float mediumLeadTime = 0.65f;
		[Tooltip("Time (in seconds) ahead exposed for debug or future behaviours that need longer prediction.")]
		[SerializeField] private float longLeadTime = 1.10f;
		[Tooltip("Higher values make prediction react faster. Lower values reduce jitter.")]
		[SerializeField] private float velocitySmoothing = 14.0f;
		[Tooltip("Limits players measured speed so prediction does not overflow on physics spikes.")]
		[SerializeField] private float maxPredictionSpeed = 24.0f;
		[Tooltip("Reduces vertical velocity contribution so enemies do not over-aim jumps, wallruns, or grapples.")]
		[SerializeField] private float verticalPredictionScale = 0.35f;

		[Header("Fallback")]
		[Tooltip("When the player is nearly stationary, use player forward as the movement direction fallback.")]
		[SerializeField] private bool useTransformForwardWhenSlow = true;
		[Tooltip("Speed below which the player is treated as slow or stationary.")]
		[SerializeField] private float slowSpeedThreshold = 0.75f;

		// Cached movement components used as the preferred source of velocity
		private Rigidbody cachedRigidbody;
		private CharacterController cachedCharacterController;

		// Position history used as a fallback velocity estimate when no movement component is available
		private Vector3 previousPosition;
		private bool hasPreviousPosition;

		// Current measured player position and smoothed velocity data exposed to enemy AI
		public Vector3 Position { get; private set; }
		public Vector3 Velocity { get; private set; }
		public Vector3 PlanarVelocity { get; private set; }

		// Horizontal movement direction used by enemies for cutoff + strafing + + pressure decisions
		public Vector3 PlanarMoveDirection { get; private set; } = Vector3.forward;

		// Cached prediction points at different time horizons so other scripts can read them directly
		public Vector3 ShortFuturePosition { get; private set; }
		public Vector3 MediumFuturePosition { get; private set; }
		public Vector3 LongFuturePosition { get; private set; }

		// Properties for checking how quickly the player is moving
		public float Speed => Velocity.magnitude;
		public float PlanarSpeed => PlanarVelocity.magnitude;
		public bool HasUsefulMotion => PlanarSpeed >= slowSpeedThreshold;

		private void Awake() {
			// Register this predictor as the active instance unless another valid instance already exists
			if (Active == null || Active == this) {
				Active = this;
			}

			// Cache optional movement components once instead of looking them up every frame
			cachedRigidbody = GetComponent<Rigidbody>();
			cachedCharacterController = GetComponent<CharacterController>();

			// Seed position history so the first update has a stable starting point
			Position = transform.position;
			previousPosition = Position;
			hasPreviousPosition = true;
			UpdateFuturePositions();
		}
		private void OnEnable() {
			// If this component is re-enabled and there is no active predictor, claim the active slot
			if (Active == null) {
				Active = this;
			}
		}

		private void OnDisable() {
			// Clear the global reference only if it currently points to this instance
			if (Active == this) {
				Active = null;
			}
		}

		private void Update() {
			// Refresh prediction data once per frame
			Tick(Time.deltaTime);
		}

		public void Tick(float deltaTime) {
			Position = transform.position;

			// Prefer velocity from physics/controller components, then fall back to position delta
			Vector3 measuredVelocity = Vector3.zero;
			if (cachedRigidbody != null) {
				measuredVelocity = cachedRigidbody.linearVelocity;
			}
			else if (cachedCharacterController != null) {
				measuredVelocity = cachedCharacterController.velocity;
			}
			else if (hasPreviousPosition && deltaTime > 0.0001f) {
				measuredVelocity = (Position - previousPosition) / deltaTime;
			}

			// Clamp any bad spikes so prediction does not launch enemies toward extreme positions
			measuredVelocity = Vector3.ClampMagnitude(measuredVelocity, maxPredictionSpeed);

			// Exponential smoothing keeps prediction responsive while reducing jitter frame-to-frame
			float blend = 1.0f - Mathf.Exp(-velocitySmoothing * Mathf.Max(0.0001f, deltaTime));
			Velocity = Vector3.Lerp(Velocity, measuredVelocity, blend);

			// Keep a separate horizontal velocity for direction and speed checks
			PlanarVelocity = Vector3.ProjectOnPlane(Velocity, Vector3.up);

			// Use real horizontal motion when the player is moving fast enough to be reliable
			if (PlanarVelocity.sqrMagnitude >= slowSpeedThreshold * slowSpeedThreshold) {
				PlanarMoveDirection = PlanarVelocity.normalized;
			}
			else if (useTransformForwardWhenSlow) {
				// When nearly stationary, use the player's facing direction as a stable fallback
				Vector3 flatForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
				if (flatForward.sqrMagnitude > 0.0001f) {
					PlanarMoveDirection = flatForward.normalized;
				}
			}

			// Store this frame's position for the next fallback velocity calculation
			previousPosition = Position;
			hasPreviousPosition = true;

			// Recalculate cached future positions after velocity and direction have been updated
			UpdateFuturePositions();
		}

		// Returns a simple velocity projection with damped vertical movement
		public Vector3 Predict(float leadTime) {
			Vector3 predictionVelocity = Velocity;

			// Reduce vertical influence so jumps, wallruns, or grapples do not dominate aim prediction
			predictionVelocity.y *= verticalPredictionScale;
			return Position + predictionVelocity * Mathf.Max(0.0f, leadTime);
		}

		private void UpdateFuturePositions() {
			// Cache the common prediction horizons used by enemy behaviours and debug visuals
			ShortFuturePosition = Predict(shortLeadTime);
			MediumFuturePosition = Predict(mediumLeadTime);
			LongFuturePosition = Predict(longLeadTime);
		}

		private void OnDrawGizmosSelected() {
			Vector3 pos = Application.isPlaying ? Position : transform.position;
			Vector3 dir = Application.isPlaying ? PlanarMoveDirection : Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;

			// Current player position and planar movement/facing direction
			Gizmos.DrawWireSphere(pos, 0.25f);
			Gizmos.DrawLine(pos, pos + dir * 2.0f);

			if (Application.isPlaying) {
				// Draw each prediction horizon and connect them so the prediction arc is easy to inspect
				Gizmos.DrawWireSphere(ShortFuturePosition, 0.18f);
				Gizmos.DrawWireSphere(MediumFuturePosition, 0.24f);
				Gizmos.DrawWireSphere(LongFuturePosition, 0.30f);
				Gizmos.DrawLine(pos, ShortFuturePosition);
				Gizmos.DrawLine(ShortFuturePosition, MediumFuturePosition);
				Gizmos.DrawLine(MediumFuturePosition, LongFuturePosition);
			}
		}
	}
}
