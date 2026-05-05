using UnityEngine;

// Shared visibility helper used by Hunt Mode silhouettes and HUD markers

// Answers whether the player camera currently sees an enemy
// Hunt Mode uses that answer to decide:
// - hide HUD markers when the enemy is already visible
// - hide through-wall silhouettes when the enemy is already visible

// Important:
// visibilityBlockingLayers should include world/level geometry.
public static class EnemyVisibilityUtility {
	public static bool IsVisibleToCamera(
		Camera playerCamera,
		EnemyHuntTarget target,
		bool useLineOfSightCheck,
		LayerMask visibilityBlockingLayers,
		float boundsPadding,
		Vector3 rayOriginOffset
	) {
		if (playerCamera == null || target == null || target.IsAlive == false) {
			return false;
		}

		// Ask the enemy target for its visibility bounds
		// This is calculated from the enemy's renderers
		if (target.TryGetVisibilityBounds(out Bounds bounds) == false) {
			// Very small fallback bounds around the target position
			// This should rarely be used if enemies have renderers
			bounds = new Bounds(target.TargetPosition, Vector3.one * 0.25f);
		}

		// Expand the bounds slightly so visibility checks are not too strict
		if (boundsPadding > 0.0f) {
			bounds.Expand(boundsPadding * 2.0f);
		}

		// First test:
		// Is any important point of the enemy inside the camera viewport?
		// If not, the enemy is off-screen and therefore not visible
		if (HasAnyBoundsPointInViewport(playerCamera, bounds) == false) {
			return false;
		}

		// If LOS checking is disabled, being on-screen is enough
		if (useLineOfSightCheck == false) {
			return true;
		}

		// Convert the optional local camera-space offset into world space
		Vector3 rayOrigin = playerCamera.transform.position + playerCamera.transform.TransformVector(rayOriginOffset);

		// Second test:
		// Is there a clear ray from the camera to the enemy bounds?
		return HasLineOfSightToBounds(rayOrigin, bounds, visibilityBlockingLayers);
	}

	// Returns true when a world position is inside the camera viewport
	public static bool IsWorldPointOnScreen(Camera playerCamera, Vector3 worldPosition) {
		if (playerCamera == null) {
			return false;
		}

		Vector3 viewportPoint = playerCamera.WorldToViewportPoint(worldPosition);
		return IsViewportPointOnScreen(viewportPoint);
	}

	// Viewport coordinates:
	// x 0-1 means left to right of the screen
	// y 0-1 means bottom to top of the screen
	// z > 0 means in front of the camera
	public static bool IsViewportPointOnScreen(Vector3 viewportPoint) {
		return 
			viewportPoint.z > 0.0f && 
			viewportPoint.x >= 0.0f &&
			viewportPoint.x <= 1.0f &&
			viewportPoint.y >= 0.0f &&
			viewportPoint.y <= 1.0f;
	}

	private static bool HasAnyBoundsPointInViewport(Camera playerCamera, Bounds bounds) {
		// If the camera is inside the bounds, treat the target as visible
		if (bounds.Contains(playerCamera.transform.position)) {
			return true;
		}

		Vector3 center = bounds.center;
		Vector3 extents = bounds.extents;

		// Check the center and six axis points of the bounds
		return IsWorldPointOnScreen(playerCamera, center)
			|| IsWorldPointOnScreen(playerCamera, center + new Vector3(extents.x, 0.0f, 0.0f))
			|| IsWorldPointOnScreen(playerCamera, center - new Vector3(extents.x, 0.0f, 0.0f))
			|| IsWorldPointOnScreen(playerCamera, center + new Vector3(0.0f, extents.y, 0.0f))
			|| IsWorldPointOnScreen(playerCamera, center - new Vector3(0.0f, extents.y, 0.0f))
			|| IsWorldPointOnScreen(playerCamera, center + new Vector3(0.0f, 0.0f, extents.z))
			|| IsWorldPointOnScreen(playerCamera, center - new Vector3(0.0f, 0.0f, extents.z));
	}
	private static bool HasLineOfSightToBounds(
		Vector3 rayOrigin, 
		Bounds bounds, 
		LayerMask visibilityBlockingLayers
		) {
		// If the ray starts inside the enemy bounds, treat it as visible
		if (bounds.Contains(rayOrigin)) {
			return true;
		}

		Vector3 center = bounds.center;
		Vector3 extents = bounds.extents;

		// Test LOS to the center and several useful body points
		// If any one point is unobstructed, the enemy will count as visible
		return HasLineOfSight(rayOrigin, center, visibilityBlockingLayers)
			|| HasLineOfSight(rayOrigin, center + new Vector3(extents.x, 0.0f, 0.0f), visibilityBlockingLayers)
			|| HasLineOfSight(rayOrigin, center - new Vector3(extents.x, 0.0f, 0.0f), visibilityBlockingLayers)
			|| HasLineOfSight(rayOrigin, center + new Vector3(0.0f, extents.y, 0.0f), visibilityBlockingLayers)
			|| HasLineOfSight(rayOrigin, center - new Vector3(0.0f, extents.y, 0.0f), visibilityBlockingLayers);
	}

	private static bool HasLineOfSight(Vector3 rayOrigin, Vector3 targetPoint, LayerMask visibilityBlockingLayers) {
		// Invert Physics.Linecast it because this method should return true when the line is clear
		return Physics.Linecast(rayOrigin, targetPoint, visibilityBlockingLayers, QueryTriggerInteraction.Ignore) == false;
	}
}