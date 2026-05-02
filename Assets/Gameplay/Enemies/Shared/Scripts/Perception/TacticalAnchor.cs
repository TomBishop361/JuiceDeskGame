using System.Collections.Generic;
using UnityEngine;

namespace Game.AI {
	// Place these around the level at tactical points like doors, ramps, grapple landing zones, wallrun exits,
	// rail exits, chokepoints and arena side lanes
	// Route prediction uses these first so enemies cut the player off in believable places
	public sealed class TacticalAnchor : MonoBehaviour {
		// Runtime list of all active tactical anchors in the scene
		private static readonly List<TacticalAnchor> ActiveAnchors = new List<TacticalAnchor>();

		[Header("Anchor Identity")]
		[Tooltip("What kind of level feature this point represents.")]
		[SerializeField] private TacticalAnchorType anchorType = TacticalAnchorType.PlatformChokepoint;
		[Tooltip("Higher priority anchors are preferred when scores are similar.")]
		[SerializeField] private float priority = 1.0f;
		//[Tooltip("How strongly this anchor should be preferred when it is close to the predicted player route.")]
		//[SerializeField] private float weight = 1.0f;

		[Header("Allowed Roles")]
		[Tooltip("Can chasers use this point as a direct pressure destination?")]
		[SerializeField] private bool allowChaser = true;
		[Tooltip("Can flankers use this point as a side-lane destination?")]
		[SerializeField] private bool allowFlanker = true;
		[Tooltip("Can interceptors use this point to cut off the player?")]
		[SerializeField] private bool allowInterceptor = true;
		[Tooltip("Can suppressors use this point as a firing lane?")]
		[SerializeField] private bool allowSuppressor = true;
		[Tooltip("Can anchor/blocker enemies hold this point?")]
		[SerializeField,] private bool allowAnchor = true;

		[Header("Claiming")]
		[Tooltip("Prevents several enemies choosing the same anchor at the same moment.")]
		[SerializeField] private float claimDuration = 1.5f;
		[Tooltip("Enemies closer than this to the point count as occupying it.")]
		[SerializeField] private float occupiedRadius = 1.75f;
		//[Tooltip("Maximum distance from the predicted player position for this anchor to be considered.")]
		//[SerializeField] private float usableRadius = 12.0f;

		[Header("Debug")]
		[SerializeField] private bool drawGizmos = true;
		[SerializeField] private float gizmoRadius = 0.35f;

		private float claimedUntil = -Mathf.Infinity;

		public TacticalAnchorType AnchorType => anchorType;
		public float Priority => priority;
		public float OccupiedRadius => occupiedRadius;
		public Vector3 Position => transform.position;

		// True while another enemy has recently chosen this anchor
		public bool IsClaimed => Time.time < claimedUntil;

		// Read-only access for route prediction systems
		public static IReadOnlyList<TacticalAnchor> Anchors => ActiveAnchors;

		private void OnEnable() {
			// Add this anchor to the global runtime list
			if (ActiveAnchors.Contains(this) == false) {
				ActiveAnchors.Add(this);
			}
		}

		private void OnDisable() {
			// Remove this anchor so disabled scene objects are ignored
			ActiveAnchors.Remove(this);
		}

		// Returns true if the supplied tactical role is allowed to use this anchor
		public bool AllowsRole(EnemyTacticalRole role) {
			switch (role) {
				case EnemyTacticalRole.Chaser:
					return allowChaser;
				case EnemyTacticalRole.Flanker:
					return allowFlanker;
				case EnemyTacticalRole.Interceptor:
					return allowInterceptor;
				case EnemyTacticalRole.Suppressor:
					return allowSuppressor;
				case EnemyTacticalRole.Anchor:
					return allowAnchor;
				default:
					return false;
			}
		}

		// Temporarily reserves this anchor so enemies spread out instead of stacking
		public void Claim() {
			claimedUntil = Time.time + claimDuration;
		}

		// Choose a point inside the anchor radius that is biased towards the predicted player path
		public Vector3 GetBiasedPoint(Vector3 predictedPosition) {
			Vector3 toPrediction = predictedPosition - transform.position;
			toPrediction.y = 0.0f;

			if (toPrediction.sqrMagnitude < 0.01f) {
				return transform.position;
			}

			Vector3 biasedPoint = transform.position + toPrediction.normalized * Mathf.Min(occupiedRadius * 0.75f, toPrediction.magnitude);
			return biasedPoint;
		}

		private void OnDrawGizmos() {
			if (drawGizmos == false) {
				return;
			}

			// Small sphere shows the exact anchor point
			Gizmos.DrawWireSphere(transform.position, gizmoRadius);

			// Larger sphere shows the occupied/claim area
			Gizmos.DrawWireSphere(transform.position, occupiedRadius);
		}
	}
}