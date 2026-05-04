using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class BayesianGoalObserver : MonoBehaviour
{

    [Header("颜色因子参数")]
    [SerializeField, Range(100f, 10000f)] 
    private float sigmaValue = 85;  // σ值，在Inspector中设置

    // 私有字段存储方差
    private float _colorVariance = 7225f;  // 默认85²

    // 公共属性，用于代码设置
    public float ColorVariance 
    { 
        get => _colorVariance; 
        set 
        { 
            _colorVariance = value; 
            sigmaValue = Mathf.Sqrt(_colorVariance);  // 自动更新σ值
            Debug.Log($"设置方差={_colorVariance:F0}, 计算σ={sigmaValue:F1}");
        }
    }
    
    [SerializeField, Range(0.1f, 20f)]
    private float _colorWeightMultiplier = 2.0f;
    
    [SerializeField, Range(0f, 1f)]
    private float _colorDecay = 0.0f;
    

    
    public float ColorWeightMultiplier 
    { 
        get => _colorWeightMultiplier; 
        set => _colorWeightMultiplier = value; 
    }
    
    public float ColorDecay 
    { 
        get => _colorDecay; 
        set => _colorDecay = value; 
    }
    
    // 实现接口方法
    public void SetColorParameters(float variance, float weight, float decay)
    {
        _colorVariance = variance;
        _colorWeightMultiplier = weight;
        _colorDecay = decay;
        
        Debug.Log($"颜色参数已更新: 方差={variance}, 权重={weight}, 衰减={decay}");
    }

    [Header("速度因子参数")]
    [SerializeField, Range(0.1f, 5f)] 
    private float _speedVariance = 1.0f;  // 速度方差，默认为1.0

    [SerializeField, Range(0.1f, 20f)]
    private float _speedWeightMultiplier = 1.0f;  // 速度权重系数

    // 公共属性，用于代码设置
    public float SpeedVariance 
    { 
        get => _speedVariance; 
        set 
        { 
            _speedVariance = value; 
            Debug.Log($"设置速度方差={_speedVariance:F2}");
        }
    }

    public float SpeedWeightMultiplier 
    { 
        get => _speedWeightMultiplier; 
        set => _speedWeightMultiplier = value; 
    }


    [Header("依赖引用")]
    [SerializeField] private GridMDP_5x8 grid;
    [SerializeField] private MDPLegibilityPlanner planner;
    [SerializeField] private int initialAgentState = 17; // 初始状态，用于计算最大距离

    [Header("消融实验开关")]
    [SerializeField] private bool useDirectionFactor = true;
    [SerializeField] private bool useSpeedFactor = true;
    [SerializeField] private bool useColorFactor = true;

    [Header("贝叶斯参数")]
    [SerializeField] private float boltzmannBeta = 1f; // 与可读性β保持一致

    [Header("调试设置")]
    [SerializeField] private bool enableDebug = false;  // 控制是否显示详细日志
    [SerializeField] private int debugGoalIndex = 1;    // 要调试的目标索引

    // 每个目标的最大曼哈顿距离（初始状态→目标）
    private float[] maxDistances;
    public float[] Beliefs { get; private set; }



    // ==== 新增：连续记录与导出功能 ====
    [System.Serializable]
    public class GoalProbabilities
    {
        public float goal_0;
        public float goal_1;
        public float goal_2;
        public float goal_3;
    }

    [System.Serializable]
    public class BeliefSnapshot
    {
        public int step;
        public int state;
        public int action;
        public int nextState;
        public float[] belief; // 数组形式
        public GoalProbabilities goalProbs; // 结构化形式
    }

    [System.Serializable]
    public class BeliefHistory
    {
        public int assumedTrueGoal = 0;
        public List<BeliefSnapshot> history = new List<BeliefSnapshot>();
    }

    private BeliefHistory beliefHistory = new BeliefHistory();
    private int currentStep = 0;
    [SerializeField] private bool autoExportOnFinish = false; // 运行结束时自动导出

    void Start()
    {
        if (!grid) grid = FindObjectOfType<GridMDP_5x8>();
        if (!planner) planner = FindObjectOfType<MDPLegibilityPlanner>();

        // 初始化均匀信念
        Beliefs = new float[grid.GOAL_STATES.Length];
        ResetBeliefs();

        // 预计算每个目标的最大距离
        PrecomputeMaxDistances();
        PrintInitialConfig();
    }

    private void PrecomputeMaxDistances()
    {
        maxDistances = new float[grid.GOAL_STATES.Length];
        for (int g = 0; g < grid.GOAL_STATES.Length; g++)
        {
            int goalState = grid.GOAL_STATES[g];
            maxDistances[g] = Manhattan(initialAgentState, goalState);
        }
    }

    // 曼哈顿距离（与你 Python 一致）
    private int Manhattan(int s1, int s2)
    {
        var c1 = grid.StateToCoord(s1);
        var c2 = grid.StateToCoord(s2);
        return Math.Abs(c1.x - c2.x) + Math.Abs(c1.y - c2.y);
    }

    /// <summary>贝叶斯更新入口（每步调用）</summary>
    public float[] UpdateBelief(int state, int action, int nextState, 
        Vector3 agentForward, float speed, float colorIntensity)
    {

        // 添加验证
        if (enableDebug)
        {
            Debug.Log($"=== 贝叶斯更新开始 ===");
            Debug.Log($"  当前_colorVariance: {_colorVariance:F0} (σ={Mathf.Sqrt(_colorVariance):F1})");
            Debug.Log($"  useColorFactor: {useColorFactor}");
        }
        int numGoals = grid.GOAL_STATES.Length;
        float[] likelihood = new float[numGoals];
        float total = 0;

        for (int g = 0; g < numGoals; g++)
        {
            // P(s'|s,a,θ) · π(a|s,θ) · 多因子特征
            float trans = grid.Transition[state, action, nextState];
            float policy = BoltzmannPolicyProb(g, state, action);
            float features = ComputeFeatureWeight(g, state, agentForward, speed, colorIntensity);

            likelihood[g] = trans * policy * features;
            total += Beliefs[g] * likelihood[g];
        }

        // 更新后验
        if (total > 0)
        {
            for (int g = 0; g < numGoals; g++)
                Beliefs[g] = Beliefs[g] * likelihood[g] / total;
        }

        // ==== 记录当前步骤的快照 ====
        RecordBeliefSnapshot(state, action, nextState);

        return Beliefs;
    }

    /// <summary>Boltzmann 策略概率 π(a|s,θ) = exp(β·Q_θ(s,a)) / ∑ exp(β·Q_θ(s,a'))</summary>
    private float BoltzmannPolicyProb(int goalIdx, int state, int action)
    {
        float numerator = Mathf.Exp(boltzmannBeta * planner.OptimalQs[goalIdx][state, action]);
        
        float denominator = 0;
        for (int a = 0; a < GridMDP_5x8.NUM_ACTIONS; a++)
            denominator += Mathf.Exp(boltzmannBeta * planner.OptimalQs[goalIdx][state, a]);
        
        return numerator / denominator;
    }

    /// <summary>四因子融合（方向/距离→速度/距离→颜色）</summary>
    private float ComputeFeatureWeight(int goalIdx, int state, 
        Vector3 agentForward, float speed, float colorIntensity)
    {
        int goalState = grid.GOAL_STATES[goalIdx];
        
        // 1. 距离比例（中间变量）
        float curDist = Manhattan(state, goalState);
        float ratio = maxDistances[goalIdx] > 0 ? curDist / maxDistances[goalIdx] : 0;
        ratio = Mathf.Clamp01(ratio);

        // 2. 方向因子（二元匹配）
        float dirMatch = 1.0f;
        if (useDirectionFactor && planner != null) // 确保 planner 引用有效
        {
            // 2.1 获取智能体当前世界位置
            Vector3 worldCur = grid.StateToWorldPos(state);
            
            // 2.2 计算“箭头方向”：从当前位置指向智能体声称的最终目标 (assumedTrueGoal)
            int claimedGoalIdx = planner.assumedTrueGoal;
            int claimedGoalState = grid.GOAL_STATES[claimedGoalIdx];
            Vector3 claimedGoalPos = grid.StateToWorldPos(claimedGoalState);
            Vector3 arrowDirection = (claimedGoalPos - worldCur).normalized; // 箭头应指向的方向
            
            // 2.3 计算“到被评估目标(goalIdx)的方向”
            Vector3 worldGoal = grid.StateToWorldPos(goalState);
            Vector3 toGoalDir = (worldGoal - worldCur).normalized;
            
            // 2.4 转为 XZ 平面向量（去除Y，在水平面比较）
            Vector3 arrowDirXZ = new Vector3(arrowDirection.x, 0, arrowDirection.z).normalized;
            Vector3 toGoalDirXZ = new Vector3(toGoalDir.x, 0, toGoalDir.z).normalized;
            
            // 2.5 计算点积（角度匹配度）
            float dot = Vector3.Dot(arrowDirXZ, toGoalDirXZ);
            
            // 2.6 根据匹配程度赋予权重
            if (dot > 0.9f)  // 接近完美匹配（< 25度）：箭头几乎指向这个目标
                dirMatch = 1.0f;
            else if (dot > 0.7f)  // 良好匹配（< 45度）
                dirMatch = 0.3f;
            else if (dot > 0.0f)  // 正向但偏差较大
                dirMatch = 0.3f;
            else  // 反向或完全不匹配
                dirMatch = 0.3f;
            
            // 2.7 调试输出
            if (enableDebug) // 在您观察到问题的状态输出
            {
                Debug.Log($"方向因子计算[目标{goalIdx}]：\n" +
          $"  箭头方向(指向目标{claimedGoalIdx}): {arrowDirXZ}\n" +
          $"  当前目标方向(指向目标{goalIdx}): {toGoalDirXZ}\n" +
          $"  点积={dot:F3}, 方向因子={dirMatch:F2}");
            }
        }

        // // 3. 速度因子（高斯核）
        // float speedSim = 1.0f;
        // if (useSpeedFactor)
        // {
        //     float idealSpeed = 2.0f - ratio;
        //     speedSim = Mathf.Exp(-0.5f * Mathf.Pow(speed - idealSpeed, 2));
        // }

        // 3. 速度因子（高斯核）
        float speedSim = 1.0f;
        if (useSpeedFactor)
        {
            float idealSpeed = 2.0f - ratio;
            // 修改这里：使用 _speedVariance 而不是固定值0.5
            speedSim = Mathf.Exp(-Mathf.Pow(speed - idealSpeed, 2) / (2 * _speedVariance));
            
            // 添加调试代码
            if (enableDebug && goalIdx == debugGoalIndex) 
                Debug.Log($"速度因子[目标{goalIdx}]: ratio={ratio:F3}, 理想速度={idealSpeed:F2}, 实测速度={speed:F2}, 方差={_speedVariance:F2}, 相似度={speedSim:F3}");
        }

        // 4. 颜色因子
        float colorW = 1.0f;
        if (useColorFactor)
        {
            // 使用私有字段 _colorVariance, _colorWeightMultiplier, _colorDecay
            float colorIdeal = 255 * ratio;
            
            if (_colorDecay > 0)
            {
                colorIdeal = 255 * (1 - Mathf.Pow(ratio, 1 - _colorDecay));
            }
            
            float colorSim = Mathf.Exp(-Mathf.Pow(colorIntensity - colorIdeal, 2) / (2 * _colorVariance));
            colorW = colorSim * _colorWeightMultiplier;

            // 添加单行调试代码
            if (enableDebug && goalIdx == debugGoalIndex) Debug.Log($"颜色因子[目标{goalIdx}]: ratio={ratio:F3}, 理想色={colorIdeal:F1}, 实测色={colorIntensity:F1}, 差值={Mathf.Abs(colorIntensity - colorIdeal):F1}, 方差={_colorVariance:F0}, 相似度={colorSim:F3}, 权重={colorW:F3}");  
        }

        return dirMatch * speedSim * colorW;
    }

    public void ResetBeliefs()
    {
        // 自动初始化（避免外部过早调用）
        if (Beliefs == null) 
            Beliefs = new float[grid.GOAL_STATES.Length];
        
        float uniform = 1f / grid.GOAL_STATES.Length;
        for (int g = 0; g < Beliefs.Length; g++)
            Beliefs[g] = uniform;
        
        // 重置历史记录
        beliefHistory.history.Clear();
        beliefHistory.assumedTrueGoal = planner != null ? planner.assumedTrueGoal : 0;
        currentStep = 0;
        
        // 记录初始状态
        RecordBeliefSnapshot(-1, -1, -1);
    }

    // 调试打印
    public void PrintCurrentBeliefs()
    {
        string msg = "当前信念: ";
        for (int g = 0; g < Beliefs.Length; g++)
            msg += $"G{g}={Beliefs[g]:F3} ";
        Debug.Log(msg);
    }

    // ==== 新增：记录信念快照 ====
    private void RecordBeliefSnapshot(int state, int action, int nextState)
    {
        if (Beliefs == null || Beliefs.Length != 4) return;
        
        var snapshot = new BeliefSnapshot
        {
            step = currentStep,
            state = state,
            action = action,
            nextState = nextState,
            belief = (float[])Beliefs.Clone(),
            goalProbs = new GoalProbabilities
            {
                goal_0 = Beliefs[0],
                goal_1 = Beliefs[1],
                goal_2 = Beliefs[2],
                goal_3 = Beliefs[3]
            }
        };
        
        beliefHistory.history.Add(snapshot);
        PrintStructuredLog(snapshot);
        currentStep++;
    }

    // 打印结构化日志
    private void PrintStructuredLog(BeliefSnapshot snapshot)
    {
        string logMsg = $"步骤 {snapshot.step}: ";
        logMsg += $"状态={snapshot.state}, ";
        logMsg += $"动作={snapshot.action}, ";
        logMsg += $"下一状态={snapshot.nextState}, ";
        logMsg += $"信念=[{Beliefs[0]:F3},{Beliefs[1]:F3},{Beliefs[2]:F3},{Beliefs[3]:F3}]";
        Debug.Log(logMsg);
    }

    [ContextMenu("导出信念历史为JSON")]
    public void ExportBeliefHistoryToJson()
    {
        if (beliefHistory.history.Count == 0)
        {
            Debug.LogWarning("信念历史为空，无法导出");
            return;
        }
        
        // 更新假设的真实目标
        if (planner != null)
            beliefHistory.assumedTrueGoal = planner.assumedTrueGoal;
        
        // 序列化为 JSON
        string json = JsonUtility.ToJson(beliefHistory, true);
        
        // 创建导出目录
        string directory = Application.dataPath + "/BeliefExports/";
        if (!Directory.Exists(directory))
            Directory.CreateDirectory(directory);
        
        // 生成文件名
        string timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
        int goalIdx = planner != null ? planner.assumedTrueGoal : 0;
        string filename = $"belief_history_goal{goalIdx}_{timestamp}.json";
        string filepath = Path.Combine(directory, filename);
        
        // 写入文件
        File.WriteAllText(filepath, json);
        
        Debug.Log($"✅ 信念历史已导出至: {filepath}");
        Debug.Log($"📊 包含 {beliefHistory.history.Count} 条记录");
        Debug.Log($"🎯 假设真实目标: 目标{goalIdx} (状态{grid.GOAL_STATES[goalIdx]})");
    }

    [ContextMenu("查看信念历史摘要")]
    public void PrintHistorySummary()
    {
        if (beliefHistory.history.Count == 0)
        {
            Debug.Log("信念历史为空");
            return;
        }
        
        Debug.Log($"=== 信念历史摘要 ===");
        Debug.Log($"总步数: {beliefHistory.history.Count}");
        Debug.Log($"假设真实目标: 目标{beliefHistory.assumedTrueGoal}");
        
        // 打印每步信念最大值
        for (int i = 0; i < Mathf.Min(10, beliefHistory.history.Count); i++)
        {
            var snap = beliefHistory.history[i];
            float maxBelief = Mathf.Max(snap.belief);
            int maxIdx = Array.IndexOf(snap.belief, maxBelief);
            Debug.Log($"步骤{snap.step}: 状态{snap.state}→{snap.nextState}, 动作{snap.action}, 最信目标={maxIdx}({maxBelief:F3})");
        }
        
        if (beliefHistory.history.Count > 10)
            Debug.Log($"... 还有 {beliefHistory.history.Count - 10} 条记录");
    }

    // 智能体到达目标时自动导出
    public void OnAgentReachedGoal()
    {
        if (autoExportOnFinish)
        {
            ExportBeliefHistoryToJson();
        }
    }

    private void PrintInitialConfig()
    {
        Debug.Log($"🔧 贝叶斯观测器初始化完成");
        Debug.Log($"  目标数量: {grid.GOAL_STATES.Length}");
        Debug.Log($"  初始信念: [{Beliefs[0]:F3},{Beliefs[1]:F3},{Beliefs[2]:F3},{Beliefs[3]:F3}]");
        Debug.Log($"  最大距离: G0={maxDistances[0]}, G1={maxDistances[1]}, G2={maxDistances[2]}, G3={maxDistances[3]}");
        Debug.Log($"  因子开关: 方向={useDirectionFactor}, 速度={useSpeedFactor}, 颜色={useColorFactor}");
    }
}