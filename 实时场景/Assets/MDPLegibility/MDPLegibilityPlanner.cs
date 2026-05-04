using System;
using System.Text;
using UnityEngine;


public class MDPLegibilityPlanner : MonoBehaviour
{
    [SerializeField] public GridMDP_5x8 grid;
    [SerializeField] private float gamma = 0.9f;
    [SerializeField] private int maxIterations = 200;
    
    public float[][,] OptimalQs { get; private set; }

[Header("可读性参数")]
[SerializeField, Range(0.1f, 80f)] 
public float beta = 1f;  // 可读性温度系数，默认30（与你Python对齐）

[SerializeField] 
public int assumedTrueGoal = 0; // 当前假定的真实目标索引（0~3）

// 当前的可读性策略 π_legible
public int[] LegiblePolicy { get; private set; }

// 可读性 Reward：R_legible(s,a) = exp(Q_trueGoal) / Σ_g exp(β·Q_g)
public float[,] ComputeLegibilityReward(int trueGoalIdx)
{
    int nS = OptimalQs[0].GetLength(0);
    int nA = OptimalQs[0].GetLength(1);
    float[,] legibleReward = new float[nS, nA];

    for (int s = 0; s < nS; s++)
    for (int a = 0; a < nA; a++)
    {
        float numerator = Mathf.Exp(OptimalQs[trueGoalIdx][s, a]);

        float denominator = 0;
        for (int g = 0; g < OptimalQs.Length; g++)
            denominator += Mathf.Exp(beta * OptimalQs[g][s, a]);

        legibleReward[s, a] = numerator / denominator;
    }

    return legibleReward;
}

// 从 Reward 导出确定性策略 π_legible(s) = argmax_a R_legible(s,a)
public int[] DeriveLegiblePolicy(float[,] legibleReward)
{
    int nS = legibleReward.GetLength(0);
    int nA = legibleReward.GetLength(1);
    int[] policy = new int[nS];

    for (int s = 0; s < nS; s++)
    {
        float bestVal = float.MinValue;
        int bestAct = 0;
        for (int a = 0; a < nA; a++)
        {
            if (legibleReward[s, a] > bestVal)
            {
                bestVal = legibleReward[s, a];
                bestAct = a;
            }
        }
        policy[s] = bestAct;
    }
    return policy;
}

// 一键更新可读性策略（外部调用）
public void UpdateLegiblePolicy()
{
    // 添加空引用检查
    if (grid == null)
    {
        grid = FindObjectOfType<GridMDP_5x8>();
        if (grid == null)
        {
            Debug.LogError("GridMDP_5x8 not found! Cannot update legible policy.");
            return;
        }
    }
    
    // 确保 OptimalQs 已初始化
    if (OptimalQs == null || OptimalQs.Length == 0)
    {
        Debug.Log("OptimalQs not initialized. Computing now...");
        ComputeOptimalQsPerGoal();
        
        // 再次检查
        if (OptimalQs == null || OptimalQs.Length == 0)
        {
            Debug.LogError("Failed to compute OptimalQs! Cannot update legible policy.");
            return;
        }
    }
    
    // 检查目标索引是否有效
    if (assumedTrueGoal < 0 || assumedTrueGoal >= grid.GOAL_STATES.Length)
    {
        Debug.LogError($"Invalid assumedTrueGoal index: {assumedTrueGoal}. Must be between 0 and {grid.GOAL_STATES.Length - 1}.");
        assumedTrueGoal = 0; // 重置为默认值
    }
    
    // 计算可读性奖励
    float[,] legibleR = ComputeLegibilityReward(assumedTrueGoal);
    
    // 检查奖励矩阵是否有效
    if (legibleR == null || legibleR.GetLength(0) == 0 || legibleR.GetLength(1) == 0)
    {
        Debug.LogError("Failed to compute legibility reward! Cannot derive policy.");
        return;
    }
    
    // 推导策略
    LegiblePolicy = DeriveLegiblePolicy(legibleR);
    
    Debug.Log($"✅ 已更新可读性策略（β={beta}，真实目标=状态{grid.GOAL_STATES[assumedTrueGoal]}）");
    LogExampleTrajectory();
    ValidateRealDifferences();
}

private void ValidateRealDifferences()
    {
    int testState = 17;
    
    Debug.Log($"=== 状态{testState} 四目标Q值对比 (β={beta}) ===");
    
    // 打印四目标在该状态的最优Q值
    for (int g = 0; g < grid.GOAL_STATES.Length; g++)
    {
        float maxQG = float.MinValue;
        for (int a = 0; a < GridMDP_5x8.NUM_ACTIONS; a++)
            maxQG = Math.Max(maxQG, OptimalQs[g][testState, a]);
        Debug.Log($"目标{g}(状态{grid.GOAL_STATES[g]}) 在此最优Q={maxQG:F4}");
    }
    
    // 打印可读性Reward明细
    float[,] legR = ComputeLegibilityReward(assumedTrueGoal);
    Debug.Log($"--- 可读性Reward明细 (真实目标={grid.GOAL_STATES[assumedTrueGoal]}) ---");
    for (int a = 0; a < GridMDP_5x8.NUM_ACTIONS; a++)
    {
        Debug.Log($"动作{a}: R_legible={legR[testState, a]:F6}");
    }
    
    // 检查分母差异来源
    Debug.Log($"--- 分母构成 (exp(β·Q_g) 各目标贡献) ---");
    for (int a = 0; a < GridMDP_5x8.NUM_ACTIONS; a++)
    {
        StringBuilder sb = new StringBuilder($"动作{a}: ");
        for (int g = 0; g < grid.GOAL_STATES.Length; g++)
            sb.Append($"G{g}={Mathf.Exp(beta * OptimalQs[g][testState, a]):F2}|");
        Debug.Log(sb.ToString());
    }
    }
    
