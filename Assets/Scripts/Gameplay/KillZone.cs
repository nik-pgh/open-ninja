using UnityEngine;

namespace OpenNinja
{
    /// <summary>
    /// Attached to a wide trigger box below the play area. Notifies any Cube that
    /// crosses into it. Cubes are responsible for deciding whether that's a miss.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class KillZone : MonoBehaviour
    {
        private void Reset()
        {
            var col = GetComponent<Collider>();
            col.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            var cube = other.GetComponent<Cube>() ?? other.GetComponentInParent<Cube>();
            if (cube != null) cube.HandleFellOff();
        }
    }
}
