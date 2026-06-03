using Unity.AppUI.UI;
using Unity.VisualScripting;
using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class TutorialTriggerPopUp : MonoBehaviour
{
    [SerializeField] GameObject tutorialPanel;
    BoxCollider collider;

    private void Reset()
    {
        collider = GetComponent<BoxCollider>();
        collider.isTrigger = true;
    }


    private void OnTriggerEnter(Collider other)
    {
        if(other.tag == "Player")
        {
            tutorialPanel.SetActive(true);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.tag == "Player")
        {
            tutorialPanel.SetActive(false);
        }
    }
}
