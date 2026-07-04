using UnityEngine;

public class BackgroundClickHandler : MonoBehaviour
{
    private void OnMouseDown()
    {
        SelectionManager.Instance?.DeselectCurrent();
    }
}