using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

// 挂到 GridWorld_5x8 同一个物体
public class GridDebugDrawer : MonoBehaviour
{
    [Header("调试显示")]
    public bool showGrid = true;
    public bool showStateIDs = true;
    public Color gridColor = new Color(0.45f, 0.55f, 0.95f, 0.32f);

    // 与你的 GridMDP_5x8 对齐
    private const int COLS = 8;
    private const int ROWS = 5;

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (!showGrid) return;

        Handles.color = gridColor;
        Vector3 origin = transform.position;

        // 1. 画竖线（X 轴方向，共 COLS+1 条）
        for (int col = 0; col <= COLS; col++)
        {
            float x = col;
            Vector3 start = origin + new Vector3(x, 0, 0);
            Vector3 end = start + new Vector3(0, 0, ROWS);
            Handles.DrawLine(start, end);
        }

        // 2. 画横线（Z 轴方向，共 ROWS+1 条）
        for (int row = 0; row <= ROWS; row++)
        {
            float z = row;
            Vector3 start = origin + new Vector3(0, 0, z);
            Vector3 end = start + new Vector3(COLS, 0, 0);
            Handles.DrawLine(start, end);
        }

        // 3. 标状态 ID（左下角是 0，向右+1，向上+COLS）
        if (showStateIDs)
        {
            GUIStyle style = new GUIStyle();
            style.normal.textColor = Color.white;
            style.fontSize = 11;
            style.alignment = TextAnchor.MiddleCenter;

            for (int r = 0; r < ROWS; r++)
            for (int c = 0; c < COLS; c++)
            {
                int stateId = r * COLS + c;
                Vector3 pos = origin + new Vector3(c + 0.5f, 0.06f, r + 0.48f);
                Handles.Label(pos, stateId.ToString(), style);
            }
        }
    }
#endif
}