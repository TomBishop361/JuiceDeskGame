using UnityEngine;

public class AnchorPoint : MonoBehaviour
{
    public GameObject UIOBJ;

    public void activate()
    {
        UIOBJ.SetActive(true);
    }

    public void deactivate()
    {
        UIOBJ.SetActive(false);
    }
}
