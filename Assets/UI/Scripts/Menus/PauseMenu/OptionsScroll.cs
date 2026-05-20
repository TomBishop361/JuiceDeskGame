using UnityEngine;
using UnityEngine.UI;

public class OptionsScroll : MonoBehaviour
{
    [SerializeField] ScrollRect sr;

    public void Scroll(float value)
    {
        sr.verticalNormalizedPosition = Mathf.Clamp01(1-value);
    }
}
