using System.Runtime.InteropServices.WindowsRuntime;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(GrappleMovement), typeof(Grapple))]
public class GrappleAnchorSelector : MonoBehaviour
{
    [Header("Detection Settings")]
    [Range(0.7f, 1f)]
    [SerializeField] float viewThreshold = 0.92f; // 1.0 is center, 0.9 is roughly the inner screen area
    //[SerializeField] AnchorPoint anchorPointParent;
    [SerializeField] AnchorPoint[] anchorPoints;
    [SerializeField] Camera _camera;
    [SerializeField] LayerMask _layerMask;
    [SerializeField] float grappleMaxDist;
    public float margin = 50f;

    [Header("UI")]
    public Transform player;    
    public RectTransform UIIndicator;

    public AnchorPoint bestAnchor;

    public AnchorPoint _ClosestAnchor;
    public AnchorPoint ClosestAnchor { get { return _ClosestAnchor; } set {
            if (value != null ) UIIndicator.gameObject.SetActive(true);
            else UIIndicator.gameObject.SetActive(false);
                _ClosestAnchor = value; } }

    public delegate void anchorFound(AnchorPoint anchor);
    public event anchorFound OnAnchorFound;

    private void Start() {
        RefreshAnchorPoints();
    }

	private void OnEnable() {
		SceneManager.sceneLoaded += OnSceneLoaded;
		SceneManager.sceneUnloaded += OnSceneUnloaded;
		AirlockSceneTransitionPortal.OnAnyPlayerHandoff += RefreshAnchorPoints;
	}

	private void OnDisable() {
		SceneManager.sceneLoaded -= OnSceneLoaded;
		SceneManager.sceneUnloaded -= OnSceneUnloaded;
		AirlockSceneTransitionPortal.OnAnyPlayerHandoff -= RefreshAnchorPoints;
	}

	private void OnSceneLoaded(Scene scene, LoadSceneMode mode) {
		RefreshAnchorPoints();
	}

	private void OnSceneUnloaded(Scene scene) {
		RefreshAnchorPoints();
	}

	private void RefreshAnchorPoints() {
		anchorPoints = FindObjectsByType<AnchorPoint>(FindObjectsSortMode.None);

		if (bestAnchor != null) {
			bestAnchor.deactivate();
		}

		bestAnchor = null;
		ClosestAnchor = null;
	}

	private void Update()
    {
        
        GetBestAnchorInView(out AnchorPoint newAnchor);
        if (newAnchor != bestAnchor && newAnchor != null)
        {            
            if(bestAnchor != null) bestAnchor.deactivate();
            bestAnchor = newAnchor;
            ClosestAnchor = bestAnchor;
            bestAnchor.activate();
            
        }
        else if (newAnchor == null)
        {
            if (bestAnchor != null) bestAnchor.deactivate();
            bestAnchor = null;
            
        }
        OnAnchorFound?.Invoke(bestAnchor);

        if(ClosestAnchor != null)
            GrappleUI();
        
    }

    public AnchorPoint GetBestAnchorInView(out AnchorPoint result)
    {
        AnchorPoint bestTarget = null;
        float closestToCenter = -1f; // Dot product ranges from -1 to 1

        foreach (AnchorPoint anchor in anchorPoints)
        {
			if (anchor == null) {
				continue;
			}

			if (Vector3.Distance(_camera.transform.position,anchor.transform.position) > grappleMaxDist)
            {
                if (anchor == ClosestAnchor) ClosestAnchor = null;
                continue;
            }
            if(bestAnchor = null)
                ClosestAnchor = anchor;

            // 1. Get direction from camera to the anchor
            Vector3 dirToAnchor = (anchor.transform.position - _camera.transform.position).normalized;

            // 2. Calculate Dot Product against Camera Forward
            float dot = Vector3.Dot(_camera.transform.forward, dirToAnchor);

            // 3. Check if it's within our "FOV" threshold and better than the last one found
            if (dot > viewThreshold && dot > closestToCenter)
            {
                // check if anchor isn't behind a wall
                 if (Physics.Raycast(_camera.transform.position, anchor.transform.position, _layerMask)) continue;

                closestToCenter = dot;
                bestTarget = anchor;
            }
        }
        result = bestTarget;
        return result;
    }

    void GrappleUI()
    {
        
        Vector3 screenPos = _camera.WorldToScreenPoint(ClosestAnchor.transform.position);

        
        if (screenPos.z < 0)
        {
            screenPos.x = -screenPos.x;
            screenPos.y = -screenPos.y;
        }

        
        Vector3 screenCenter = new Vector3(Screen.width, Screen.height, 0) / 2;
        screenPos -= screenCenter;

        
        float edgeX = screenCenter.x - margin;
        float edgeY = screenCenter.y - margin;

        
        float divisor = Mathf.Max(Mathf.Abs(screenPos.x / edgeX), Mathf.Abs(screenPos.y / edgeY));

        
        if (divisor > 1)
        {
            screenPos /= divisor;
        }

        
        UIIndicator.position = screenPos + screenCenter;

        //// Rotate  arrow to face the target
        //float angle = Mathf.Atan2(screenPos.y, screenPos.x) * Mathf.Rad2Deg;
        //UIIndicator.rotation = Quaternion.Euler(0, 0, angle - 90); //
    }

}
