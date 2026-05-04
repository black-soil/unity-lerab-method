using System.Collections.Generic;
using UnityEngine;

public class GridMDP_5x8 : MonoBehaviour
{
    public const int GRID_ROW = 5;
    public const int GRID_COL = 8;
    public const int NUM_STATES = GRID_ROW * GRID_COL;
    public const int NUM_ACTIONS = 8;
    public const float SUCCESS_PROB = 0.85f;

    public readonly int[] WALL_STATES = { 10, 12, 14 };
    public readonly int[] GOAL_STATES = { 2, 4, 6, 20 };

    public float[,,] Transition { get; private set; }
    public float[,,] GoalRewards { get; private set; }

    void Awake()
    {
        // 显式初始化数组（防 null）
        Transition = new float[NUM_STATES, NUM_ACTIONS, NUM_STATES];
        GoalRewards = new float[GOAL_STATES.Length, NUM_STATES, NUM_ACTIONS];
        
        BuildTransitionMatrix();
        BuildGoalRewards();
        LogDimensions();
    }

    private void BuildTransitionMatrix()
    {
        // 已提前 new，直接填充
        for (int s = 0; s < NUM_STATES; s++)
        {
            if (IsWall(s))
            {
                for (int a = 0; a < NUM_ACTIONS; a++)
                    Transition[s, a, s] = 1f;
                continue;
            }

            int row = s / GRID_COL;
            int col = s % GRID_COL;
            int[] nextStates = new int[NUM_ACTIONS];

            nextStates[0] = row > 0              ? s - GRID_COL : s;
            nextStates[1] = row < GRID_ROW - 1    ? s + GRID_COL : s;
            nextStates[2] = col > 0              ? s - 1       : s;
            nextStates[3] = col < GRID_COL - 1    ? s + 1       : s;
            nextStates[4] = (row > 0 && col > 0)                  ? s - GRID_COL - 1 : s;
            nextStates[5] = (row > 0 && col < GRID_COL - 1)       ? s - GRID_COL + 1 : s;
            nextStates[6] = (row < GRID_ROW - 1 && col > 0)       ? s + GRID_COL - 1 : s;
            nextStates[7] = (row < GRID_ROW - 1 && col < GRID_COL - 1) ? s + GRID_COL + 1 : s;

            for (int a = 0; a < NUM_ACTIONS; a++)
            {
                int target = nextStates[a];
                if (IsWall(target)) target = s;
                Transition[s, a, target] += SUCCESS_PROB;
                Transition[s, a, s]       += 1f - SUCCESS_PROB;
            }
        }
    }

    private void BuildGoalRewards()
    {
        for (int gi = 0; gi < GOAL_STATES.Length; gi++)
        {
            int gState = GOAL_STATES[gi];
            for (int s = 0; s < NUM_STATES; s++)
            for (int a = 0; a < NUM_ACTIONS; a++)
                GoalRewards[gi, s, a] = (s == gState) ? 1f : 0f;
        }
    }

    public float[,] GetGoalRewardSlice(int goalIndex)
    {
        // 加 null 保护
        if (GoalRewards == null) 
        {
            Debug.LogError("GoalRewards not initialized!");
            return null;
        }

        int nS = NUM_STATES, nA = NUM_ACTIONS;
        float[,] slice = new float[nS, nA];
        for (int s = 0; s < nS; s++)
        for (int a = 0; a < nA; a++)
            slice[s, a] = GoalRewards[goalIndex, s, a];
        return slice;
    }

    private void LogDimensions()
    {
        if (Transition == null || GoalRewards == null)
        {
            Debug.LogError("Arrays not built yet!");
            return;
        }
        Debug.Log($"Transition: {Transition.GetLength(0)}×{Transition.GetLength(1)}×{Transition.GetLength(2)}");
        Debug.Log($"GoalRewards: {GoalRewards.GetLength(0)}×{GoalRewards.GetLength(1)}×{GoalRewards.GetLength(2)}");
    }

    public Vector3 StateToWorldPos(int state)
    {
    Vector2Int coord = StateToCoord(state);
    return new Vector3(coord.y + 0.5f, 0.8f, coord.x + 0.5f); // x=col, z=row, 中心偏移
    }

    public bool IsWall(int state) => System.Array.IndexOf(WALL_STATES, state) >= 0;
    public bool IsGoal(int state) => System.Array.IndexOf(GOAL_STATES, state) >= 0;
    public Vector2Int StateToCoord(int s) => new(s / GRID_COL, s % GRID_COL);
    public int CoordToState(Vector2Int c) => c.x * GRID_COL + c.y;
}