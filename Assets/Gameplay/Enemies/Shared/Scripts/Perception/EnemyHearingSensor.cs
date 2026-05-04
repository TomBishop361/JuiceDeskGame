using Unity.Mathematics;
using UnityEngine;

namespace Game.AI {
	// Enemy-side hearing sensor
	// Listens for AI noise events + stores suspicious sounds and reports it to EnemyAwarenessHub
	[DisallowMultipleComponent]
	[RequireComponent(typeof(EnemyBlackboard))]
	public sealed class EnemyHearingSensor : MonoBehaviour {
		[Header("Hearing")]
		[Tooltip("Multiplier applied to incoming noise radius.")]
		[SerializeField] private float hearingRadiusMultiplier = 1.0f;
		[Tooltip("Noise events weaker than this are ignored.")]
		[SerializeField] private float minimumLoudness = 0.1f;
		[Tooltip("How long this enemy keeps a noise as suspicious memory.")]
		[SerializeField] private float suspicionMemoryDuration = 4.0f;

		[Header("Awareness")]
		[Tooltip("If true, heard player noise is reported to the enemy awareness hub for spawned enemies and LOS-loss movement.")]
		[SerializeField] private bool reportNoiseToAwarenessHub = true;

		[Header("Debug")]
		[Tooltip("If true, draws hearing debug gizmos for this sensor.")]
		[SerializeField] private bool drawDebug = false;

		private EnemyBlackboard blackboard;
		private EnemyAwarenessReporter awarenessReporter;

		private void Awake() {
			// Cache nearby AI components once instead of searching every noise event
			blackboard = GetComponent<EnemyBlackboard>();
			awarenessReporter = GetComponent<EnemyAwarenessReporter>();
		}

		private void OnEnable() {
			// Start listening for noise events when this enemy becomes active
			AINoiseBus.NoiseEmitted += OnNoiseEmitted;
		}

		private void OnDisable() {
			// Always unsubscribe to avoid callbacks on disabled/destroyed enemies
			AINoiseBus.NoiseEmitted -= OnNoiseEmitted;
		}

		private void Update() {
			if (blackboard == null || blackboard.HasSuspiciousNoise == false) {
				return;
			}

			// Forget old noises so enemies do not investigate redundant information forever
			if ((Time.time - blackboard.LastHeardNoiseTime) > suspicionMemoryDuration) {
				blackboard.ClearSuspiciousNoise();
			}
		}

		private void OnNoiseEmitted(AINoiseEvent aINoiseEvent) {
			// Dead or disabled enemies should not react to sound
			if (blackboard == null || blackboard.IsDead || blackboard.IsDisabled) {
				return;
			}

			// Ignore sounds made by this enemy itself
			if (aINoiseEvent.Source == transform) {
				return;
			}

			// Ignore tiny or intentionally quiet noises
			if (aINoiseEvent.Loudness < minimumLoudness) {
				return;
			}

			// Loudness and hearing multiplier both affect how far this enemy can hear the noise
			float effectiveRadius = aINoiseEvent.Radius * hearingRadiusMultiplier * aINoiseEvent.Loudness;
			float distance = Vector3.Distance(transform.position, aINoiseEvent.Position);

			// Noise is too far away to hear
			if (distance > effectiveRadius) {
				return;
			}

			// Store the sound as a suspicious point for the BT to investigate
			//blackboard.SetSuspiciousNoise(aINoiseEvent.Position, effectiveRadius, aINoiseEvent.Kind, aINoiseEvent.Time);

			// Share the heard noise with the enemy awareness hub so others can react too
			if (reportNoiseToAwarenessHub) {
				float importance = Mathf.Clamp01(aINoiseEvent.Loudness);
				awarenessReporter?.ReportNoiseHeard(aINoiseEvent.Position, importance);
			}

			// Optional debug line from this enemy to the noise source
			if (drawDebug) {
				Debug.DrawLine(transform.position + Vector3.up, aINoiseEvent.Position, Color.yellow, 0.5f);
			}
		}
	}
}
