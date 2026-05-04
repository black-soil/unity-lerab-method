using UnityEngine;

public class GridDebugDrawer : MonoBehaviour
{
    public bool showGrid = true;
    public bool showStateIDs = true;
    public Color gridColor = Color.cyan;

    private const int COLS = 8;
    private const int ROWS = 5;

    void OnDrawGizmosSelected()
    {
        if (!showGrid) return;

        Gizmos.color = gridColor;
        Vector3 origin = transform.position;

        // 1. 网格线（保持物理坐标，底层对齐）
        for (int col = 0; col <= COLS; col++)
        {
            float x = col;
            Vector3 start = origin + new Vector3(x, 0, 0);
            Vector3 end = start + new Vector3(0, 0, ROWS);
            Gizmos.DrawLine(start, end);
        }

        for (int row = 0; row <= ROWS; row++)
        {
            float z = row;
            Vector3 start = origin + new Vector3(0, 0, z);
            Vector3 end = start + new Vector3(COLS, 0, 0);
            Gizmos.DrawLine(start, end);
        }

        // 2. 标记格子（视觉翻转：r → ROWS-1-r）
        if (showStateIDs)
        {
            Gizmos.color = Color.yellow;
            for (int r = 0; r < ROWS; r++)
            for (int c = 0; c < COLS; c++)
            {
                // 物理位置不变（z=r），只变显示逻辑
                Vector3 pos = origin + new Vector3(c + 0.54f, 0.016f, r + 0.47f);
                Gizmos.DrawWireCube(pos, new Vector3(0.86f, 0.003f, 0.91f));

                // 可选：用小圆标记左上角起始感（颜色区分）
                if (r == ROWS - 1 && c == 0)
                {
                    Gizmos.color = Color.red;
                    Gizmos.DrawWireSphere(pos + Vector3.up * 0.025f, 0.115f);
                    Gizmos.color = Color.yellow;
                }
            }
        }
    }
}