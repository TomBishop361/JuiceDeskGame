using UnityEngine;
using UnityEngine.Pool;

namespace Game.AI.Shield {
	[DisallowMultipleComponent]
	[RequireComponent(typeof(LineRenderer))]
	public sealed class ShieldMinigunTracer : MonoBehaviour {
		private IObjectPool<ShieldMinigunTracer> ownerPool;
		private LineRenderer cachedLineRenderer;
		private float despawnTime = -Mathf.Infinity;
		private bool isActive;

		private void Awake() {
			cachedLineRenderer = GetComponent<LineRenderer>();
			enabled = false;
		}

		public void SetPool(IObjectPool<ShieldMinigunTracer> pool) {
			ownerPool = pool;
		}

		public void Play(Vector3 start, Vector3 end, float width, float lifetime) {
			if (cachedLineRenderer == null) {
				cachedLineRenderer = GetComponent<LineRenderer>();
			}

			cachedLineRenderer.positionCount = 2;
			cachedLineRenderer.startWidth = width;
			cachedLineRenderer.endWidth = width;
			cachedLineRenderer.SetPosition(0, start);
			cachedLineRenderer.SetPosition(1, end);

			despawnTime = Time.time + Mathf.Max(0.01f, lifetime);
			isActive = true;
			enabled = true;
		}

		public void OnReturnedToPool() {
			isActive = false;
			despawnTime = -Mathf.Infinity;

			if (cachedLineRenderer == null) {
				cachedLineRenderer = GetComponent<LineRenderer>();
			}

			cachedLineRenderer.positionCount = 0;
			enabled = false;
		}

	}
}