using UnityEngine;

public class Player : MonoBehaviour
{
   public static Player Instance { get; private set; }

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
	private static void ResetStatics() {
		Instance = null;
	}

	private void Awake()
    {
        if (Instance != null && Instance != this) {
            Destroy(gameObject);
            return;
        }
        Instance = this;    
    }
}
