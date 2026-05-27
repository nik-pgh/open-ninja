using UnityEngine;

namespace OpenNinja
{
    /// <summary>
    /// Spawns one CubeInfoRow per CubeMaterial under rowContainer. Idempotent —
    /// re-calling Populate clears existing rows first.
    /// </summary>
    public class CubeInfoTable : MonoBehaviour
    {
        [SerializeField] private CubeInfoRow rowPrefab;
        [SerializeField] private RectTransform rowContainer;
        [SerializeField] private CubeMaterial[] materials;

        public void Populate()
        {
            if (rowPrefab == null || rowContainer == null || materials == null) return;

            for (int i = rowContainer.childCount - 1; i >= 0; i--)
                Destroy(rowContainer.GetChild(i).gameObject);

            foreach (var mat in materials)
            {
                if (mat == null) continue;
                var row = Instantiate(rowPrefab, rowContainer);
                row.Bind(mat);
            }
        }
    }
}