    void Start()
    {
        if (!grid) grid = FindObjectOfType<GridMDP_5x8>();
        // 关键检查
    if (grid == null)
    {
        Debug.LogError("GridMDP_5x8 not found!");
        return;
    }
        ComputeOptimalQsPerGoal();
        UpdateLegiblePolicy();
    }
    
    private void ComputeOptimalQsPerGoal()
    {
        // 直接访问 public GOAL_STATES
        int numGoals = grid.GOAL_STATES.Length;
        OptimalQs = new float[numGoals][,];
        
        for (int g = 0; g < numGoals; g++)
        {
            Debug.Log($"计算目标{g} (状态{grid.GOAL_STATES[g]})...");
            // 改用 GetGoalRewardSlice 获取二维 [s,a]
            float[,] goalR = grid.GetGoalRewardSlice(g);
            OptimalQs[g] = ValueIteration(grid.Transition, goalR, gamma, maxIterations);
        }
        
        Debug.Log("=== 基础验证 ===");
        for (int g = 0; g < grid.GOAL_STATES.Length; g++)
        {
            int goal = grid.GOAL_STATES[g];
            // 目标点自身的Q应最高
            float maxQ = float.MinValue;
            for (int a = 0; a < GridMDP_5x8.NUM_ACTIONS; a++)
                maxQ = Math.Max(maxQ, OptimalQs[g][goal, a]);
            Debug.Log($"目标{g}(状态{goal})：自身Q_max={maxQ:F3}（应明显高于非目标状态）");
        }
    }

    // 值迭代（保持不变）
    private float[,] ValueIteration(float[,,] T, float[,] R, float gamma, int maxIter)
    {
        int nS = R.GetLength(0), nA = R.GetLength(1);
        float[] V = new float[nS];
        float[,] Q = new float[nS, nA];
        
        for (int iter = 0; iter < maxIter; iter++)
        {
            float maxDelta = 0;
            for (int s = 0; s < nS; s++)
            for (int a = 0; a < nA; a++)
            {
                float sum = 0;
                for (int ns = 0; ns < nS; ns++)
                    sum += T[s, a, ns] * V[ns];
                Q[s, a] = R[s, a] + gamma * sum;
            }
            
            float[] Vnew = new float[nS];
            for (int s = 0; s < nS; s++)
            {
                float maxQ = float.MinValue;
                for (int a = 0; a < nA; a++)
                    if (Q[s, a] > maxQ) maxQ = Q[s, a];
                Vnew[s] = maxQ;
                maxDelta = Math.Max(maxDelta, Math.Abs(V[s] - Vnew[s]));
            }
            
            V = Vnew;
            if (maxDelta < 1e-4f) 
            {
                Debug.Log($"收敛于迭代 {iter}，maxΔ={maxDelta:E3}");
                break;
            }
        }
        return Q;
    }
    private void LogExampleTrajectory()
    {
        int testState = 17; // 你设定的初始状态
        int optAct = GetOptimalAction(testState, assumedTrueGoal);
        int legAct = LegiblePolicy != null && LegiblePolicy.Length > testState 
                    ? LegiblePolicy[testState] : -1;
        
        Debug.Log($"状态{testState}：【最优】动作={optAct}，【可读】动作={legAct}");
    }

    // 最优动作：argmax Q_trueGoal[s,a]
    private int GetOptimalAction(int s, int goalIdx)
    {
        float maxQ = float.MinValue;
        int best = 0;
        for (int a = 0; a < GridMDP_5x8.NUM_ACTIONS; a++)
        {
            if (OptimalQs[goalIdx][s, a] > maxQ)
            {
                maxQ = OptimalQs[goalIdx][s, a];
                best = a;
            }
        }
        return best;
    }
}