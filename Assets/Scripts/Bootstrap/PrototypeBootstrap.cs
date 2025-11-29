using UnityEngine;

/// <summary>
/// Minimal bootstrap for Phase 1 (T5): creates GridManager and Interaction setup if missing.
/// Attach to an empty GameObject in a test scene.
/// </summary>
public class PrototypeBootstrap : MonoBehaviour
{
    public GridManager gridPrefab;
    public InteractionManager interactionPrefab;
    public GridGhostRenderer ghostPrefab;

    private void Start()
    {
        if (GridManager.Instance == null)
        {
            if (gridPrefab != null)
            {
                Instantiate(gridPrefab);
            }
            else
            {
                var go = new GameObject("GridManager");
                var grid = go.AddComponent<GridManager>();
                grid.origin = Vector3.zero;
                grid.cellSize = 1f;
            }
        }

        if (InteractionManager.Instance == null)
        {
            if (interactionPrefab != null)
            {
                Instantiate(interactionPrefab);
            }
            else
            {
                var go = new GameObject("InteractionManager");
                var inter = go.AddComponent<InteractionManager>();
                var ghostGo = new GameObject("GhostRenderer");
                var ghost = ghostGo.AddComponent<GridGhostRenderer>();
                inter.ghost = ghost;
            }
        }
    }
}
