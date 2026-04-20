using UnityEngine;

namespace Game.AI.Drone {
	[DisallowMultipleComponent]
	[RequireComponent(typeof(FlightEnemyMotor))]
	public sealed class DroneVisualMotion : MonoBehaviour {
		[Header("References")]
		[Tooltip("Child transform that contains the visible drone mesh. Leave the root alone so gameplay movement remains stable.")]
		[SerializeField] private Transform visualRoot;
		[Tooltip("Flight motor used to read current movement speed.")]
		[SerializeField] private FlightEnemyMotor flightMotor;

		[Header("Bobbing")]
		[Tooltip("Small local up/down hover motion layered on top of the existing flight path.")]
		[SerializeField] private float bobAmplitude = 0.15f;
		[Tooltip("How quickly the local visual bob loops.")]
		[SerializeField] private float bobFrequency = 2.0f;

		[Header("Tilt")]
		[Tooltip("Maximum nose-up / nose-down tilt applied from movement direction.")]
		[SerializeField] private float maxPitch = 10.0f;
		[Tooltip("Maximum side tilt applied while orbiting / strafing.")]
		[SerializeField] private float maxBank = 14.0f;
		[Tooltip("How quickly pitch / bank settle toward their target values.")]
		[SerializeField] private float tiltSmoothing = 8.0f;

		[Header("Recoil")]
		[Tooltip("How far the visible drone kicks back when it fires.")]
		[SerializeField] private float recoilDistance = 0.18f;
		[Tooltip("How quickly the recoil returns back to neutral.")]
		[SerializeField] private float recoilReturnSpeed = 10.0f;

		private Vector3 baseLocalPosition;
		private Quaternion baseLocalRotation;
		private Vector3 lastWorldPosition;
		private float bobPhase;
		private float recoilOffset;
		private float smoothedPitch;
		private float smoothedBank;

		private void Reset() {
			if (flightMotor == null) {
				flightMotor = GetComponent<FlightEnemyMotor>();
			}

			if (visualRoot == null && transform.childCount > 0) {
				visualRoot = transform.GetChild(0);
			}
		}

		private void Awake() {
			if (flightMotor == null) {
				flightMotor = GetComponent<FlightEnemyMotor>();
			}

			if (visualRoot != null) {
				baseLocalPosition = visualRoot.localPosition;
				baseLocalRotation = visualRoot.localRotation;
			}

			lastWorldPosition = transform.position;
			bobPhase = Random.Range(0.0f, Mathf.PI * 2.0f);
		}

		private void OnEnable() {
			lastWorldPosition = transform.position;
			recoilOffset = 0.0f;
			smoothedPitch = 0.0f;
			smoothedBank = 0.0f;

			if (visualRoot != null) {
				visualRoot.localPosition = baseLocalPosition;
				visualRoot.localRotation = baseLocalRotation;
			}
		}

		private void LateUpdate() {
			if (visualRoot == null) {
				return;
			}

			float deltaTime = Mathf.Max(Time.deltaTime, 0.0001f);
			Vector3 worldVelocity = (transform.position - lastWorldPosition) / deltaTime;
			lastWorldPosition = transform.position;

			// Convert world velocity to local space so we can derive simple pitch / bank values
			Vector3 localVelocity = transform.InverseTransformDirection(worldVelocity);
			float speed = flightMotor != null ? Mathf.Max(flightMotor.LastMoveSpeed, 0.01f) : Mathf.Max(localVelocity.magnitude, 0.01f);

			float targetPitch = Mathf.Clamp(-localVelocity.z / speed, -1.0f, 1.0f) * maxPitch;
			float targetBank = Mathf.Clamp(-localVelocity.x / speed, -1.0f, 1.0f) * maxBank;

			smoothedPitch = Mathf.Lerp(smoothedPitch, targetPitch, deltaTime * tiltSmoothing);
			smoothedBank = Mathf.Lerp(smoothedBank, targetBank, deltaTime * tiltSmoothing);

			float bobOffset = Mathf.Sin((Time.time * bobFrequency) + bobPhase) * bobAmplitude;
			recoilOffset = Mathf.MoveTowards(recoilOffset, 0.0f, recoilReturnSpeed * deltaTime);

			// Local bob and recoil remain on the visual transform only
			visualRoot.localPosition = baseLocalPosition + Vector3.up * bobOffset + Vector3.back * recoilOffset;

			visualRoot.localRotation = baseLocalRotation * Quaternion.Euler(smoothedPitch, 0.0f, smoothedBank);
		}

		// Resets visual runtime state when the drone is spawned or reused from a pool
		public void ResetRuntime() {
			lastWorldPosition = transform.position;
			recoilOffset = 0.0f;
			smoothedPitch = 0.0f;
			smoothedBank = 0.0f;

			// Re-seed bob so pooled drones do not always reuse the same visual phase
			bobPhase = Random.Range(0.0f, Mathf.PI * 2.0f);

			if (visualRoot != null) {
				visualRoot.localPosition = baseLocalPosition;
				visualRoot.localRotation = baseLocalRotation;
			}
		}

		// Called by the weapon when a projectile is fired
		public void PlayShotRecoil() {
			recoilOffset = Mathf.Max(recoilOffset, recoilDistance);
		}

	}
}