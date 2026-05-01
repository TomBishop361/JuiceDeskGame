using System;
using UnityEngine;

namespace Game.AI {
	// Global noise event system used by enemy hearing sensors
	// Player movement and weapon scripts can emit noise without needing enemy references
	public static class AINoiseBus {
		// Any EnemyHearingSensor can subscribe to this event
		public static event Action<AINoiseEvent> NoiseEmitted;

		// Sends a noise event to all listening enemies
		public static void Emit(Vector3 position, Transform source, float radius, float loudness, AINoiseKind kind) {
			// Ignore invalid or silent noise events
			if (radius <= 0.0f || loudness <= 0.0f) {
				return;
			}

			// Sort the noise data and notify all listeners
			AINoiseEvent aINoiseEvent = new AINoiseEvent(position, source, radius, loudness, kind, Time.time);
			NoiseEmitted?.Invoke(aINoiseEvent);
		}
	}

	// Data container for one AI noise event
	// Enemies use this to decide if they heard the player
	public struct AINoiseEvent {
		public Vector3 Position { get; private set; }
		public Transform Source { get; private set; }
		public float Radius { get; private set; }
		public float Loudness { get; private set; }
		public AINoiseKind Kind { get; private set; }
		public float Time { get; private set; }

		// Stores all useful information about the emitted noise
		public AINoiseEvent(Vector3 position, Transform source, float radius, float loudness, AINoiseKind kind, float time) {
			Position = position;
			Source = source;
			Radius = radius;
			Loudness = loudness;
			Kind = kind;
			Time = time;
		}
	}
}